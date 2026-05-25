namespace Automaton.ProjectDiscoveryStates;

internal interface IDiscoveryAutomationStateFactory
{
    IProjectDiscoveryAutomationState Create(DiscoveryAutomationStateKind stateKind);
}
