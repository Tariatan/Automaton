using Automaton.MiningStates;
using Serilog;

namespace Automaton;

internal sealed class MiningAutomationService
{
    private const int StartupDelayMilliseconds = 3_000;
    private const int StepDelayMilliseconds = 500;
    private static readonly ILogger Logger = Log.ForContext<MiningAutomationService>();

    private readonly MiningAutomationContext m_Context;
    private IMiningAutomationState m_CurrentState;

    public MiningAutomationService()
        : this(new ScreenCaptureService(), new AutomationInputController(), new SystemAutomationClock())
    {
    }

    internal MiningAutomationService(
        ScreenCaptureService screenCaptureService,
        IAutomationInputController automationInputController,
        IAutomationClock automationClock)
    {
        m_Context = new MiningAutomationContext(screenCaptureService, automationInputController, automationClock);
        m_CurrentState = new StartingGameState();
    }

    public MiningAutomationStepSummary AutomateCurrentScreen(CancellationToken cancellationToken)
    {
        return AutomateCurrentScreen(MiningAutomationStateKind.StartingGame, cancellationToken);
    }

    public MiningAutomationStepSummary AutomateCurrentScreen(
        MiningAutomationStateKind startingState,
        CancellationToken cancellationToken)
    {
        m_CurrentState = CreateState(startingState);
        Logger.Information("Mining automation loop starting. StartingState={StartingState}", startingState);
        m_Context.AutomationInputController.Delay(StartupDelayMilliseconds, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        MiningAutomationStepSummary? lastSummary = null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                lastSummary = ExecuteSingleStep(cancellationToken);
                m_Context.AutomationInputController.Delay(StepDelayMilliseconds, cancellationToken);
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

    public MiningAutomationStepSummary ExecuteSingleStep(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var transition = m_CurrentState.Execute(m_Context, cancellationToken);
        Logger.Information(
            "Mining automation step executed. State={State}, NextState={NextState}, Action={Action}, CapturePath={CapturePath}",
            transition.State,
            transition.NextState,
            transition.Action,
            transition.CapturePath);
        m_CurrentState = CreateState(transition.NextState);

        return new MiningAutomationStepSummary(
            transition.State,
            transition.NextState,
            transition.Action,
            transition.CapturePath,
            transition.DockedScreen,
            transition.LocationChangeTimer,
            transition.AsteroidBeltOverview,
            transition.AsteroidBeltLanding);
    }

    private static IMiningAutomationState CreateState(MiningAutomationStateKind stateKind)
    {
        return stateKind switch
        {
            MiningAutomationStateKind.StartingGame => new StartingGameState(),
            MiningAutomationStateKind.Login => new LoginState(),
            MiningAutomationStateKind.Docked => new DockedState(),
            MiningAutomationStateKind.Undocking => new UndockingState(),
            MiningAutomationStateKind.SelectBeltAndWarp => new SelectBeltAndWarpState(),
            MiningAutomationStateKind.LandedOnAsteroidBelt => new LandedOnAsteroidBeltState(),
            MiningAutomationStateKind.ApproachingAsteroid => new ApproachingAsteroidState(),
            _ => new PendingMiningAutomationState(stateKind)
        };
    }
}

internal sealed record MiningAutomationStepSummary(
    MiningAutomationStateKind State,
    MiningAutomationStateKind NextState,
    MiningAutomationActionKind Action,
    string? CapturePath,
    DockedScreenAnalysis? DockedScreen,
    LocationChangeTimerLocation? LocationChangeTimer = null,
    AsteroidBeltOverviewAnalysis? AsteroidBeltOverview = null,
    AsteroidBeltLandingAnalysis? AsteroidBeltLanding = null);

internal enum MiningAutomationStateKind
{
    StartingGame,
    Login,
    Docked,
    Undocking,
    SelectBeltAndWarp,
    WarpingToAsteroidField,
    LandedOnAsteroidBelt,
    ApproachingAsteroid,
    Mining,
    UnloadCargo,
    Recovery
}

internal enum MiningAutomationActionKind
{
    None,
    StartGame,
    LoginPilot,
    FocusMiningHold,
    Undock,
    CompleteUndock,
    WarpToAsteroidField,
    ApproachAsteroid,
    ActivateMiningLasers,
    UnloadCargo,
    Recover
}
