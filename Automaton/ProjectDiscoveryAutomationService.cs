using System.IO;
using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using Automaton.ProjectDiscoveryStates;
using OpenCvSharp;
using Serilog;

namespace Automaton;

internal sealed class ProjectDiscoveryAutomationService(
    ScreenCaptureService screenCaptureService,
    SampleImageProcessor sampleImageProcessor,
    PlayfieldDetector playfieldDetector,
    IAutomationInputController automationInputController,
    IGameActionService gameActionService,
    ConnectionLostPopupDetector connectionLostPopupDetector,
    ClientIsRunningButtonDetector clientIsRunningButtonDetector,
    IDiscoveryAutomationStateFactory discoveryAutomationStateFactory)
{
    private const string SamplesFolderName = "samples";
    private const string TrainingFolderName = "Training";
    private const string TrainingOutputFolderName = "playfields";
    private const int InitialPilotIndex = 1;
    private static readonly ILogger Logger = Log.ForContext<ProjectDiscoveryAutomationService>();
    private IProjectDiscoveryAutomationState m_CurrentState = null!;
    private ProjectDiscoveryAutomationContext m_Context = null!;

    private ScreenCaptureService ScreenCaptureService { get; } = screenCaptureService;

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
            var outputPath = ScreenCaptureService.WriteAnnotatedOutput(image, analysis, sampleFile);
            results.Add(analysis.Result with { OutputPath = outputPath });
        }

        Logger.Information(
            "Sample processing finished. SamplesDirectory={SamplesDirectory}, ResultCount={ResultCount}",
            SamplesFolderName,
            results.Count);
        return new SampleProcessingSummary(SamplesFolderName, results);
    }

    public TrainingExtractionSummary ExtractTrainingPlayfields()
    {
        var imageFiles = Directory
            .EnumerateFiles(TrainingFolderName, "*.png", SearchOption.TopDirectoryOnly)
            .Where(file => !Path.GetFileName(file).Contains("masked", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (imageFiles.Length == 0)
        {
            throw new InvalidOperationException($"No PNG files were found in {TrainingFolderName}.");
        }

        var extracted = 0;
        var skipped = 0;

        foreach (var imageFile in imageFiles)
        {
            using var image = Cv2.ImRead(imageFile);
            if (image.Empty())
            {
                skipped++;
                continue;
            }

            var detection = playfieldDetector.Detect(image);
            if (!detection.IsFound)
            {
                Logger.Warning("Playfield not found, skipping. File={File}", Path.GetFileName(imageFile));
                skipped++;
                continue;
            }

            using var playfield = new Mat(image, detection.Bounds);
            var outputFileName = Path.GetFileNameWithoutExtension(imageFile) + ".png";
            var outputPath = Path.Combine(TrainingOutputFolderName, outputFileName);
            Cv2.ImWrite(outputPath, playfield);
            extracted++;

            Logger.Information(
                "Extracted playfield. Source={Source}, Bounds={Bounds}, Output={Output}",
                Path.GetFileName(imageFile),
                detection.Bounds,
                outputFileName);

            TryExtractMaskedCompanion(TrainingFolderName, imageFile, detection.Bounds, TrainingOutputFolderName);
        }

        Logger.Information(
            "Training extraction completed. Extracted={Extracted}, Skipped={Skipped}, OutputDirectory={OutputDirectory}",
            extracted, skipped, TrainingOutputFolderName);

        return new TrainingExtractionSummary(extracted, skipped, TrainingOutputFolderName);
    }

    private static void TryExtractMaskedCompanion(string trainingDirectory, string originalFile, Rect bounds, string outputDirectory)
    {
        var baseName = Path.GetFileNameWithoutExtension(originalFile);
        var maskedFile = Directory
            .EnumerateFiles(trainingDirectory, "*.png", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(file =>
            {
                var fileName = Path.GetFileName(file);
                return fileName.Contains("masked", StringComparison.OrdinalIgnoreCase) &&
                       fileName.StartsWith(baseName + ".", StringComparison.OrdinalIgnoreCase);
            });

        if (maskedFile is null)
        {
            return;
        }

        using var maskedImage = Cv2.ImRead(maskedFile);
        if (maskedImage.Empty())
        {
            return;
        }

        if (bounds.Right > maskedImage.Width || bounds.Bottom > maskedImage.Height)
        {
            Logger.Warning(
                "Masked image too small for playfield bounds, skipping. MaskedFile={MaskedFile}, ImageSize={Width}x{Height}, Bounds={Bounds}",
                Path.GetFileName(maskedFile), maskedImage.Width, maskedImage.Height, bounds);
            return;
        }

        using var maskedPlayfield = new Mat(maskedImage, bounds);
        var maskedOutputFileName = baseName + ".masked.png";
        var maskedOutputPath = Path.Combine(outputDirectory, maskedOutputFileName);
        Cv2.ImWrite(maskedOutputPath, maskedPlayfield);

        Logger.Information(
            "Extracted masked companion. Source={Source}, Output={Output}",
            Path.GetFileName(maskedFile),
            maskedOutputFileName);
    }

    public DiscoveryAutomationStepSummary Automate(
        CancellationToken cancellationToken,
        DiscoveryAutomationStateKind startingState = DiscoveryAutomationStateKind.Discover,
        int initialPilotIndex = InitialPilotIndex,
        bool keepDebugImages = false)
    {
        Logger.Information("Automation loop starting. InitialPilotIndex={InitialPilotIndex}", initialPilotIndex);
        automationInputController.Delay(Delays.AutomationStartupDelayMs, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        m_Context = new ProjectDiscoveryAutomationContext(initialPilotIndex, keepDebugImages);

        m_CurrentState = CreateState(startingState);

        DiscoveryAutomationStepSummary? lastSummary = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (m_CurrentState.Kind == DiscoveryAutomationStateKind.Discover)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var capture = ScreenCaptureService.CaptureCurrentScreenImage();
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
            ScreenCaptureService.FlushClickTrace();
        }

        return lastSummary ?? throw new OperationCanceledException(cancellationToken);
    }

    private DiscoveryAutomationStepSummary ExecuteSingleStep(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DiscoveryAutomationStateTransition transition = null!;
        for (var attempt = 1; attempt <= Settings.DetectionRetryAttempts; attempt++)
        {
            transition = m_CurrentState.Execute(m_Context, cancellationToken);
            if (!ShouldRetryAfterDetectionMiss(transition) || attempt >= Settings.DetectionRetryAttempts)
            {
                break;
            }

            Logger.Warning(
                "Detection miss in {State}. Retrying once before recovery. Attempt={Attempt}/{MaxAttempts}, CapturePath={CapturePath}",
                transition.State,
                attempt,
                Settings.DetectionRetryAttempts,
                transition.CapturePath);
            automationInputController.Delay(Settings.DetectionRetryDelayMs, cancellationToken);
        }

        Logger.Information(
            "Project Discovery automation step executed. State={State}, NextState={NextState}, Action={Action}",
            transition.State,
            transition.NextState,
            transition.Action);
        m_Context.LastAction = transition.Action;
        m_CurrentState = CreateState(transition.NextState);

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
        using var capture = ScreenCaptureService.CaptureCurrentScreen(".discovery-connection-lost-popup-check");
        var detection = connectionLostPopupDetector.Detect(capture.Image);
        if (detection.State != PopupState.ConnectionLost)
        {
            return false;
        }

        DrawPopupDebugOverlay(capture.CapturePath, detection, "Connection lost popup detected");
        Logger.Warning("Connection Lost popup detected during {CurrentState}. CapturePath={CapturePath}", m_CurrentState.Kind, capture.CapturePath);
        m_CurrentState = CreateState(DiscoveryAutomationStateKind.RecoverConnectionLostPopup);
        return true;
    }

    private bool TryTransitionToRecoverClientIsRunningButtonVisible(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var capture = ScreenCaptureService.CaptureCurrentScreen(".discovery-client-is-running-button-check");
        if (!clientIsRunningButtonDetector.Detect(capture.Image, out var location))
        {
            return false;
        }

        DrawButtonDebugOverlay(capture.CapturePath, location.Bounds, "Client Is Running button detected");
        Logger.Warning(
            "Client Is Running button detected during {CurrentState}. CapturePath={CapturePath}",
            m_CurrentState.Kind,
            capture.CapturePath);
        m_CurrentState = CreateState(DiscoveryAutomationStateKind.RecoverClientIsRunningButtonVisible);
        return true;
    }

    private static void DrawPopupDebugOverlay(string imagePath, PopupDetection detection, string label)
    {
        using var image = Cv2.ImRead(imagePath);
        if (image.Empty())
        {
            return;
        }

        DebugOverlay.Annotate(image, (detection.Bounds, OverlayColor.RedOrange));
        DebugOverlay.Label(image, label, OverlayColor.RedOrange);
        Cv2.ImWrite(imagePath, image);
    }

    private static void DrawButtonDebugOverlay(string imagePath, Rect bounds, string label)
    {
        using var image = Cv2.ImRead(imagePath);
        if (image.Empty())
        {
            return;
        }

        DebugOverlay.Annotate(image, (bounds, OverlayColor.RedOrange));
        DebugOverlay.Label(image, label, OverlayColor.RedOrange);
        Cv2.ImWrite(imagePath, image);
    }

    private IProjectDiscoveryAutomationState CreateState(DiscoveryAutomationStateKind stateKind)
    {
        return discoveryAutomationStateFactory.Create(stateKind);
    }
}

internal sealed record SampleProcessingSummary(
    string SamplesDirectory,
    IReadOnlyList<SampleProcessingResult> Results);

internal sealed record TrainingExtractionSummary(
    int Extracted,
    int Skipped,
    string OutputDirectory);
