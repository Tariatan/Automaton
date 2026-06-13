using Automaton.CommonAutomationStates;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class RecoverClientIsRunningButtonVisibleState(
    CommonRecoverClientIsRunningButtonVisibleState commonRecoverClientIsRunningButtonVisibleState) : IMiningAutomationState
{
    private const string CaptureSuffix = ".mining-client-is-running-button-visible-recovery";
    private readonly ILogger m_Logger = Log.ForContext<RecoverClientIsRunningButtonVisibleState>();

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.RecoverClientIsRunningButtonVisible;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        var capturePath = commonRecoverClientIsRunningButtonVisibleState.Execute(
            context.ScreenCaptureService,
            CaptureSuffix,
            context.LastAction,
            m_Logger,
            cancellationToken);

        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.StartingGame,
            MiningAutomationActionKind.RestartGame,
            capturePath);
    }
}
