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
    IAutomationClock automationClock,
    ControlButtonDetector controlButtonDetector,
    MaxSubmissionsPopupDetector maxSubmissionsPopupDetector,
    SlowDownPopupDetector slowDownPopupDetector) : IProjectDiscoveryAutomationState
{
    private const int MaximumConsecutivePlayfieldMisses = 5;
    private const int MaximumSubmissionsPerWindow = 5;
    private static readonly OverlayColor ControlButtonSearchOverlayColor = OverlayColor.Yellow;
    private static readonly OverlayColor ControlButtonMatchOverlayColor = OverlayColor.Cyan;
    private readonly Queue<DateTime> m_SubmittedAtUtc = new();
    private readonly ILogger m_Logger = Log.ForContext<DiscoverState>();
    public DiscoveryAutomationStateKind Kind => DiscoveryAutomationStateKind.Discover;

    public DiscoveryAutomationStateTransition Execute(ProjectDiscoveryAutomationContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.LastAction == DiscoveryAutomationActionKind.LoginPilot)
        {
            m_Logger.Information("Activate Discovery Project window after login");
            automationInputController.PressKeyChord(VirtualKeys.Alt, VirtualKeys.L, cancellationToken);
            automationInputController.Delay(Delays.LoadWindowMs, cancellationToken);
        }

        using var traceImages = new TraceImageScope(context.KeepDebugImages);
        var captureSummary = screenCaptureService.CaptureAndAnalyzeCurrentScreen();
        traceImages.Track(captureSummary);

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
                automationInputController.QuitGame(cancellationToken);
                return new DiscoveryAutomationStateTransition(
                    Kind,
                    DiscoveryAutomationStateKind.StartingGame,
                    DiscoveryAutomationActionKind.StartGame,
                    captureSummary.CapturePath);
            }
        }

        // Polygons
        ClickPolygonPoints(captureSummary.Analysis.Polygons, cancellationToken);
        automationInputController.Delay(Delays.SubmitActivationMs, cancellationToken);

        var playfieldBounds = captureSummary.Analysis.PlayfieldDetection.Bounds;
        using var postPolygonCapture = screenCaptureService.CaptureCurrentScreen(".discovery-post-polygons");
        traceImages.Track(postPolygonCapture.CapturePath);

        // Try to detect enabled Submit button after outlining polygons
        var enabledButtonDetection = controlButtonDetector.Detect(postPolygonCapture.Image, playfieldBounds, ControlButtonState.Enabled);
        DrawControlButtonDetectionOverlay(postPolygonCapture.CapturePath, enabledButtonDetection);

        // Disabled Submit button most probably means overlapping polygons.
        if (!enabledButtonDetection.IsFound || enabledButtonDetection.ButtonBounds is null)
        {
            m_Logger.Warning("Enabled submit button was not detected after polygon clicking. Transitioning to overlap recovery");
            return new DiscoveryAutomationStateTransition(
                Kind,
                DiscoveryAutomationStateKind.RecoverOverlap,
                DiscoveryAutomationActionKind.RecoverOverlap,
                captureSummary.CapturePath);
        }

        // Focus button area.
        FocusControlButton(enabledButtonDetection.ButtonBounds.Value, cancellationToken);

        // Wait before Submit click if we are too fast.
        DelayBeforeRateLimitedSubmit(cancellationToken);

        // Left-click the 'Submit' button.
        automationInputController.LeftClick(cancellationToken);
        RecordSubmit(automationClock.UtcNow);
        automationInputController.Delay(Delays.SubmitResultMs, cancellationToken);

        // Take focused screen to trace the result of submission.
        var focusedCapturePath = CaptureFocusedScreenTrace(captureSummary, cancellationToken);
        traceImages.Track(focusedCapturePath);

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
                automationInputController.LeftClick(cancellationToken);
                automationInputController.Delay(Delays.MinimumClickMs, cancellationToken);
            }

            automationInputController.MoveTo(polygon[0]);
            automationInputController.LeftClick(cancellationToken);
            automationInputController.Delay(Delays.MinimumClickMs, cancellationToken);
        }
    }

    private void FocusControlButton(Rect controlButtonBounds, CancellationToken cancellationToken)
    {
        var anchor = GeometryHelper.Center(controlButtonBounds);
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
        var delay = GetDelayBeforeNextSubmit(automationClock.UtcNow);
        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        m_Logger.Information("Waiting before submit because of rate limit. DelayMilliseconds={DelayMilliseconds}", (int)Math.Ceiling(delay.TotalMilliseconds));
        automationInputController.Delay((int)Math.Ceiling(delay.TotalMilliseconds), cancellationToken);
    }

    private TimeSpan GetDelayBeforeNextSubmit(DateTime utcNow)
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

    private void RecordSubmit(DateTime utcNow)
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

    private static void DrawControlButtonDetectionOverlay(string annotatedImagePath, ControlButtonDetection detection)
    {
        if (string.IsNullOrWhiteSpace(annotatedImagePath))
        {
            return;
        }

        using var annotated = Cv2.ImRead(annotatedImagePath);
        if (annotated.Empty())
        {
            return;
        }

        if (detection.SearchBounds is { Width: > 0, Height: > 0 })
        {
            DebugOverlay.Annotate(annotated, (detection.SearchBounds, ControlButtonSearchOverlayColor));
        }

        if (detection.ButtonBounds is not null)
        {
            DebugOverlay.Annotate(annotated, (detection.ButtonBounds.Value, ControlButtonMatchOverlayColor));
        }

        Cv2.PutText(
            annotated,
            $"{detection.TargetState} score={detection.Score:0.000} hsvD={detection.DisabledHsvDistance:0.00}",
            new Point(detection.SearchBounds.X, Math.Max(25, detection.SearchBounds.Y - 10)),
            HersheyFonts.HersheySimplex,
            0.55,
            ControlButtonSearchOverlayColor.ToScalar(),
            2,
            LineTypes.AntiAlias);
        Cv2.ImWrite(annotatedImagePath, annotated);
    }
}
