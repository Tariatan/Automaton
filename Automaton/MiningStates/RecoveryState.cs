using Automaton.Detectors;
using Automaton.Primitives;
using OpenCvSharp;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class RecoveryState : IMiningAutomationState
{
    private const string CaptureSuffix = ".mining-recovery";

    private readonly AsteroidBeltOverviewDetector m_BeltOverviewDetector;
    private readonly ILogger m_Logger;

    public RecoveryState()
        : this(new AsteroidBeltOverviewDetector(), Log.ForContext<RecoveryState>())
    {
    }

    private RecoveryState(AsteroidBeltOverviewDetector beltOverviewDetector, ILogger? logger = null)
    {
        m_BeltOverviewDetector = beltOverviewDetector;
        m_Logger = logger ?? Log.ForContext<RecoveryState>();
    }

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.Recovery;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        m_Logger.Debug("Executing {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();

        // Debounce
        context.AutomationInputController.Delay(Delays.RecoveryMs, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(CaptureSuffix);
        using var screen = Cv2.ImRead(capturePath);

        var nextState = MiningAutomationStateKind.None;

        // Try to detect safe haven again
        if (context.LastAction == MiningAutomationActionKind.QuitGameFromSpace)
        {
            var beltAnalysis = m_BeltOverviewDetector.Analyze(screen);
            if (!beltAnalysis.OverviewLocated || beltAnalysis.HomeStationBounds is null)
            {
                m_Logger.Error("Home Station not found in Belt overview while undocked. Quit Game instead of endless wandering in space.");
                context.AutomationInputController.QuitGame(cancellationToken);
                return new MiningAutomationStateTransition(
                    Kind,
                    MiningAutomationStateKind.None,
                    MiningAutomationActionKind.None,
                    capturePath);
            }

            // We are still in space and docking is possible
            nextState = MiningAutomationStateKind.Dock;
        }

        // Try to detect Undock button again
        if (context.LastAction == MiningAutomationActionKind.QuitGameFromDock)
        {
            if (!UndockButtonDetector.TryLocate(screen, out _))
            {
                m_Logger.Error("Undock button not found while docked. Quit Game since this state is unrecoverable.");
                context.AutomationInputController.QuitGame(cancellationToken);
                return new MiningAutomationStateTransition(
                    Kind,
                    MiningAutomationStateKind.None,
                    MiningAutomationActionKind.None,
                    capturePath);
            }

            // Docked and Undock button found
            nextState = MiningAutomationStateKind.UnloadCargo;
        }

        return new MiningAutomationStateTransition(
            Kind,
            nextState,
            MiningAutomationActionKind.Recover,
            capturePath);
    }
}
