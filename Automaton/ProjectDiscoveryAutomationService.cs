using System.IO;
using System.Windows;
using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using OpenCvSharp;
using Serilog;
using Point = OpenCvSharp.Point;
using Rect = OpenCvSharp.Rect;

namespace Automaton;

internal sealed class ProjectDiscoveryAutomationService(
    ScreenCaptureService screenCaptureService,
    IAutomationInputController automationInputController,
    IAutomationClock automationClock,
    ErrorPopupDetector errorPopupDetector,
    PlayNowButtonLocator playNowButtonLocator)
{
    private const int MaximumSubmissionsPerWindow = 5;
    private const int MaximumConsecutivePlayfieldMisses = 5;
    private const int InitialPilotIndex = 1;
    private const string NoPlayButtonFoundDebugText = "No play button found";
    private const string PilotNotFoundDebugTextTemplate = "Pilot {0} not found";
    private const double DebugOverlayTextScale = 0.8;
    private const int DebugOverlayTextThickness = 2;
    private const int DebugOverlayLeftPadding = 30;
    private const int DebugOverlayTopPadding = 40;
    private static readonly Rect ControlButtonBounds = new(930, 645, 271, 11);
    private static readonly Scalar DebugOverlayTextColor = new(80, 120, 255);
    private static readonly ILogger Logger = Log.ForContext<ProjectDiscoveryAutomationService>();

    private readonly AutomationSubmitRateLimiter m_SubmitRateLimiter = new();
    private readonly Random m_Random = new();
    private int m_CurrentPilotIndex = InitialPilotIndex;

    internal bool KeepDebugImages { get; set; } = true;

    public void ProcessSamples()
    {
        Logger.Information("Processing samples through automation service.");
        screenCaptureService.ProcessSamples();
    }

    internal StartupAutomationSummary PrepareAutomationFromLauncherStartup(
        int initialPilotIndex,
        CancellationToken cancellationToken)
    {
        using var traceImages = CreateTraceImageScope();
        Logger.Information("Preparing launcher startup automation. InitialPilotIndex={InitialPilotIndex}", initialPilotIndex);
        var playButtonCapturePath = screenCaptureService.CaptureCurrentScreenTrace(".play");
        traceImages.Track(playButtonCapturePath);
        cancellationToken.ThrowIfCancellationRequested();

        using var playButtonScreen = Cv2.ImRead(playButtonCapturePath);
        if (!playNowButtonLocator.TryLocate(playButtonScreen, out var playButtonLocation))
        {
            DrawDebugOverlay(playButtonCapturePath, NoPlayButtonFoundDebugText);
            Logger.Error("No play button found during startup automation. CapturePath={CapturePath}", playButtonCapturePath);
            return new StartupAutomationSummary(
                playButtonCapturePath,
                false);
        }

        Logger.Information("Play button found during startup automation. CapturePath={CapturePath}, Bounds={Bounds}", playButtonCapturePath, playButtonLocation.Bounds);
        automationInputController.MoveTo(GeometryHelper.Center(playButtonLocation.Bounds));
        automationInputController.LeftClick(cancellationToken);
        automationInputController.Delay(Delays.ProjectDiscoveryLauncherStartupMs, cancellationToken);
        automationInputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.W, cancellationToken);

        var pilotSelectionCapturePath = screenCaptureService.CaptureCurrentScreenTrace($".startup-pilot-{initialPilotIndex}");
        traceImages.Track(pilotSelectionCapturePath);
        cancellationToken.ThrowIfCancellationRequested();
        using var pilotSelectionScreen = Cv2.ImRead(pilotSelectionCapturePath);
        if (!PilotAvatarLocator.TryLocate(pilotSelectionScreen, initialPilotIndex, out var pilotLocation))
        {
            DrawPilotNotFoundDebugOverlay(pilotSelectionCapturePath, initialPilotIndex);
            Logger.Error("Pilot was not found during startup automation. PilotIndex={PilotIndex}, CapturePath={CapturePath}", initialPilotIndex, pilotSelectionCapturePath);
            return new StartupAutomationSummary(
                playButtonCapturePath,
                true,
                pilotSelectionCapturePath);
        }

        Logger.Information("Pilot found during startup automation. PilotIndex={PilotIndex}, CapturePath={CapturePath}, Bounds={Bounds}", initialPilotIndex, pilotSelectionCapturePath, pilotLocation.Bounds);
        automationInputController.MoveTo(GeometryHelper.Center(pilotLocation.Bounds));
        automationInputController.LeftClick(cancellationToken);
        automationInputController.Delay(Delays.ProjectDiscoveryLauncherStartupMs, cancellationToken);
        m_CurrentPilotIndex = initialPilotIndex;

        // Hide GUI
        automationInputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.Shift, VirtualKeys.F9, cancellationToken);
        automationInputController.PressKeyChord(VirtualKeys.Alt, VirtualKeys.L, cancellationToken);

        return new StartupAutomationSummary(playButtonCapturePath, true, pilotSelectionCapturePath, true, pilotLocation.Bounds, true);
    }

    public AutomationSummary AutomateCurrentScreen(DpiScale dpi, CancellationToken cancellationToken)
    {
        return AutomateCurrentScreen(dpi, InitialPilotIndex, cancellationToken);
    }

    public AutomationSummary AutomateCurrentScreen(
        DpiScale dpi,
        int initialPilotIndex,
        CancellationToken cancellationToken)
    {
        Logger.Information("Automation loop starting. InitialPilotIndex={InitialPilotIndex}", initialPilotIndex);
        automationInputController.Delay(Delays.AutomationStartupDelayMs, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        AutomationSummary? lastSummary = null;
        m_CurrentPilotIndex = initialPilotIndex;
        var consecutivePlayfieldMisses = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var traceImages = CreateTraceImageScope();
                var captureSummary = screenCaptureService.CaptureAndAnalyzeCurrentScreen();
                traceImages.Track(captureSummary);
                cancellationToken.ThrowIfCancellationRequested();

                var popupState = DetectPopupStateWithLauncherGuard(captureSummary.CapturePath);
                if (TryHandleDetectedPopup(
                    popupState,
                    captureSummary,
                    traceImages,
                    clickedPointCount: 0,
                    controlBounds: null,
                    focusedCapturePath: captureSummary.CapturePath,
                    detectionStage: "main capture",
                    cancellationToken,
                    out var popupSummary))
                {
                    lastSummary = popupSummary;
                    switch (lastSummary)
                    {
                        case { SlowDownPopupDetected: true, MaximumSubmissionsReached: false, ConnectionLostDetected: false, PopupDetectionAmbiguous: false }:
                            consecutivePlayfieldMisses = 0;
                            automationInputController.Delay(Delays.MinimumClickMs, cancellationToken);
                            continue;
                        case { MaximumSubmissionsReached: true, PilotSwitchSucceeded: true }:
                            automationInputController.Delay(Delays.MinimumClickMs, cancellationToken);
                            continue;
                        default:
                            return lastSummary;
                    }
                }

                if (captureSummary.Analysis.Result.PlayfieldFound)
                {
                    consecutivePlayfieldMisses = 0;
                }
                else
                {
                    consecutivePlayfieldMisses++;
                    Logger.Warning(
                        "Playfield was not found during automation. CapturePath={CapturePath}, ConsecutivePlayfieldMisses={ConsecutivePlayfieldMisses}, MaximumConsecutivePlayfieldMisses={MaximumConsecutivePlayfieldMisses}",
                        captureSummary.CapturePath,
                        consecutivePlayfieldMisses,
                        MaximumConsecutivePlayfieldMisses);

                    if (consecutivePlayfieldMisses >= MaximumConsecutivePlayfieldMisses)
                    {
                        Logger.Error(
                            "Automation loop requested launcher restart because the playfield was not found repeatedly. CapturePath={CapturePath}, ConsecutivePlayfieldMisses={ConsecutivePlayfieldMisses}, CurrentPilotIndex={CurrentPilotIndex}",
                            captureSummary.CapturePath,
                            consecutivePlayfieldMisses,
                            m_CurrentPilotIndex);

                        automationInputController.QuitGame(cancellationToken);
                        return new AutomationSummary(
                            captureSummary,
                            0,
                            null,
                            string.Empty,
                            CurrentPilotIndex: m_CurrentPilotIndex,
                            PlayfieldMissingLimitReached: true,
                            RestartFromLauncherRequested: true);
                    }
                }

                lastSummary = AutomateSingleCycle(dpi, m_SubmitRateLimiter, captureSummary, traceImages, cancellationToken);
                switch (lastSummary)
                {
                    case { ConnectionLostDetected: true } or { PopupDetectionAmbiguous: true }:
                        return lastSummary;
                    case { MaximumSubmissionsReached: true, PilotSwitchSucceeded: false }:
                        Logger.Warning("Automation loop stopped because maximum submissions were reached and pilot switching did not succeed. CurrentPilotIndex={CurrentPilotIndex}, TargetPilotIndex={TargetPilotIndex}", lastSummary.CurrentPilotIndex, lastSummary.TargetPilotIndex);
                        return lastSummary;
                    default:
                        automationInputController.Delay(Delays.MinimumClickMs, cancellationToken);
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (lastSummary is not null)
        {
            Logger.Information("Automation loop canceled after a completed cycle. CapturePath={CapturePath}, CurrentPilotIndex={CurrentPilotIndex}", lastSummary.CaptureSummary.CapturePath, lastSummary.CurrentPilotIndex);
            return lastSummary;
        }

        return lastSummary ?? throw new OperationCanceledException(cancellationToken);
    }

    private AutomationSummary AutomateSingleCycle(
        DpiScale dpi,
        AutomationSubmitRateLimiter rateLimiter,
        ScreenCaptureAnalysisSummary captureSummary,
        TraceImageScope traceImages,
        CancellationToken cancellationToken)
    {
        var clickedPointCount = ClickPolygonPoints(captureSummary.Analysis.Polygons, cancellationToken);
        Logger.Information("Automation cycle analyzed screen. CapturePath={CapturePath}, PlayfieldFound={PlayfieldFound}, ClusterCount={ClusterCount}, ClickedPointCount={ClickedPointCount}", captureSummary.CapturePath, captureSummary.Analysis.Result.PlayfieldFound, captureSummary.Analysis.Result.ClusterCount, clickedPointCount);

        automationInputController.Delay(Delays.MinimumClickMs, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        // Focus the known safe control button area.
        FocusControlButton(ControlButtonBounds, dpi, cancellationToken);

        DelayBeforeRateLimitedSubmit(rateLimiter, cancellationToken);

        // Left-click the 'Submit' button.
        automationInputController.LeftClick(cancellationToken);
        rateLimiter.RecordSubmit(automationClock.UtcNow);
        automationInputController.Delay(Delays.MinimumClickMs, cancellationToken);
        var focusedCapturePath = CaptureFocusedScreenTrace(captureSummary, cancellationToken);
        traceImages.Track(focusedCapturePath);
        var focusedPopupState = DetectPopupStateWithLauncherGuard(focusedCapturePath);
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
            m_CurrentPilotIndex);

        return new AutomationSummary(
            captureSummary,
            clickedPointCount,
            ControlButtonBounds,
            focusedCapturePath,
            CurrentPilotIndex: m_CurrentPilotIndex);
    }

    private PilotSwitchResult SwitchToNextPilot(
        ScreenCaptureAnalysisSummary captureSummary,
        TraceImageScope traceImages,
        CancellationToken cancellationToken)
    {
        if (!PilotAvatarLocator.TryGetNextPilotIndex(m_CurrentPilotIndex, out var nextPilotIndex))
        {
            Logger.Warning("No next pilot is configured => Quit Game. CurrentPilotIndex={CurrentPilotIndex}", m_CurrentPilotIndex);
            // Quit Game
            automationInputController.QuitGame(cancellationToken);
            return new PilotSwitchResult(m_CurrentPilotIndex, Succeeded: false, null);
        }

        Logger.Information("Switching pilot. CurrentPilotIndex={CurrentPilotIndex}, TargetPilotIndex={TargetPilotIndex}", m_CurrentPilotIndex, nextPilotIndex);
        automationInputController.Logout(cancellationToken);

        // Wait for full logout
        automationInputController.Delay(Delays.PilotLogoutMs, cancellationToken);

        // Close any window on login screen
        automationInputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.W, cancellationToken);

        // Make screenshot of pilots on login screen
        var pilotSelectionCapturePath = CapturePilotSelectionScreenTrace(captureSummary, nextPilotIndex, cancellationToken);
        traceImages.Track(pilotSelectionCapturePath);
        using var pilotSelectionScreen = Cv2.ImRead(pilotSelectionCapturePath);

        // Locate next pilot
        if (!PilotAvatarLocator.TryLocate(pilotSelectionScreen, nextPilotIndex, out var location))
        {
            // Failed to locate requested pilot
            DrawPilotNotFoundDebugOverlay(pilotSelectionCapturePath, nextPilotIndex);
            
            Logger.Warning("Target pilot was not found. TargetPilotIndex={TargetPilotIndex}, CapturePath={CapturePath}", nextPilotIndex, pilotSelectionCapturePath);
            return new PilotSwitchResult(nextPilotIndex, Succeeded: false, pilotSelectionCapturePath);
        }

        // Login requested pilot
        automationInputController.MoveTo(GeometryHelper.Center(location.Bounds));
        automationInputController.LeftClick(cancellationToken);
        automationInputController.Delay(Delays.ProjectDiscoveryPilotLoginMs, cancellationToken);
        m_CurrentPilotIndex = nextPilotIndex;

        // Close any window after login
        automationInputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.W, cancellationToken);

        // Activate Project Discovery window
        automationInputController.Delay(Delays.MinimumClickMs, cancellationToken);
        automationInputController.PressKeyChord(VirtualKeys.Alt, VirtualKeys.L, cancellationToken);

        Logger.Information("Pilot switch succeeded. CurrentPilotIndex={CurrentPilotIndex}, CapturePath={CapturePath}, Bounds={Bounds}", m_CurrentPilotIndex, pilotSelectionCapturePath, location.Bounds);
        return new PilotSwitchResult(nextPilotIndex, Succeeded: true, pilotSelectionCapturePath);
    }

    private bool TryHandleDetectedPopup(
        ErrorPopupDetector.PopupState popupState,
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
            case ErrorPopupDetector.PopupState.None:
                summary = null!;
                return false;
            case ErrorPopupDetector.PopupState.ConnectionLost:
                Logger.Error("Connection Lost popup detected during {DetectionStage}. Stopping automation. FocusedCapturePath={FocusedCapturePath}", detectionStage, focusedCapturePath);
                automationInputController.Delay(Delays.ConnectionLostExitMs, cancellationToken);
                automationInputController.PressKey(VirtualKeys.Enter, cancellationToken);
                summary = new AutomationSummary(
                    captureSummary,
                    clickedPointCount,
                    controlBounds,
                    focusedCapturePath,
                    CurrentPilotIndex: m_CurrentPilotIndex,
                    ConnectionLostDetected: true);
                return true;
            case ErrorPopupDetector.PopupState.SlowDown:
                RecoverFromSlowDownPopup(focusedCapturePath, cancellationToken);
                summary = new AutomationSummary(
                    captureSummary,
                    clickedPointCount,
                    controlBounds,
                    focusedCapturePath,
                    CurrentPilotIndex: m_CurrentPilotIndex,
                    SlowDownPopupDetected: true);
                return true;
            case ErrorPopupDetector.PopupState.MaximumSubmissions:
                var pilotSwitchResult = SwitchToNextPilot(captureSummary, traceImages, cancellationToken);
                Logger.Warning("Maximum submissions popup detected during {DetectionStage}. FocusedCapturePath={FocusedCapturePath}, CurrentPilotIndex={CurrentPilotIndex}, TargetPilotIndex={TargetPilotIndex}, PilotSwitchSucceeded={PilotSwitchSucceeded}, PilotSwitchCapturePath={PilotSwitchCapturePath}", detectionStage, focusedCapturePath, m_CurrentPilotIndex, pilotSwitchResult.TargetPilotIndex, pilotSwitchResult.Succeeded, pilotSwitchResult.CapturePath);
                summary = new AutomationSummary(
                    captureSummary,
                    clickedPointCount,
                    controlBounds,
                    focusedCapturePath,
                    MaximumSubmissionsReached: true,
                    CurrentPilotIndex: m_CurrentPilotIndex,
                    TargetPilotIndex: pilotSwitchResult.TargetPilotIndex,
                    PilotSwitchSucceeded: pilotSwitchResult.Succeeded,
                    PilotSwitchCapturePath: pilotSwitchResult.CapturePath,
                    NoFurtherPilotsAvailable: !pilotSwitchResult.Succeeded &&
                                              pilotSwitchResult.TargetPilotIndex == m_CurrentPilotIndex);
                return true;
            case ErrorPopupDetector.PopupState.Unknown:
                Logger.Error("Ambiguous popup detected during {DetectionStage}. Stopping automation for safety. FocusedCapturePath={FocusedCapturePath}", detectionStage, focusedCapturePath);
                summary = new AutomationSummary(
                    captureSummary,
                    clickedPointCount,
                    controlBounds,
                    focusedCapturePath,
                    CurrentPilotIndex: m_CurrentPilotIndex,
                    PopupDetectionAmbiguous: true);
                return true;
            default:
                summary = null!;
                return false;
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
        automationInputController.PressKeyChord(VirtualKeys.Alt, VirtualKeys.L, cancellationToken);
    }

    private ErrorPopupDetector.PopupState DetectPopupStateWithLauncherGuard(string capturePath)
    {
        using var image = Cv2.ImRead(capturePath);
        if (image.Empty() || !playNowButtonLocator.TryLocate(image, out var playButtonLocation))
        {
            return errorPopupDetector.DetectPopupStateAndDrawDebugOverlay(capturePath);
        }

        Logger.Information(
            "Skipping popup detection because launcher PLAY NOW button was found. CapturePath={CapturePath}, PlayButtonBounds={PlayButtonBounds}",
            capturePath,
            playButtonLocation.Bounds);

        return ErrorPopupDetector.PopupState.None;
    }

    private TraceImageScope CreateTraceImageScope()
    {
        return new TraceImageScope(KeepDebugImages);
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

    private void FocusControlButton(Rect controlButtonBounds, DpiScale dpi, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var anchor = new Point(
            m_Random.Next(controlButtonBounds.X, controlButtonBounds.Right),
            m_Random.Next(controlButtonBounds.Y, controlButtonBounds.Bottom));
        var scaledAnchor = ScalePointForDpi(anchor, dpi);

        automationInputController.MoveTo(scaledAnchor);
        automationInputController.Delay(Delays.HoverMs, cancellationToken);
    }

    private string CaptureFocusedScreenTrace(ScreenCaptureAnalysisSummary captureSummary, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var focusedCapturePath = Path.Combine(
            captureSummary.CapturesDirectory,
            $"{Path.GetFileNameWithoutExtension(captureSummary.CapturePath)}.focused.png");
        screenCaptureService.CaptureCurrentScreenToFile(focusedCapturePath);
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
        screenCaptureService.CaptureCurrentScreenToFile(pilotSelectionCapturePath);
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
            DebugOverlayTextColor,
            DebugOverlayTextThickness,
            LineTypes.AntiAlias);
        Cv2.ImWrite(imagePath, image);
    }

    internal static Point ScalePointForDpi(Point point, DpiScale dpi)
    {
        return new Point(
            (int)Math.Round(point.X * dpi.DpiScaleX, MidpointRounding.AwayFromZero),
            (int)Math.Round(point.Y * dpi.DpiScaleY, MidpointRounding.AwayFromZero));
    }

    private sealed class TraceImageScope(bool keepImages) : IDisposable
    {
        private readonly HashSet<string> m_ImagePaths = new(StringComparer.OrdinalIgnoreCase);

        public void Track(ScreenCaptureAnalysisSummary captureSummary)
        {
            Track(captureSummary.CapturePath);
            Track(captureSummary.Analysis.Result.OutputPath);
        }

        public void Track(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return;
            }

            m_ImagePaths.Add(Path.GetFullPath(imagePath));
        }

        public void Dispose()
        {
            if (keepImages)
            {
                return;
            }

            foreach (var imagePath in m_ImagePaths)
            {
                DeleteImageFile(imagePath);
            }
        }

        private static void DeleteImageFile(string imagePath)
        {
            try
            {
                if (!File.Exists(imagePath))
                {
                    return;
                }

                File.Delete(imagePath);
                Logger.Debug("Deleted trace image. ImagePath={ImagePath}", imagePath);
            }
            catch (Exception exception)
            {
                Logger.Warning(exception, "Could not delete trace image. ImagePath={ImagePath}", imagePath);
            }
        }
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

internal sealed record StartupAutomationSummary(
    string PlayButtonCapturePath,
    bool PlayButtonFound,
    string? PilotCapturePath = null,
    bool PilotLocated = false,
    Rect? PilotBounds = null,
    bool ShouldStartAutomation = false);

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
