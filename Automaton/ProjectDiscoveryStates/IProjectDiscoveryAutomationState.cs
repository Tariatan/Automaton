namespace Automaton.ProjectDiscoveryStates;

internal interface IProjectDiscoveryAutomationState
{
    DiscoveryAutomationStateKind Kind { get; }

    DiscoveryAutomationStateTransition Execute(
        ProjectDiscoveryAutomationContext context,
        CancellationToken cancellationToken);
}
