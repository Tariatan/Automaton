namespace Automaton.ProjectDiscoveryStates;

internal sealed class ProjectDiscoveryAutomationContext(int initialPilotIndex)
{
    public int CurrentPilotIndex { get; set; } = initialPilotIndex;
    public int ConsecutivePlayfieldMisses { get; set; }
    public DiscoveryAutomationActionKind LastAction { get; set; }
}
