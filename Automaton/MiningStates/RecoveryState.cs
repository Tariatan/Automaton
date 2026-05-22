using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class RecoveryState(
    IAutomationInputController automationInputController,
    AsteroidBeltOverviewDetector beltOverviewDetector,
    ILogger? logger = null)
    : IMiningAutomationState
{
    private const string CaptureSuffix = ".mining-recovery";

    private readonly ILogger m_Logger = logger ?? Log.ForContext<RecoveryState>();

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.Recovery;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        m_Logger.Debug("Executing {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();

        // Debounce
        automationInputController.Delay(Delays.RecoveryMs, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        using var capture = context.ScreenCaptureService.CaptureCurrentScreen(CaptureSuffix);

        var nextState = MiningAutomationStateKind.None;

        // Try to detect safe haven again
        if (context.LastAction == MiningAutomationActionKind.QuitGameFromSpace)
        {
            var beltAnalysis = beltOverviewDetector.Analyze(capture.Image);
            if (!beltAnalysis.OverviewLocated || beltAnalysis.HomeStationBounds is null)
            {
                m_Logger.Error("Home Station not found in Belt overview while undocked. Quit Game instead of endless wandering in space.");
                automationInputController.QuitGame(cancellationToken);
                // Debounce
                automationInputController.Delay(Delays.RecoveryMs, cancellationToken);
                return new MiningAutomationStateTransition(
                    Kind,
                    MiningAutomationStateKind.StartingGame,
                    MiningAutomationActionKind.None,
                    capture.CapturePath);
            }

            // We are still in space and docking is possible
            nextState = MiningAutomationStateKind.Dock;
        }

        // Try to detect Undock button again
        if (context.LastAction == MiningAutomationActionKind.QuitGameFromDock)
        {
            if (!UndockButtonDetector.TryLocate(capture.Image, out _))
            {
                m_Logger.Error("Undock button not found while docked. Quit Game since this state is unrecoverable.");
                automationInputController.QuitGame(cancellationToken);
                // Debounce
                automationInputController.Delay(Delays.RecoveryMs, cancellationToken);
                return new MiningAutomationStateTransition(
                    Kind,
                    MiningAutomationStateKind.StartingGame,
                    MiningAutomationActionKind.None,
                    capture.CapturePath);
            }

            // Docked and Undock button found
            nextState = MiningAutomationStateKind.UnloadCargo;
        }

        return new MiningAutomationStateTransition(
            Kind,
            nextState,
            MiningAutomationActionKind.Recover,
            capture.CapturePath);
    }
}
