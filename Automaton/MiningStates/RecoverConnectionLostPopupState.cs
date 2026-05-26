using Automaton.CommonAutomationStates;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class RecoverConnectionLostPopupState(
    ConnectionLostPopupRecoveryBehavior connectionLostPopupRecoveryBehavior) : IMiningAutomationState
{
    private readonly ILogger m_Logger = Log.ForContext<RecoverConnectionLostPopupState>();

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.RecoverConnectionLostPopup;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        connectionLostPopupRecoveryBehavior.Execute(context.LastAction, m_Logger, cancellationToken);

        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.StartingGame,
            MiningAutomationActionKind.RecoverConnectionLostPopup);
    }
}
