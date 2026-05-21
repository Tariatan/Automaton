using Automaton.Detectors;
using Automaton.Utilities;
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
        
        var capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(CaptureSuffix);
        cancellationToken.ThrowIfCancellationRequested();

        context.AutomationInputController.Delay(Delays.UndockingWindowActivationMs, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        using var initialScreen = Cv2.ImRead(capturePath);

        // TryLocate Undock button
        if (!UndockButtonDetector.TryLocate(initialScreen, out var undockButtonBounds))
        {
            // Failed to detect Undock button
            m_Logger.Error("Not in Dock => abort undocking");
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Recover,
                capturePath);
        }

        // Undocking
        context.ClickUiElement(Center(undockButtonBounds), cancellationToken);

        context.AutomationInputController.Delay(Delays.InitialUndockMs, cancellationToken);

        // Try to locate Location Change Timer icon with 1 second interval
        for (var attempt = 0; attempt < LocationChangeTimerPollingAttemptCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(CaptureSuffix);
            using var screen = Cv2.ImRead(capturePath);
            if (m_Detector.TryLocate(screen, out _))
            {
                m_Logger.Information("Location Change Timer located");

                // Located => warp to asteroid belt
                return new MiningAutomationStateTransition(
                    Kind,
                    MiningAutomationStateKind.SelectBeltAndWarp,
                    MiningAutomationActionKind.CompleteUndock,
                    capturePath);
            }

            context.AutomationInputController.Delay(Delays.LocationChangeTimerPollingMs, cancellationToken);
        }

        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Recovery,
            MiningAutomationActionKind.Recover,
            capturePath);
    }

    private static Point Center(Rect bounds) => new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
}
