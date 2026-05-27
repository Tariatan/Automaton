namespace Automaton.ProjectDiscoveryStates;

internal sealed record DiscoveryAutomationStateTransition(
    DiscoveryAutomationStateKind State,
    DiscoveryAutomationStateKind NextState,
    DiscoveryAutomationActionKind Action,
    string? CapturePath = null);

internal sealed record DiscoveryAutomationStepSummary(
    DiscoveryAutomationStateKind State,
    DiscoveryAutomationStateKind NextState,
    DiscoveryAutomationActionKind Action,
    string? CapturePath);

internal enum DiscoveryAutomationStateKind
{
    None,
    StartingGame,
    Login,
    Discover,
    Recovery,
    RecoverSlowDownPopup,
    RecoverConnectionLostPopup,
    RecoverMaxSubmissionsPopup
}

internal enum DiscoveryAutomationActionKind
{
    None,
    StartGame,
    LoginPilot,
    LoginNextPilot,
    DiscoverAndSubmit,
    Recover,
    RecoverSlowDownPopup,
    RecoverConnectionLostPopup,
    RecoverMaxSubmissionsPopup,
    StopAutomation,
    NoFurtherPilotsAvailable,
}
