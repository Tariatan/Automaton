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

        var context = new ProjectDiscoveryAutomationContext(initialPilotIndex);
        var currentState = SetCurrentState(startingState, progress);

        DiscoveryAutomationStepSummary? lastSummary = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (currentState.Kind == DiscoveryAutomationStateKind.Discover)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var capture = screenCaptureService.CaptureCurrentScreenImage();
                    gameActionService.TryHideUi(capture, cancellationToken);
                }

                (lastSummary, currentState) = ExecuteSingleStep(currentState, context, progress, cancellationToken);

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

                if (TryTransitionToRecoverConnectionLostPopup(currentState, progress, cancellationToken, out var afterConnectionLostState))
                {
                    currentState = afterConnectionLostState;
                    continue;
                }

                if (currentState.Kind != DiscoveryAutomationStateKind.RecoverClientIsRunningButtonVisible &&
                    TryTransitionToRecoverClientIsRunningButtonVisible(currentState, progress, cancellationToken, out var afterClientIsRunningState))
                {
                    currentState = afterClientIsRunningState;
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

    private (DiscoveryAutomationStepSummary Summary, IProjectDiscoveryAutomationState NextState) ExecuteSingleStep(
        IProjectDiscoveryAutomationState currentState,
        ProjectDiscoveryAutomationContext context,
        IProgress<DiscoveryAutomationStateKind>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DiscoveryAutomationStateTransition transition = null!;
        for (var attempt = 1; attempt <= Config.DetectionRetryAttempts; attempt++)
        {
            transition = currentState.Execute(context, cancellationToken);
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
        context.LastAction = transition.Action;
        var nextState = SetCurrentState(transition.NextState, progress);

        return (new DiscoveryAutomationStepSummary(
            transition.State,
            transition.NextState,
            transition.Action,
            transition.CapturePath),
            nextState);
    }

    private static bool ShouldRetryAfterDetectionMiss(DiscoveryAutomationStateTransition transition)
    {
        return transition is { Action: DiscoveryAutomationActionKind.Recover, FailureReason: DiscoveryAutomationFailureReason.DetectionMiss };
    }

    private bool TryTransitionToRecoverConnectionLostPopup(
        IProjectDiscoveryAutomationState currentState,
        IProgress<DiscoveryAutomationStateKind>? progress,
        CancellationToken cancellationToken,
        out IProjectDiscoveryAutomationState nextState)
    {
        cancellationToken.ThrowIfCancellationRequested();
        nextState = currentState;
        using var capture = screenCaptureService.CaptureCurrentScreenInMemory(".discovery-connection-lost-popup-check");
        var detection = connectionLostPopupDetector.Detect(capture.Image);
        if (detection.State != PopupState.ConnectionLost)
        {
            return false;
        }

        DrawDebugOverlay(capture.Image, detection.Bounds, "Connection lost popup detected");
        screenCaptureService.SaveCapture(capture);
        Logger.Warning("Connection Lost popup detected during {CurrentState}. CapturePath={CapturePath}", currentState.Kind, capture.CapturePath);
        nextState = SetCurrentState(DiscoveryAutomationStateKind.RecoverConnectionLostPopup, progress);
        return true;
    }

    private bool TryTransitionToRecoverClientIsRunningButtonVisible(
        IProjectDiscoveryAutomationState currentState,
        IProgress<DiscoveryAutomationStateKind>? progress,
        CancellationToken cancellationToken,
        out IProjectDiscoveryAutomationState nextState)
    {
        cancellationToken.ThrowIfCancellationRequested();
        nextState = currentState;
        using var capture = screenCaptureService.CaptureCurrentScreenInMemory(".discovery-client-is-running-button-check");
        if (!clientIsRunningButtonDetector.Detect(capture.Image, out var location))
        {
            return false;
        }

        DrawDebugOverlay(capture.Image, location.Bounds, "Client Is Running button detected");
        screenCaptureService.SaveCapture(capture);
        Logger.Warning(
            "Client Is Running button detected during {CurrentState}. CapturePath={CapturePath}",
            currentState.Kind,
            capture.CapturePath);
        nextState = SetCurrentState(DiscoveryAutomationStateKind.RecoverClientIsRunningButtonVisible, progress);
        return true;
    }

    private static void DrawDebugOverlay(Mat image, Rect bounds, string label)
    {
        if (image.Empty())
        {
            return;
        }

        DebugOverlay.Annotate(image, (bounds, OverlayColor.RedOrange));
        DebugOverlay.Label(image, label, OverlayColor.RedOrange);
    }

    private IProjectDiscoveryAutomationState SetCurrentState(
        DiscoveryAutomationStateKind kind,
        IProgress<DiscoveryAutomationStateKind>? progress)
    {
        var state = discoveryAutomationStateFactory.Create(kind);
        progress?.Report(kind);
        return state;
    }
}

internal sealed record SampleProcessingSummary(
    string SamplesDirectory,
    IReadOnlyList<SampleProcessingResult> Results);
