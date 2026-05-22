namespace Automaton.MiningStates;

internal interface IMiningAutomationState
{
    MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken);
}
