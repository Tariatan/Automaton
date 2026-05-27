using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class UndockingState(
    IAutomationInputController automationInputController,
    LocationChangeTimerDetector detector)
    : IMiningAutomationState
{
    private const int LocationChangeTimerPollingAttemptCount = 30;
    private const string CaptureSuffix = ".mining-undocking";

    private readonly ILogger m_Logger = Log.ForContext<UndockingState>();

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.Undocking;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        m_Logger.Debug("Executing {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();
        
        var capture = context.ScreenCaptureService.CaptureCurrentScreen(CaptureSuffix);
        cancellationToken.ThrowIfCancellationRequested();

        automationInputController.Delay(Delays.UndockingBounceMs, cancellationToken);
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
        automationInputController.ClickUiElement(GeometryHelper.Center(undockButtonBounds), cancellationToken);
        capture.Dispose();

        automationInputController.Delay(Delays.InitialUndockMs, cancellationToken);

        // Try to locate Location Change Timer icon with 1 second interval
        for (var attempt = 0; attempt < LocationChangeTimerPollingAttemptCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            capture = context.ScreenCaptureService.CaptureCurrentScreen(CaptureSuffix);
            if (detector.TryLocate(capture.Image, out _))
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
            automationInputController.Delay(Delays.LocationChangeTimerPollingMs, cancellationToken);
        }

        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Recovery,
            MiningAutomationActionKind.Recover,
            capture.CapturePath);
    }

}
