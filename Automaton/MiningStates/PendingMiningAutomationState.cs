using Serilog;

namespace Automaton.MiningStates;

internal sealed class PendingMiningAutomationState(MiningAutomationStateKind kind, ILogger? logger = null)
    : IMiningAutomationState
{
    private readonly ILogger m_Logger = logger ?? Log.ForContext<PendingMiningAutomationState>();

    public MiningAutomationStateKind Kind { get; } = kind;

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
