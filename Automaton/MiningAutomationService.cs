using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.MiningStates;
using Automaton.Primitives;
using Serilog;

namespace Automaton;

internal sealed class MiningAutomationService(
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
    WarOverviewDetector warOverviewDetector,
    ConnectionLostPopupDetector connectionLostPopupDetector,
    CommonAutomationStates.ConnectionLostPopupRecoveryBehavior connectionLostPopupRecoveryBehavior)
{
    private static readonly ILogger Logger = Log.ForContext<MiningAutomationService>();
    private readonly MiningAutomationContext m_Context = new(screenCaptureService, automationClock);
    private IMiningAutomationState m_CurrentState = null!;

    public MiningAutomationStepSummary Automate(
        MiningAutomationStateKind startingState,
        CancellationToken cancellationToken)
    {
        m_CurrentState = CreateState(startingState);
        Logger.Information("Mining automation loop starting. StartingState={StartingState}", startingState);
        automationInputController.Delay(Delays.AutomationStartupDelayMs, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        MiningAutomationStepSummary? lastSummary = null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                lastSummary = ExecuteSingleStep(cancellationToken);

                automationInputController.TryHideUi(lastSummary.CapturePath, cancellationToken);

                if (TryTransitionToRecoverConnectionLostPopup(cancellationToken))
                {
                    continue;
                }

                if (lastSummary.Action is MiningAutomationActionKind.QuitGameAndExitApplication or MiningAutomationActionKind.Reboot)
                {
                    Logger.Information(
                        "Mining automation requested application stop. State={State}, NextState={NextState}, Action={Action}, CapturePath={CapturePath}",
                        lastSummary.State,
                        lastSummary.NextState,
                        lastSummary.Action,
                        lastSummary.CapturePath);
                    return lastSummary;
                }

                automationInputController.Delay(Delays.StateMachineNextStepDelayMs, cancellationToken);
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
            "Mining automation step executed. State={State}, NextState={NextState}, Action={Action}",
            transition.State,
            transition.NextState,
            transition.Action);
        m_Context.LastAction = transition.Action;
        m_CurrentState = CreateState(transition.NextState);

        return new MiningAutomationStepSummary(
            transition.State,
            transition.NextState,
            transition.Action,
            transition.CapturePath);
    }

    private bool TryTransitionToRecoverConnectionLostPopup(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var capture = m_Context.ScreenCaptureService.CaptureCurrentScreen(".mining-connection-lost-popup-check");
        var detection = connectionLostPopupDetector.Detect(capture.CapturePath);
        if (detection.State != PopupState.ConnectionLost)
        {
            return false;
        }

        Logger.Error("Connection Lost popup detected during {CurrentState}. CapturePath={CapturePath}", m_CurrentState.GetType().Name, capture.CapturePath);
        m_CurrentState = CreateState(MiningAutomationStateKind.RecoverConnectionLostPopup);
        return true;
    }

    private IMiningAutomationState CreateState(MiningAutomationStateKind stateKind)
    {
        return stateKind switch
        {
            MiningAutomationStateKind.StartingGame => new StartingGameState(automationInputController, playNowButtonLocator),
            MiningAutomationStateKind.Login => new LoginState(automationInputController),
            MiningAutomationStateKind.Dock => new DockingState(automationInputController, homeStationDetector),
            MiningAutomationStateKind.UnloadCargo => new UnloadingCargoState(automationInputController, inventoryDetector, downtimeDetector),
            MiningAutomationStateKind.Undocking => new UndockingState(automationInputController, locationChangeTimerDetector),
            MiningAutomationStateKind.SelectBeltAndWarp => new SelectBeltAndWarpState(automationInputController, asteroidBeltOverviewDetector, mineOverviewDetector, warOverviewDetector, Random.Shared.Next),
            MiningAutomationStateKind.ApproachingAsteroid => new ApproachingAsteroidState(automationInputController, mineOverviewDetector, firstAsteroidWithinReachDetector),
            MiningAutomationStateKind.Mining => new MiningState(automationInputController, miningAsteroidDetector, miningLaserDetector, warOverviewDetector),
            MiningAutomationStateKind.Recovery => new RecoveryState(automationInputController, asteroidBeltOverviewDetector, homeStationDetector, playNowButtonLocator),
            MiningAutomationStateKind.RecoverConnectionLostPopup => new RecoverConnectionLostPopupState(connectionLostPopupRecoveryBehavior),
            _ => new PendingMiningAutomationState(stateKind)
        };
    }
}
