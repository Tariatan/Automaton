namespace Automaton.MiningStates;

internal sealed record MiningAutomationStateTransition(
    MiningAutomationStateKind State,
    MiningAutomationStateKind NextState,
    MiningAutomationActionKind Action,
    string? CapturePath = null);

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
    RecoverConnectionLostPopup,
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
    QuitGameAndExitApplication,
    Reboot,
    Relogin,
    Recover,
    RecoverConnectionLostPopup
}
