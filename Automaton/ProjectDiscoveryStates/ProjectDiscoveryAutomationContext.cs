using System.Windows;

namespace Automaton.ProjectDiscoveryStates;

internal sealed class ProjectDiscoveryAutomationContext(int initialPilotIndex, bool keepDebugImages)
{
    public bool KeepDebugImages { get; } = keepDebugImages;
    public int CurrentPilotIndex { get; set; } = initialPilotIndex;
    public int ConsecutivePlayfieldMisses { get; set; }
    public DiscoveryAutomationActionKind LastAction { get; set; }
}
