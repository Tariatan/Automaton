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
    MaxSubmissionsPopupDetector maxSubmissionsPopupDetector,
    SlowDownPopupDetector slowDownPopupDetector) : IProjectDiscoveryAutomationState
{
    private const int MaximumConsecutivePlayfieldMisses = 5;
    private const int MaximumSubmissionsPerWindow = 5;
    private static readonly Rect ControlButtonBounds = new(1360, 960, 460, 30);
    private static readonly Scalar ControlButtonOverlayColor = new(0, 255, 255);
    private readonly Random m_Random = new();
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
        DrawControlButtonBoundsOverlay(captureSummary.Analysis.Result.OutputPath);

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
        automationInputController.Delay(Delays.MinimumClickMs, cancellationToken);

        // Focus button area.
        FocusControlButton(cancellationToken);

        // Wait before Submit click if we are too fast
        DelayBeforeRateLimitedSubmit(cancellationToken);

        // Left-click the 'Submit' button.
        automationInputController.LeftClick(cancellationToken);
        RecordSubmit(automationClock.UtcNow);
        automationInputController.Delay(Delays.MinimumClickMs, cancellationToken);

        // Take focused screen to trace the result of submission
        var focusedCapturePath = CaptureFocusedScreenTrace(captureSummary, cancellationToken);
        traceImages.Track(focusedCapturePath);

        // Try to detect MaxSubmissions/SlowDown popup.
        // Transition to corresponding Recover*PopupSate
        var maxSubmissionsPopupDetection = maxSubmissionsPopupDetector.Detect(focusedCapturePath);
        if (maxSubmissionsPopupDetection.State == PopupState.MaxSubmissions)
        {
            return new DiscoveryAutomationStateTransition(
                Kind,
                DiscoveryAutomationStateKind.RecoverMaxSubmissionsPopup,
                DiscoveryAutomationActionKind.RecoverMaxSubmissionsPopup,
                captureSummary.CapturePath);
        }

        var slowDownPopupDetection = slowDownPopupDetector.Detect(focusedCapturePath);
        if (slowDownPopupDetection.State == PopupState.SlowDown)
        {
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

    private void FocusControlButton(CancellationToken cancellationToken)
    {
        var anchor = new Point(
            m_Random.Next(ControlButtonBounds.X, ControlButtonBounds.Right),
            m_Random.Next(ControlButtonBounds.Y, ControlButtonBounds.Bottom));
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

    private static void DrawControlButtonBoundsOverlay(string annotatedImagePath)
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

        Cv2.Rectangle(annotated, ControlButtonBounds, ControlButtonOverlayColor, 2);
        Cv2.PutText(
            annotated,
            "ControlButtonBounds",
            new Point(ControlButtonBounds.X, Math.Max(25, ControlButtonBounds.Y - 10)),
            HersheyFonts.HersheySimplex,
            0.7,
            ControlButtonOverlayColor,
            2,
            LineTypes.AntiAlias);
        Cv2.ImWrite(annotatedImagePath, annotated);
    }
}
