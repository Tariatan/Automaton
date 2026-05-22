using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.MiningStates;
using Automaton.Primitives;
using Serilog;

namespace Automaton;

internal sealed class MiningAutomationService
{
    private readonly IAutomationInputController m_AutomationInputController;
    private static readonly ILogger Logger = Log.ForContext<MiningAutomationService>();

    private readonly MiningAutomationContext m_Context;
    private readonly PlayNowButtonLocator m_PlayNowButtonLocator;
    private readonly HomeStationDetector m_HomeStationDetector;
    private readonly LocationChangeTimerDetector m_LocationChangeTimerDetector;
    private readonly InventoryDetector m_InventoryDetector;
    private readonly DowntimeDetector m_DowntimeDetector;
    private readonly AsteroidBeltOverviewDetector m_AsteroidBeltOverviewDetector;
    private readonly MineOverviewDetector m_MineOverviewDetector;
    private readonly FirstAsteroidWithinReachDetector m_FirstAsteroidWithinReachDetector;
    private readonly MiningAsteroidDetector m_MiningAsteroidDetector;
    private readonly MiningLaserDetector m_MiningLaserDetector;
    private readonly WarOverviewDetector m_WarOverviewDetector;
    private IMiningAutomationState m_CurrentState;

    internal MiningAutomationService(
        ScreenCaptureService screenCaptureService,
        IAutomationInputController automationInputController,
        IAutomationClock automationClock,
        PlayNowButtonLocator playNowButtonLocator,
        HomeStationDetector homeStationDetector,
        LocationChangeTimerDetector locationChangeTimerDetector,
        InventoryDetector inventoryDetector,
        DowntimeDetector downtimeDetector,
        AsteroidBeltOverviewDetector asteroidBeltOverviewDetector,
        MineOverviewDetector mineOverviewDetector,
        FirstAsteroidWithinReachDetector firstAsteroidWithinReachDetector,
        MiningAsteroidDetector miningAsteroidDetector,
        MiningLaserDetector miningLaserDetector,
        WarOverviewDetector warOverviewDetector)
    {
        m_AutomationInputController = automationInputController;
        m_Context = new MiningAutomationContext(screenCaptureService, automationClock);
        m_PlayNowButtonLocator = playNowButtonLocator;
        m_HomeStationDetector = homeStationDetector;
        m_LocationChangeTimerDetector = locationChangeTimerDetector;
        m_InventoryDetector = inventoryDetector;
        m_DowntimeDetector = downtimeDetector;
        m_AsteroidBeltOverviewDetector = asteroidBeltOverviewDetector;
        m_MineOverviewDetector = mineOverviewDetector;
        m_FirstAsteroidWithinReachDetector = firstAsteroidWithinReachDetector;
        m_MiningAsteroidDetector = miningAsteroidDetector;
        m_MiningLaserDetector = miningLaserDetector;
        m_WarOverviewDetector = warOverviewDetector;
        m_CurrentState = CreateState(MiningAutomationStateKind.StartingGame);
    }

    public MiningAutomationStepSummary AutomateCurrentScreen(
        MiningAutomationStateKind startingState,
        CancellationToken cancellationToken)
    {
        m_CurrentState = CreateState(startingState);
        Logger.Information("Mining automation loop starting. StartingState={StartingState}", startingState);
        m_AutomationInputController.Delay(Delays.AutomationStartupDelayMs, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        MiningAutomationStepSummary? lastSummary = null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                lastSummary = ExecuteSingleStep(cancellationToken);
                if (lastSummary.Action == MiningAutomationActionKind.QuitGameAndExitApplication)
                {
                    Logger.Information(
                        "Mining automation requested application exit. State={State}, NextState={NextState}, CapturePath={CapturePath}",
                        lastSummary.State,
                        lastSummary.NextState,
                        lastSummary.CapturePath);
                    return lastSummary;
                }

                m_AutomationInputController.Delay(Delays.StateMachineNextStepDelayMs, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (lastSummary is not null)
        {
            Logger.Information(
                "Mining automation loop canceled after a completed step. State={State}, NextState={NextState}, Action={Action}, CapturePath={CapturePath}",
                lastSummary.State,
                lastSummary.NextState,
                lastSummary.Action,
                lastSummary.CapturePath);
            return lastSummary;
        }

        return lastSummary ?? throw new OperationCanceledException(cancellationToken);
    }

    private MiningAutomationStepSummary ExecuteSingleStep(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var transition = m_CurrentState.Execute(m_Context, cancellationToken);
        Logger.Information(
            "Mining automation step executed. State={State}, NextState={NextState}, Action={Action}, CapturePath={CapturePath}",
            transition.State,
            transition.NextState,
            transition.Action,
            transition.CapturePath);
        m_Context.LastAction = transition.Action;
        m_CurrentState = CreateState(transition.NextState);

        return new MiningAutomationStepSummary(
            transition.State,
            transition.NextState,
            transition.Action,
            transition.CapturePath);
    }

    private IMiningAutomationState CreateState(MiningAutomationStateKind stateKind)
    {
        return stateKind switch
        {
            MiningAutomationStateKind.StartingGame => new StartingGameState(m_AutomationInputController, m_PlayNowButtonLocator),
            MiningAutomationStateKind.Login => new LoginState(m_AutomationInputController),
            MiningAutomationStateKind.Dock => new DockingState(m_AutomationInputController, m_HomeStationDetector),
            MiningAutomationStateKind.UnloadCargo => new UnloadingCargoState(m_AutomationInputController, m_InventoryDetector, m_DowntimeDetector),
            MiningAutomationStateKind.Undocking => new UndockingState(m_AutomationInputController, m_LocationChangeTimerDetector),
            MiningAutomationStateKind.SelectBeltAndWarp => new SelectBeltAndWarpState(m_AutomationInputController, m_AsteroidBeltOverviewDetector, m_MineOverviewDetector, Random.Shared.Next),
            MiningAutomationStateKind.ApproachingAsteroid => new ApproachingAsteroidState(m_AutomationInputController, m_MineOverviewDetector, m_FirstAsteroidWithinReachDetector),
            MiningAutomationStateKind.Mining => new MiningState(m_AutomationInputController, m_MiningAsteroidDetector, m_MiningLaserDetector, m_WarOverviewDetector),
            MiningAutomationStateKind.Recovery => new RecoveryState(m_AutomationInputController, m_AsteroidBeltOverviewDetector),
            _ => new PendingMiningAutomationState(stateKind)
        };
    }
}

internal sealed record MiningAutomationStepSummary(
    MiningAutomationStateKind State,
    MiningAutomationStateKind NextState,
    MiningAutomationActionKind Action,
    string? CapturePath);

internal enum MiningAutomationStateKind
{
    None,
    StartingGame,
    Login,
    Dock,
    Undocking,
    SelectBeltAndWarp,
    WarpingToAsteroidField,
    ApproachingAsteroid,
    Mining,
    UnloadCargo,
    Recovery,
}

internal enum MiningAutomationActionKind
{
    None,
    StartGame,
    LoginPilot,
    Dock,
    FocusMiningHold,
    Undock,
    CompleteUndock,
    WarpToAsteroidField,
    ApproachAsteroid,
    ActivateMiningLasers,
    UnloadCargo,
    QuitGameFromSpace,
    QuitGameFromDock,
    QuitGameAndExitApplication,
    Recover
}
