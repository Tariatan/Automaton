using Automaton.Helpers;
using Automaton.MiningStates;
using Automaton.Primitives;
using Serilog;

namespace Automaton;

internal sealed class MiningAutomationService
{
    private static readonly ILogger Logger = Log.ForContext<MiningAutomationService>();

    private readonly MiningAutomationContext m_Context;
    private IMiningAutomationState m_CurrentState;

    public MiningAutomationService()
        : this(new ScreenCaptureService(), new AutomationInputController(), new SystemAutomationClock())
    {
    }

    private MiningAutomationService(
        ScreenCaptureService screenCaptureService,
        IAutomationInputController automationInputController,
        IAutomationClock automationClock)
    {
        m_Context = new MiningAutomationContext(screenCaptureService, automationInputController, automationClock);
        m_CurrentState = new StartingGameState();
    }

    public MiningAutomationStepSummary AutomateCurrentScreen(
        MiningAutomationStateKind startingState,
        CancellationToken cancellationToken)
    {
        m_CurrentState = CreateState(startingState);
        Logger.Information("Mining automation loop starting. StartingState={StartingState}", startingState);
        m_Context.AutomationInputController.Delay(Delays.AutomationStartupDelayMs, cancellationToken);
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

                m_Context.AutomationInputController.Delay(Delays.StateMachineNextStepDelayMs, cancellationToken);
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

    private static IMiningAutomationState CreateState(MiningAutomationStateKind stateKind)
    {
        return stateKind switch
        {
            MiningAutomationStateKind.StartingGame => new StartingGameState(),
            MiningAutomationStateKind.Login => new LoginState(),
            MiningAutomationStateKind.Dock => new DockingState(),
            MiningAutomationStateKind.UnloadCargo => new UnloadingCargoState(),
            MiningAutomationStateKind.Undocking => new UndockingState(),
            MiningAutomationStateKind.SelectBeltAndWarp => new SelectBeltAndWarpState(),
            MiningAutomationStateKind.ApproachingAsteroid => new ApproachingAsteroidState(),
            MiningAutomationStateKind.Mining => new MiningState(),
            MiningAutomationStateKind.Recovery => new RecoveryState(),
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
