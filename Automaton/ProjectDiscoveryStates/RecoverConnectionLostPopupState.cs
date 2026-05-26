using Automaton.CommonAutomationStates;
using Serilog;

namespace Automaton.ProjectDiscoveryStates;

internal sealed class RecoverConnectionLostPopupState(
    ConnectionLostPopupRecoveryBehavior connectionLostPopupRecoveryBehavior) : IProjectDiscoveryAutomationState
{
    private readonly ILogger m_Logger = Log.ForContext<RecoverConnectionLostPopupState>();
    public DiscoveryAutomationStateKind Kind => DiscoveryAutomationStateKind.RecoverConnectionLostPopup;

    public DiscoveryAutomationStateTransition Execute(ProjectDiscoveryAutomationContext context, CancellationToken cancellationToken)
    {
        connectionLostPopupRecoveryBehavior.Execute(context.LastAction, m_Logger, cancellationToken);

        return new DiscoveryAutomationStateTransition(
            Kind,
            DiscoveryAutomationStateKind.StartingGame,
            DiscoveryAutomationActionKind.RecoverConnectionLostPopup);
    }
}
