using Serilog;

namespace Automaton.MiningStates;

internal sealed class PendingMiningAutomationState(MiningAutomationStateKind kind)
    : IMiningAutomationState
{
    private readonly ILogger m_Logger = Log.ForContext<PendingMiningAutomationState>();

    public MiningAutomationStateKind Kind { get; } = kind;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        m_Logger.Information("Executing pending {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();
        return new MiningAutomationStateTransition(
            Kind,
            Kind,
            MiningAutomationActionKind.None);
    }
}
