using Automaton.CommonAutomationStates;
using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using Automaton.ProjectDiscoveryStates;
using OpenCvSharp;
using Serilog;
using System.IO;
using System.Windows;
using Point = OpenCvSharp.Point;
using Rect = OpenCvSharp.Rect;

namespace Automaton;

internal sealed class ProjectDiscoveryAutomationService(
    ScreenCaptureService screenCaptureService,
    IAutomationInputController automationInputController,
    IAutomationClock automationClock,
    MaxSubmissionsPopupDetector maximumSubmissionsPopupDetector,
    SlowDownPopupDetector slowDownPopupDetector,
    ConnectionLostPopupDetector connectionLostPopupDetector,
    PlayNowButtonLocator playNowButtonLocator,
    IDiscoveryAutomationStateFactory discoveryAutomationStateFactory)
{
    private readonly CommonLoginState m_CommonLoginState = new(automationInputController, screenCaptureService);
    private readonly CommonExitState m_CommonExitState = new(automationInputController);
    private const int MaximumSubmissionsPerWindow = 5;
    private const int InitialPilotIndex = 1;
    private const string PilotNotFoundDebugTextTemplate = "Pilot {0} not found";
    private const double DebugOverlayTextScale = 0.8;
    private const int DebugOverlayTextThickness = 2;
    private const int DebugOverlayLeftPadding = 30;
    private const int DebugOverlayTopPadding = 40;
    private static readonly Rect ControlButtonBounds = new(930, 645, 271, 11);
    private static readonly ILogger Logger = Log.ForContext<ProjectDiscoveryAutomationService>();
    private IProjectDiscoveryAutomationState m_CurrentState = null!;
    private ProjectDiscoveryAutomationContext m_Context = null!;

    private readonly AutomationSubmitRateLimiter m_SubmitRateLimiter = new();
    private readonly Random m_Random = new();

    internal int CurrentPilotIndex { get; private set; } = InitialPilotIndex;

    internal ScreenCaptureService ScreenCaptureService { get; } = screenCaptureService;

    public void ProcessSamples()
    {
        Logger.Information("Processing samples through automation service.");
        ScreenCaptureService.ProcessSamples();
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

        CurrentPilotIndex = initialPilotIndex;
        m_CurrentState = CreateState(startingState);

        DiscoveryAutomationStepSummary? lastSummary = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                lastSummary = ExecuteSingleStep(cancellationToken);
                if (lastSummary.Action == DiscoveryAutomationActionKind.StopAutomation)
                {
                    Logger.Information(
                        "Project Discovery automation requested application exit. State={State}, NextState={NextState}",
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

        return lastSummary ?? throw new OperationCanceledException(cancellationToken);
    }

    private DiscoveryAutomationStepSummary ExecuteSingleStep(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var transition = m_CurrentState.Execute(m_Context, cancellationToken);
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

    private IProjectDiscoveryAutomationState CreateState(DiscoveryAutomationStateKind stateKind)
    {
        return discoveryAutomationStateFactory.Create(stateKind);
    }

    internal AutomationSummary AutomateSingleCycle(
        DpiScale dpi,
        ScreenCaptureAnalysisSummary captureSummary,
        TraceImageScope traceImages,
        CancellationToken cancellationToken)
    {
        var clickedPointCount = ClickPolygonPoints(captureSummary.Analysis.Polygons, cancellationToken);
        Logger.Information("Automation cycle analyzed screen. CapturePath={CapturePath}, PlayfieldFound={PlayfieldFound}, ClusterCount={ClusterCount}, ClickedPointCount={ClickedPointCount}", captureSummary.CapturePath, captureSummary.Analysis.Result.PlayfieldFound, captureSummary.Analysis.Result.ClusterCount, clickedPointCount);

        automationInputController.Delay(Delays.MinimumClickMs, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        // Focus the known safe control button area.
        FocusControlButton(ControlButtonBounds, cancellationToken);

        DelayBeforeRateLimitedSubmit(m_SubmitRateLimiter, cancellationToken);

        // Left-click the 'Submit' button.
        automationInputController.LeftClick(cancellationToken);
        m_SubmitRateLimiter.RecordSubmit(automationClock.UtcNow);
        automationInputController.Delay(Delays.MinimumClickMs, cancellationToken);
        var focusedCapturePath = CaptureFocusedScreenTrace(captureSummary, cancellationToken);
        traceImages.Track(focusedCapturePath);
        var focusedPopupState = PopupState.None;
        if (!playNowButtonLocator.TryLocateAndDrawDebugOverlay(focusedCapturePath, out var playButtonLocation))
        {
            var connectionLostDetection = connectionLostPopupDetector.Detect(focusedCapturePath);
            var slowDownDetection = slowDownPopupDetector.Detect(focusedCapturePath);
            var maximumSubmissionsDetection = maximumSubmissionsPopupDetector.Detect(focusedCapturePath);
            var detected = new[]
            {
                connectionLostDetection,
                slowDownDetection,
                maximumSubmissionsDetection
            }.Where(detection => detection.State != PopupState.None).ToArray();

            focusedPopupState = detected.Length switch
            {
                0 => PopupState.None,
                1 => detected[0].State,
                _ => PopupState.Unknown
            };
        }
        else
        {
            Logger.Information(
                "Skipping popup detection because launcher PLAY NOW button was found. CapturePath={CapturePath}, PlayButtonBounds={PlayButtonBounds}",
                focusedCapturePath,
                playButtonLocation.Bounds);
        }
        if (TryHandleDetectedPopup(
            focusedPopupState,
            captureSummary,
            traceImages,
            clickedPointCount,
            ControlButtonBounds,
            focusedCapturePath,
            "post-submit focus",
            cancellationToken,
            out var popupSummary))
        {
            return popupSummary;
        }

        // Left-click the 'Continue' button.
        automationInputController.LeftClick(cancellationToken);
        automationInputController.Delay(Delays.MinimumClickMs, cancellationToken);

        // Left-click the next 'Continue' button.
        automationInputController.LeftClick(cancellationToken);
        Logger.Information(
            "Automation cycle submitted and continued. CapturePath={CapturePath}, FocusedCapturePath={FocusedCapturePath}, ClickedPointCount={ClickedPointCount}, CurrentPilotIndex={CurrentPilotIndex}",
            captureSummary.CapturePath,
            focusedCapturePath,
            clickedPointCount,
            CurrentPilotIndex);

        return new AutomationSummary(
            captureSummary,
            clickedPointCount,
            ControlButtonBounds,
            focusedCapturePath,
            CurrentPilotIndex: CurrentPilotIndex);
    }

    private PilotSwitchResult SwitchToNextPilot(
        ScreenCaptureAnalysisSummary captureSummary,
        TraceImageScope traceImages,
        CancellationToken cancellationToken)
    {
        if (!PilotAvatarLocator.TryGetNextPilotIndex(CurrentPilotIndex, out var nextPilotIndex))
        {
            Logger.Warning("No next pilot is configured => Quit Game. CurrentPilotIndex={CurrentPilotIndex}", CurrentPilotIndex);
            // Quit Game
            m_CommonExitState.QuitGame(cancellationToken);
            return new PilotSwitchResult(CurrentPilotIndex, Succeeded: false, null);
        }

        Logger.Information("Switching pilot. CurrentPilotIndex={CurrentPilotIndex}, TargetPilotIndex={TargetPilotIndex}", CurrentPilotIndex, nextPilotIndex);
        automationInputController.Logout(cancellationToken);

        // Make screenshot of pilots on login screen
        var pilotSelectionCapturePath = CapturePilotSelectionScreenTrace(captureSummary, nextPilotIndex, cancellationToken);
        traceImages.Track(pilotSelectionCapturePath);

        // Locate next pilot
        if (!m_CommonLoginState.TryLoginPilot(
            nextPilotIndex,
            pilotSelectionCapturePath,
            cancellationToken,
            out var location))
        {
            // Failed to locate requested pilot
            DrawPilotNotFoundDebugOverlay(pilotSelectionCapturePath, nextPilotIndex);
            
            Logger.Warning("Target pilot was not found. TargetPilotIndex={TargetPilotIndex}, CapturePath={CapturePath}", nextPilotIndex, pilotSelectionCapturePath);
            return new PilotSwitchResult(nextPilotIndex, Succeeded: false, pilotSelectionCapturePath);
        }

        // Login requested pilot
        CurrentPilotIndex = nextPilotIndex;

        Logger.Information("Pilot switch succeeded. CurrentPilotIndex={CurrentPilotIndex}, CapturePath={CapturePath}, Bounds={Bounds}", CurrentPilotIndex, pilotSelectionCapturePath, location);
        return new PilotSwitchResult(nextPilotIndex, Succeeded: true, pilotSelectionCapturePath);
    }

    internal bool TryHandleDetectedPopup(
        PopupState popupState,
        ScreenCaptureAnalysisSummary captureSummary,
        TraceImageScope traceImages,
        int clickedPointCount,
        Rect? controlBounds,
        string focusedCapturePath,
        string detectionStage,
        CancellationToken cancellationToken,
        out AutomationSummary summary)
    {
        switch (popupState)
        {
            case PopupState.None:
                summary = null!;
                return false;
            default:
                summary = HandlePopupState(
                    popupState,
                    captureSummary,
                    clickedPointCount,
                    controlBounds,
                    focusedCapturePath,
                    detectionStage,
                    cancellationToken,
                    traceImages);
                return true;
        }
    }

    internal AutomationSummary HandlePopupState(
        PopupState popupState,
        ScreenCaptureAnalysisSummary captureSummary,
        int clickedPointCount,
        Rect? controlBounds,
        string focusedCapturePath,
        string detectionStage,
        CancellationToken cancellationToken,
        TraceImageScope? traceImages = null)
    {
        switch (popupState)
        {
            case PopupState.ConnectionLost:
                Logger.Error("Connection Lost popup detected during {DetectionStage}. Stopping automation. FocusedCapturePath={FocusedCapturePath}", detectionStage, focusedCapturePath);
                automationInputController.Delay(Delays.ConnectionLostExitMs, cancellationToken);
                automationInputController.PressKey(VirtualKeys.Enter, cancellationToken);
                return new AutomationSummary(
                    captureSummary,
                    clickedPointCount,
                    controlBounds,
                    focusedCapturePath,
                    CurrentPilotIndex: CurrentPilotIndex,
                    ConnectionLostDetected: true);
            case PopupState.SlowDown:
                RecoverFromSlowDownPopup(focusedCapturePath, cancellationToken);
                return new AutomationSummary(
                    captureSummary,
                    clickedPointCount,
                    controlBounds,
                    focusedCapturePath,
                    CurrentPilotIndex: CurrentPilotIndex,
                    SlowDownPopupDetected: true);
            case PopupState.MaxSubmissions:
            {
                using var localTraceImages = traceImages is null ? new TraceImageScope(m_Context.KeepDebugImages) : null;
                var effectiveTraceImages = traceImages ?? localTraceImages!;
                var pilotSwitchResult = SwitchToNextPilot(captureSummary, effectiveTraceImages, cancellationToken);
                Logger.Warning("Maximum submissions popup detected during {DetectionStage}. FocusedCapturePath={FocusedCapturePath}, CurrentPilotIndex={CurrentPilotIndex}, TargetPilotIndex={TargetPilotIndex}, PilotSwitchSucceeded={PilotSwitchSucceeded}, PilotSwitchCapturePath={PilotSwitchCapturePath}", detectionStage, focusedCapturePath, CurrentPilotIndex, pilotSwitchResult.TargetPilotIndex, pilotSwitchResult.Succeeded, pilotSwitchResult.CapturePath);
                return new AutomationSummary(
                    captureSummary,
                    clickedPointCount,
                    controlBounds,
                    focusedCapturePath,
                    MaximumSubmissionsReached: true,
                    CurrentPilotIndex: CurrentPilotIndex,
                    TargetPilotIndex: pilotSwitchResult.TargetPilotIndex,
                    PilotSwitchSucceeded: pilotSwitchResult.Succeeded,
                    PilotSwitchCapturePath: pilotSwitchResult.CapturePath,
                    NoFurtherPilotsAvailable: !pilotSwitchResult.Succeeded &&
                                              pilotSwitchResult.TargetPilotIndex == CurrentPilotIndex);
            }
            case PopupState.Unknown:
                Logger.Error("Ambiguous popup detected during {DetectionStage}. Stopping automation for safety. FocusedCapturePath={FocusedCapturePath}", detectionStage, focusedCapturePath);
                return new AutomationSummary(
                    captureSummary,
                    clickedPointCount,
                    controlBounds,
                    focusedCapturePath,
                    CurrentPilotIndex: CurrentPilotIndex,
                    PopupDetectionAmbiguous: true);
            default:
                return new AutomationSummary(
                    captureSummary,
                    clickedPointCount,
                    controlBounds,
                    focusedCapturePath,
                    CurrentPilotIndex: CurrentPilotIndex,
                    PopupDetectionAmbiguous: true);
        }
    }

    private void RecoverFromSlowDownPopup(string focusedCapturePath, CancellationToken cancellationToken)
    {
        Logger.Warning(
            "Slow Down popup detected. FocusedCapturePath={FocusedCapturePath}, RecoveryDelayMilliseconds={RecoveryDelayMilliseconds}",
            focusedCapturePath,
            Delays.SubmissionWindowMs);
        automationInputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.W, cancellationToken);
        automationInputController.Delay(Delays.SubmissionWindowMs, cancellationToken);
    }

    private void DelayBeforeRateLimitedSubmit(AutomationSubmitRateLimiter rateLimiter, CancellationToken cancellationToken)
    {
        var delay = rateLimiter.GetDelayBeforeNextSubmit(automationClock.UtcNow);
        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        Logger.Information("Waiting before submit because of rate limit. DelayMilliseconds={DelayMilliseconds}", (int)Math.Ceiling(delay.TotalMilliseconds));
        automationInputController.Delay((int)Math.Ceiling(delay.TotalMilliseconds), cancellationToken);
    }

    private int ClickPolygonPoints(IReadOnlyList<Point[]> polygons, CancellationToken cancellationToken)
    {
        Logger.Debug("Clicking polygon points. PolygonCount={PolygonCount}", polygons.Count);
        var clickedPointCount = 0;

        foreach (var polygon in polygons)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (polygon.Length == 0)
            {
                continue;
            }

            foreach (var point in polygon)
            {
                cancellationToken.ThrowIfCancellationRequested();
                automationInputController.MoveTo(point);
                automationInputController.LeftClick(cancellationToken);
                clickedPointCount++;
                automationInputController.Delay(Delays.MinimumClickMs, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            automationInputController.MoveTo(polygon[0]);
            automationInputController.LeftClick(cancellationToken);
            clickedPointCount++;
            automationInputController.Delay(Delays.MinimumClickMs, cancellationToken);
        }

        Logger.Debug("Finished clicking polygon points. ClickedPointCount={ClickedPointCount}", clickedPointCount);
        return clickedPointCount;
    }

    private void FocusControlButton(Rect controlButtonBounds, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var anchor = new Point(
            m_Random.Next(controlButtonBounds.X, controlButtonBounds.Right),
            m_Random.Next(controlButtonBounds.Y, controlButtonBounds.Bottom));

        automationInputController.MoveTo(anchor);
        automationInputController.Delay(Delays.HoverMs, cancellationToken);
    }

    private string CaptureFocusedScreenTrace(ScreenCaptureAnalysisSummary captureSummary, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var focusedCapturePath = Path.Combine(
            captureSummary.CapturesDirectory,
            $"{Path.GetFileNameWithoutExtension(captureSummary.CapturePath)}.focused.png");
        ScreenCaptureService.CaptureCurrentScreenToFile(focusedCapturePath);
        return focusedCapturePath;
    }

    private string CapturePilotSelectionScreenTrace(
        ScreenCaptureAnalysisSummary captureSummary,
        int pilotIndex,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pilotSelectionCapturePath = Path.Combine(
            captureSummary.CapturesDirectory,
            $"{Path.GetFileNameWithoutExtension(captureSummary.CapturePath)}.pilot-{pilotIndex}.png");
        ScreenCaptureService.CaptureCurrentScreenToFile(pilotSelectionCapturePath);
        return pilotSelectionCapturePath;
    }

    private static void DrawPilotNotFoundDebugOverlay(string imagePath, int pilotIndex)
    {
        DrawDebugOverlay(imagePath, string.Format(PilotNotFoundDebugTextTemplate, pilotIndex));
    }

    private static void DrawDebugOverlay(string imagePath, string text)
    {
        using var image = Cv2.ImRead(imagePath);
        if (image.Empty())
        {
            return;
        }

        Cv2.PutText(
            image,
            text,
            new Point(DebugOverlayLeftPadding, DebugOverlayTopPadding),
            HersheyFonts.HersheySimplex,
            DebugOverlayTextScale,
            new Scalar(80, 120, 255),
            DebugOverlayTextThickness,
            LineTypes.AntiAlias);
        Cv2.ImWrite(imagePath, image);
    }

    private sealed class AutomationSubmitRateLimiter
    {
        private readonly Queue<DateTime> m_SubmittedAtUtc = new();

        public TimeSpan GetDelayBeforeNextSubmit(DateTime utcNow)
        {
            RemoveExpiredSubmissions(utcNow);
            if (m_SubmittedAtUtc.Count < MaximumSubmissionsPerWindow)
            {
                return TimeSpan.Zero;
            }

            var elapsed = utcNow - m_SubmittedAtUtc.Peek();
            var remaining = TimeSpan.FromMilliseconds(Delays.SubmissionWindowMs) - elapsed;
            return remaining <= TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }

        public void RecordSubmit(DateTime utcNow)
        {
            RemoveExpiredSubmissions(utcNow);
            m_SubmittedAtUtc.Enqueue(utcNow);
        }

        private void RemoveExpiredSubmissions(DateTime utcNow)
        {
            while (m_SubmittedAtUtc.Count > 0 &&
                   (utcNow - m_SubmittedAtUtc.Peek()).TotalMilliseconds >= Delays.SubmissionWindowMs)
            {
                m_SubmittedAtUtc.Dequeue();
            }
        }
    }
}

internal sealed record PilotSwitchResult(
    int TargetPilotIndex,
    bool Succeeded,
    string? CapturePath);

internal sealed record AutomationSummary(
    ScreenCaptureAnalysisSummary CaptureSummary,
    int ClickedPointCount,
    Rect? ControlButtonBounds,
    string FocusedCapturePath,
    bool MaximumSubmissionsReached = false,
    int CurrentPilotIndex = 1,
    int? TargetPilotIndex = null,
    bool PilotSwitchSucceeded = false,
    string? PilotSwitchCapturePath = null,
    bool NoFurtherPilotsAvailable = false,
    bool PlayfieldMissingLimitReached = false,
    bool RestartFromLauncherRequested = false,
    bool SlowDownPopupDetected = false,
    bool ConnectionLostDetected = false,
    bool PopupDetectionAmbiguous = false);
