namespace Automaton.ProjectDiscoveryStates;

internal sealed record DiscoveryAutomationStateTransition(
    DiscoveryAutomationStateKind State,
    DiscoveryAutomationStateKind NextState,
    DiscoveryAutomationActionKind Action,
    string? CapturePath = null)
{
    public DiscoveryAutomationFailureReason FailureReason { get; init; } = DiscoveryAutomationFailureReason.None;
}

internal sealed record DiscoveryAutomationStepSummary(
    DiscoveryAutomationStateKind State,
    DiscoveryAutomationStateKind NextState,
    DiscoveryAutomationActionKind Action,
    string? CapturePath);

internal enum DiscoveryAutomationStateKind
{
    StartingGame,
    Login,
    Discover,
    Recovery,
    RecoverOverlap,
    RecoverSlowDownPopup,
    RecoverConnectionLostPopup,
    RecoverMaxSubmissionsPopup
}

internal enum DiscoveryAutomationActionKind
{
    StartGame,
    LoginPilot,
    LoginNextPilot,
    DiscoverAndSubmit,
    Recover,
    RecoverOverlap,
    RecoverSlowDownPopup,
    RecoverConnectionLostPopup,
    RecoverMaxSubmissionsPopup,
    StopAutomation,
    NoFurtherPilotsAvailable,
}

internal enum DiscoveryAutomationFailureReason
{
    None,
    DetectionMiss,
}
