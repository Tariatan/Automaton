using System.IO;
using Automaton.Core.Detectors;
using Automaton.Core.Helpers;
using Automaton.Core.Primitives;
using Automaton.Helpers;
using Automaton.ProjectDiscoveryStates;
using OpenCvSharp;
using Serilog;

namespace Automaton;

internal sealed class ProjectDiscoveryAutomationService(
    ScreenCaptureService screenCaptureService,
    SampleImageProcessor sampleImageProcessor,
    IAutomationInputController automationInputController,
    IGameActionService gameActionService,
    ConnectionLostPopupDetector connectionLostPopupDetector,
    ClientIsRunningButtonDetector clientIsRunningButtonDetector,
    IDiscoveryAutomationStateFactory discoveryAutomationStateFactory)
{
    private const string SamplesFolderName = "samples";
    private const int InitialPilotIndex = 1;
    private static readonly ILogger Logger = Log.ForContext<ProjectDiscoveryAutomationService>();
    private IProjectDiscoveryAutomationState m_CurrentState = null!;
    private ProjectDiscoveryAutomationContext m_Context = null!;
    private IProgress<DiscoveryAutomationStateKind>? m_Progress;

    public SampleProcessingSummary ProcessSamples()
    {
        Logger.Information("Sample processing started. SamplesDirectory={SamplesDirectory}", SamplesFolderName);
        if (!Directory.Exists(SamplesFolderName))
        {
            throw new DirectoryNotFoundException($"Samples folder was not found: {SamplesFolderName}");
        }

        var sampleFiles = SampleImageProcessor.EnumerateSampleImageFiles(SamplesFolderName);

        if (sampleFiles.Count == 0)
        {
            throw new InvalidOperationException($"No files were found in {SamplesFolderName}.");
        }

        var results = new List<SampleProcessingResult>(sampleFiles.Count);
        foreach (var sampleFile in sampleFiles)
        {
            using var image = Cv2.ImRead(sampleFile);
            var analysis = sampleImageProcessor.AnalyzeImage(image, sampleFile);
            var outputPath = ImageAnnotator.WriteAnnotatedOutput(image, analysis, sampleFile);
            results.Add(analysis.Result with { OutputPath = outputPath });
        }

        Logger.Information(
            "Sample processing finished. SamplesDirectory={SamplesDirectory}, ResultCount={ResultCount}",
            SamplesFolderName,
            results.Count);
        return new SampleProcessingSummary(SamplesFolderName, results);
    }

    public DiscoveryAutomationStepSummary Automate(
        CancellationToken cancellationToken,
        DiscoveryAutomationStateKind startingState = DiscoveryAutomationStateKind.Discover,
        int initialPilotIndex = InitialPilotIndex,
        IProgress<DiscoveryAutomationStateKind>? progress = null)
    {
        Logger.Information("Automation loop starting. InitialPilotIndex={InitialPilotIndex}", initialPilotIndex);
        automationInputController.Delay(Delays.AutomationStartupDelayMs, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        m_Progress = progress;
        m_Context = new ProjectDiscoveryAutomationContext(initialPilotIndex);

        SetCurrentState(startingState);

        DiscoveryAutomationStepSummary? lastSummary = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (m_CurrentState.Kind == DiscoveryAutomationStateKind.Discover)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var capture = screenCaptureService.CaptureCurrentScreenImage();
                    gameActionService.TryHideUi(capture, cancellationToken);
                }

                lastSummary = ExecuteSingleStep(cancellationToken);

                if (lastSummary.Action == DiscoveryAutomationActionKind.Reboot)
                {
                    Logger.Information(
                        "Project Discovery automation requested operating system reboot. State={State}, NextState={NextState}",
                        lastSummary.State,
                        lastSummary.NextState);
                    return lastSummary;
                }

                if (lastSummary.Action == DiscoveryAutomationActionKind.Shutdown)
                {
                    Logger.Information(
                        "Project Discovery automation requested safe application shutdown. State={State}, NextState={NextState}",
                        lastSummary.State,
                        lastSummary.NextState);
                    return lastSummary;
                }

                if (lastSummary.Action == DiscoveryAutomationActionKind.NoFurtherPilotsAvailable)
                {
                    Logger.Information(
                        "Project Discovery automation completed for all available pilots. State={State}, NextState={NextState}",
                        lastSummary.State,
                        lastSummary.NextState);
                    return lastSummary;
                }

                if (TryTransitionToRecoverConnectionLostPopup(cancellationToken))
                {
                    continue;
                }

                if (lastSummary.State != DiscoveryAutomationStateKind.RecoverClientIsRunningButtonVisible &&
                    TryTransitionToRecoverClientIsRunningButtonVisible(cancellationToken))
                {
                    continue;
                }

                automationInputController.Delay(Delays.StateMachineNextStepDelayMs, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (lastSummary is not null)
        {
            Logger.Information("Automation loop canceled after a completed cycle. State={State}, NextState={NextState}, Action={Action}, CapturePath={CapturePath}",
                    lastSummary.State,
                    lastSummary.NextState,
                    lastSummary.Action,
                    lastSummary.CapturePath);
            return lastSummary;
        }
        finally
        {
            screenCaptureService.FlushClickTrace();
        }

        return lastSummary ?? throw new OperationCanceledException(cancellationToken);
    }

    private DiscoveryAutomationStepSummary ExecuteSingleStep(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DiscoveryAutomationStateTransition transition = null!;
        for (var attempt = 1; attempt <= Config.DetectionRetryAttempts; attempt++)
        {
            transition = m_CurrentState.Execute(m_Context, cancellationToken);
            if (!ShouldRetryAfterDetectionMiss(transition) || attempt >= Config.DetectionRetryAttempts)
            {
                break;
            }

            Logger.Warning(
                "Detection miss in {State}. Retrying once before recovery. Attempt={Attempt}/{MaxAttempts}, CapturePath={CapturePath}",
                transition.State,
                attempt,
                Config.DetectionRetryAttempts,
                transition.CapturePath);
            automationInputController.Delay(Config.DetectionRetryDelayMs, cancellationToken);
        }

        Logger.Information(
            "Project Discovery automation step executed. State={State}, NextState={NextState}, Action={Action}",
            transition.State,
            transition.NextState,
            transition.Action);
        m_Context.LastAction = transition.Action;
        SetCurrentState(transition.NextState);

        return new DiscoveryAutomationStepSummary(
            transition.State,
            transition.NextState,
            transition.Action,
            transition.CapturePath);
    }

    private static bool ShouldRetryAfterDetectionMiss(DiscoveryAutomationStateTransition transition)
    {
        return transition is { Action: DiscoveryAutomationActionKind.Recover, FailureReason: DiscoveryAutomationFailureReason.DetectionMiss };
    }

    private bool TryTransitionToRecoverConnectionLostPopup(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var capture = screenCaptureService.CaptureCurrentScreenInMemory(".discovery-connection-lost-popup-check");
        var detection = connectionLostPopupDetector.Detect(capture.Image);
        if (detection.State != PopupState.ConnectionLost)
        {
            return false;
        }

        DrawPopupDebugOverlay(capture.Image, detection, "Connection lost popup detected");
        screenCaptureService.SaveCapture(capture);
        Logger.Warning("Connection Lost popup detected during {CurrentState}. CapturePath={CapturePath}", m_CurrentState.Kind, capture.CapturePath);
        SetCurrentState(DiscoveryAutomationStateKind.RecoverConnectionLostPopup);
        return true;
    }

    private bool TryTransitionToRecoverClientIsRunningButtonVisible(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var capture = screenCaptureService.CaptureCurrentScreenInMemory(".discovery-client-is-running-button-check");
        if (!clientIsRunningButtonDetector.Detect(capture.Image, out var location))
        {
            return false;
        }

        DrawButtonDebugOverlay(capture.Image, location.Bounds, "Client Is Running button detected");
        screenCaptureService.SaveCapture(capture);
        Logger.Warning(
            "Client Is Running button detected during {CurrentState}. CapturePath={CapturePath}",
            m_CurrentState.Kind,
            capture.CapturePath);
        SetCurrentState(DiscoveryAutomationStateKind.RecoverClientIsRunningButtonVisible);
        return true;
    }

    private static void DrawPopupDebugOverlay(Mat image, PopupDetection detection, string label)
    {
        if (image.Empty())
        {
            return;
        }

        DebugOverlay.Annotate(image, (detection.Bounds, OverlayColor.RedOrange));
        DebugOverlay.Label(image, label, OverlayColor.RedOrange);
    }

    private static void DrawButtonDebugOverlay(Mat image, Rect bounds, string label)
    {
        if (image.Empty())
        {
            return;
        }

        DebugOverlay.Annotate(image, (bounds, OverlayColor.RedOrange));
        DebugOverlay.Label(image, label, OverlayColor.RedOrange);
    }

    private void SetCurrentState(DiscoveryAutomationStateKind kind)
    {
        m_CurrentState = CreateState(kind);
        m_Progress?.Report(kind);
    }

    private IProjectDiscoveryAutomationState CreateState(DiscoveryAutomationStateKind stateKind)
    {
        return discoveryAutomationStateFactory.Create(stateKind);
    }
}

internal sealed record SampleProcessingSummary(
    string SamplesDirectory,
    IReadOnlyList<SampleProcessingResult> Results);
