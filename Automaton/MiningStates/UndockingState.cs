using Automaton.Detectors;
using Automaton.Primitives;
using OpenCvSharp;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class UndockingState : IMiningAutomationState
{
    private const int LocationChangeTimerPollingAttemptCount = 15;
    private const string CaptureSuffix = ".mining-undocking";

    private readonly LocationChangeTimerDetector m_Detector;
    private readonly ILogger m_Logger;

    public UndockingState()
        : this(new LocationChangeTimerDetector(), Log.ForContext<UndockingState>())
    {
    }

    private UndockingState(
        LocationChangeTimerDetector detector,
        ILogger? logger = null)
    {
        m_Detector = detector;
        m_Logger = logger ?? Log.ForContext<UndockingState>();
    }

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.Undocking;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        m_Logger.Debug("Executing {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();
        
        var capture = context.ScreenCaptureService.CaptureCurrentScreen(CaptureSuffix);
        cancellationToken.ThrowIfCancellationRequested();

        context.AutomationInputController.Delay(Delays.UndockingWindowActivationMs, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        // TryLocate Undock button
        if (!UndockButtonDetector.TryLocate(capture.Image, out var undockButtonBounds))
        {
            // Failed to detect Undock button
            m_Logger.Error("Not in Dock => abort undocking");
            var result = new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Recover,
                capture.CapturePath);
            capture.Dispose();
            return result;
        }

        // Undocking
        context.ClickUiElement(Center(undockButtonBounds), cancellationToken);
        capture.Dispose();

        context.AutomationInputController.Delay(Delays.InitialUndockMs, cancellationToken);

        // Try to locate Location Change Timer icon with 1 second interval
        for (var attempt = 0; attempt < LocationChangeTimerPollingAttemptCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            capture = context.ScreenCaptureService.CaptureCurrentScreen(CaptureSuffix);
            if (m_Detector.TryLocate(capture.Image, out _))
            {
                m_Logger.Information("Location Change Timer located");

                // Located => warp to asteroid belt
                var result = new MiningAutomationStateTransition(
                    Kind,
                    MiningAutomationStateKind.SelectBeltAndWarp,
                    MiningAutomationActionKind.CompleteUndock,
                    capture.CapturePath);
                capture.Dispose();
                return result;
            }

            capture.Dispose();
            context.AutomationInputController.Delay(Delays.LocationChangeTimerPollingMs, cancellationToken);
        }

        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Recovery,
            MiningAutomationActionKind.Recover,
            capture.CapturePath);
    }

    private static Point Center(Rect bounds) => new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
}
