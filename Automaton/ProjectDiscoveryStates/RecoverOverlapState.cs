using Automaton.Helpers;
using Automaton.Primitives;
using Serilog;

namespace Automaton.ProjectDiscoveryStates;

internal sealed class RecoverOverlapState(
    IAutomationInputController automationInputController,
    IGameActionService gameActionService) : IProjectDiscoveryAutomationState
{
    private readonly ILogger m_Logger = Log.ForContext<RecoverOverlapState>();
    public DiscoveryAutomationStateKind Kind => DiscoveryAutomationStateKind.RecoverOverlap;

    public DiscoveryAutomationStateTransition Execute(ProjectDiscoveryAutomationContext context, CancellationToken cancellationToken)
    {
        m_Logger.Warning("Recovering from overlap: re-opening discovery playfield.");
        gameActionService.CloseActiveWindow(cancellationToken);
        automationInputController.Delay(Delays.WindowActivationMs, cancellationToken);
        gameActionService.ToggleProjectDiscoveryWindow(cancellationToken);
        return new DiscoveryAutomationStateTransition(
            Kind,
            DiscoveryAutomationStateKind.Discover,
            DiscoveryAutomationActionKind.RecoverOverlap);
    }
}
