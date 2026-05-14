using Serilog;

namespace Automaton.MiningStates;

internal sealed class PendingMiningAutomationState : IMiningAutomationState
{
    private readonly ILogger m_Logger;

    public PendingMiningAutomationState(MiningAutomationStateKind kind, ILogger? logger = null)
    {
        Kind = kind;
        m_Logger = logger ?? Log.ForContext<PendingMiningAutomationState>();
    }

    public MiningAutomationStateKind Kind { get; }

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        m_Logger.Debug("Executing pending {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();
        return new MiningAutomationStateTransition(
            Kind,
            Kind,
            MiningAutomationActionKind.None);
    }
}
