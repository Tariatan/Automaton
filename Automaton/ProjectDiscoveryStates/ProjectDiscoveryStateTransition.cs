using System.ComponentModel;
using System.Reflection;

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
    [Description("Starting game")]
    StartingGame,
    [Description("Login")]
    Login,
    [Description("Discover")]
    Discover,
    [Description("Recovery")]
    Recovery,
    [Description("Recover from overlapped polygons")]
    RecoverOverlap,
    [Description("Recover from Slow Down popup")]
    RecoverSlowDownPopup,
    [Description("Recover from Connection Lost popup")]
    RecoverConnectionLostPopup,
    [Description("Recover from Max Submissions popup")]
    RecoverMaxSubmissionsPopup,
    [Description("Recover from the client being not in foreground")]
    RecoverClientIsRunningButtonVisible
}

internal static class DiscoveryAutomationStateKindExtensions
{
    internal static string ToDisplayString(this DiscoveryAutomationStateKind kind) =>
        typeof(DiscoveryAutomationStateKind)
            .GetField(kind.ToString())
            ?.GetCustomAttribute<DescriptionAttribute>()
            ?.Description ?? kind.ToString();
}

internal enum DiscoveryAutomationActionKind
{
    StartGame,
    RestartGame,
    Reboot,
    LoginPilot,
    LoginNextPilot,
    DiscoverAndSubmit,
    Recover,
    RecoverOverlap,
    RecoverSlowDownPopup,
    RecoverConnectionLostPopup,
    RecoverMaxSubmissionsPopup,
    NoFurtherPilotsAvailable,
    Shutdown,
}

internal enum DiscoveryAutomationFailureReason
{
    None,
    DetectionMiss,
}
