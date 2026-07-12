using System.IO;
using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using OpenCvSharp;
using Serilog;
using Point = OpenCvSharp.Point;
using Rect = OpenCvSharp.Rect;

namespace Automaton.ProjectDiscoveryStates;

internal sealed class DiscoverState(
    ScreenCaptureService screenCaptureService,
    IAutomationInputController automationInputController,
    ClickTraceRecorder clickTraceRecorder,
    IGameActionService gameActionService,
    IAutomationClock automationClock,
    MaxSubmissionsPopupDetector maxSubmissionsPopupDetector,
    SlowDownPopupDetector slowDownPopupDetector,
    DowntimeDetector downtimeDetector,
    AccuracyDetector accuracyDetector) : IProjectDiscoveryAutomationState
{
    private const int MaximumConsecutivePlayfieldMisses = 5;
    private const int MaximumSubmissionsPerWindow = 5;
    private static readonly OverlayColor EnabledButtonSearchOverlayColor = OverlayColor.Yellow;
    private readonly Queue<DateTime> m_SubmittedAtLocal = new();
    private readonly ILogger m_Logger = Log.ForContext<DiscoverState>();
    public DiscoveryAutomationStateKind Kind => DiscoveryAutomationStateKind.Discover;

    public DiscoveryAutomationStateTransition Execute(ProjectDiscoveryAutomationContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.LastAction == DiscoveryAutomationActionKind.LoginPilot)
        {
            m_Logger.Information("Activate Discovery Project window after login");
            gameActionService.ToggleProjectDiscoveryWindow(cancellationToken);
            automationInputController.Delay(Delays.LoadWindowMs, cancellationToken);
        }

        var captureSummary = screenCaptureService.CaptureAndAnalyzeCurrentScreen();

        if (captureSummary.Analysis.Result.PlayfieldFound)
        {
            context.ConsecutivePlayfieldMisses = 0;
        }
        else
        {
            context.ConsecutivePlayfieldMisses++;

            // Restart Game if playfield was not found for five times in a row
            if (context.ConsecutivePlayfieldMisses >= MaximumConsecutivePlayfieldMisses)
            {
                m_Logger.Error("Playfield was not found for {Times} times in a row => Restarting", MaximumConsecutivePlayfieldMisses);
                gameActionService.QuitGame(cancellationToken);
                return new DiscoveryAutomationStateTransition(
                    Kind,
                    DiscoveryAutomationStateKind.StartingGame,
                    DiscoveryAutomationActionKind.StartGame,
                    captureSummary.CapturePath);
            }
        }

        LogDetectedAccuracy(captureSummary.CapturePath, context.CurrentPilotIndex);

        // Polygons
        using (clickTraceRecorder.SuppressRecording())
        {
            ClickPolygonPoints(captureSummary.Analysis.Polygons, cancellationToken);
        }

        automationInputController.Delay(Delays.SubmitActivationMs, cancellationToken);

        var playfieldBounds = captureSummary.Analysis.PlayfieldDetection.Bounds;
        using var postPolygonCapture = screenCaptureService.CaptureCurrentScreenInMemory(".discovery-post-polygons");
        var enabledButtonDetection = EnabledButtonDetector.Detect(postPolygonCapture.Image, playfieldBounds);

        // Disabled Submit button most probably means overlapping polygons.
        if (!enabledButtonDetection.IsFound || enabledButtonDetection.ButtonBounds is null)
        {
            DrawEnabledButtonSearchOverlay(postPolygonCapture.Image, enabledButtonDetection);
            screenCaptureService.SaveCapture(postPolygonCapture);
            m_Logger.Warning("Enabled submit button was not detected after polygon clicking. Transitioning to overlap recovery");
            return new DiscoveryAutomationStateTransition(
                Kind,
                DiscoveryAutomationStateKind.RecoverOverlap,
                DiscoveryAutomationActionKind.RecoverOverlap,
                captureSummary.CapturePath);
        }

        FocusEnabledButton(enabledButtonDetection.ButtonBounds.Value, cancellationToken);

        // Wait before Submit click if we are too fast.
        DelayBeforeRateLimitedSubmit(cancellationToken);

        // Left-click the 'Submit' button.
        automationInputController.LeftClick(cancellationToken);
        RecordSubmit(automationClock.LocalNow);
        automationInputController.Delay(Delays.SubmitResultMs, cancellationToken);

        // Take focused screen to trace the result of submission.
        var focusedCapturePath = CaptureFocusedScreenTrace(captureSummary, cancellationToken);

        // Try to detect MaxSubmissions popup first.
        using var focusedImage = Cv2.ImRead(focusedCapturePath);
        var maxSubmissionsPopupDetection = maxSubmissionsPopupDetector.Detect(focusedImage);
        if (maxSubmissionsPopupDetection.State == PopupState.MaxSubmissions)
        {
            DrawPopupDebugOverlay(focusedCapturePath, maxSubmissionsPopupDetection, "Maximum submissions popup detected");
            return new DiscoveryAutomationStateTransition(
                Kind,
                DiscoveryAutomationStateKind.RecoverMaxSubmissionsPopup,
                DiscoveryAutomationActionKind.RecoverMaxSubmissionsPopup,
                captureSummary.CapturePath);
        }

        // Now try to detect SlowDown popup.
        var slowDownPopupDetection = slowDownPopupDetector.Detect(focusedImage);
        if (slowDownPopupDetection.State == PopupState.SlowDown)
        {
            DrawPopupDebugOverlay(focusedCapturePath, slowDownPopupDetection, "Slow down popup detected");
            return new DiscoveryAutomationStateTransition(
                Kind,
                DiscoveryAutomationStateKind.RecoverSlowDownPopup,
                DiscoveryAutomationActionKind.RecoverSlowDownPopup,
                captureSummary.CapturePath);
        }

        // Left-click the 'Continue' button.
        automationInputController.LeftClick(cancellationToken);
        automationInputController.Delay(Delays.MinimumClickMs, cancellationToken);

        // Left-click the next 'Continue' button.
        automationInputController.LeftClick(cancellationToken);

        var utcNow = automationClock.UtcNow;
        if (downtimeDetector.IsDowntimeImminent(utcNow))
        {
            m_Logger.Warning("Downtime imminent => quit game and request operating system shutdown. LocalNow={LocalNow}", automationClock.LocalNow);
            gameActionService.QuitGame(cancellationToken);
            return new DiscoveryAutomationStateTransition(
                Kind,
                DiscoveryAutomationStateKind.Recovery,
                DiscoveryAutomationActionKind.Shutdown,
                captureSummary.CapturePath);
        }

        return new DiscoveryAutomationStateTransition(
            Kind,
            Kind,
            DiscoveryAutomationActionKind.DiscoverAndSubmit,
            captureSummary.CapturePath);
    }

    private void ClickPolygonPoints(IReadOnlyList<Point[]> polygons, CancellationToken cancellationToken)
    {
        foreach (var polygon in polygons)
        {
            if (polygon.Length == 0)
            {
                continue;
            }

            foreach (var point in polygon)
            {
                automationInputController.MoveTo(point);
                automationInputController.LeftClick(cancellationToken, recordClick: false);
                automationInputController.Delay(Delays.MinimumClickMs, cancellationToken);
            }

            automationInputController.MoveTo(polygon[0]);
            automationInputController.LeftClick(cancellationToken, recordClick: false);
            automationInputController.Delay(Delays.MinimumClickMs, cancellationToken);
        }
    }

    private void LogDetectedAccuracy(string capturePath, int pilotIndex)
    {
        using var image = Cv2.ImRead(capturePath);
        var detection = accuracyDetector.Detect(image);
        if (detection.IsFound)
        {
            m_Logger.Information("Pilot {PilotIndex} accuracy = {Accuracy}", pilotIndex, detection.Text);
        }
    }

    private void FocusEnabledButton(Rect buttonBounds, CancellationToken cancellationToken)
    {
        var anchor = GeometryHelper.Center(buttonBounds);
        automationInputController.MoveTo(anchor);
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

    private void DelayBeforeRateLimitedSubmit(CancellationToken cancellationToken)
    {
        var delay = GetDelayBeforeNextSubmit(automationClock.LocalNow);
        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        m_Logger.Information("Waiting before submit because of rate limit. DelayMilliseconds={DelayMilliseconds}", (int)Math.Ceiling(delay.TotalMilliseconds));
        automationInputController.Delay((int)Math.Ceiling(delay.TotalMilliseconds), cancellationToken);
    }

    private TimeSpan GetDelayBeforeNextSubmit(DateTime localNow)
    {
        RemoveExpiredSubmissions(localNow);
        if (m_SubmittedAtLocal.Count < MaximumSubmissionsPerWindow)
        {
            return TimeSpan.Zero;
        }

        var elapsed = localNow - m_SubmittedAtLocal.Peek();
        var remaining = TimeSpan.FromMilliseconds(Delays.SubmissionWindowMs) - elapsed;
        return remaining <= TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    private void RecordSubmit(DateTime localNow)
    {
        RemoveExpiredSubmissions(localNow);
        m_SubmittedAtLocal.Enqueue(localNow);
    }

    private void RemoveExpiredSubmissions(DateTime localNow)
    {
        while (m_SubmittedAtLocal.Count > 0 &&
               (localNow - m_SubmittedAtLocal.Peek()).TotalMilliseconds >= Delays.SubmissionWindowMs)
        {
            m_SubmittedAtLocal.Dequeue();
        }
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

    private static void DrawEnabledButtonSearchOverlay(Mat image, EnabledButtonDetection detection)
    {
        if (image.Empty() || detection.SearchBounds is not { Width: > 0, Height: > 0 })
        {
            return;
        }

        DebugOverlay.Annotate(image, (detection.SearchBounds, EnabledButtonSearchOverlayColor));
        Cv2.PutText(
            image,
            $"score={detection.Score:0.000} hsvD={detection.HsvDistance:0.00}",
            new Point(detection.SearchBounds.X, Math.Max(25, detection.SearchBounds.Y - 10)),
            HersheyFonts.HersheySimplex,
            0.55,
            EnabledButtonSearchOverlayColor.ToScalar(),
            2,
            LineTypes.AntiAlias);
    }
}
