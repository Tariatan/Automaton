using Automaton.CommonAutomationStates;
using Automaton.Helpers;
using Serilog;

namespace Automaton.ProjectDiscoveryStates;

internal sealed class RecoverClientIsRunningButtonVisibleState(
    ScreenCaptureService screenCaptureService,
    CommonRecoverClientIsRunningButtonVisibleState commonRecoverClientIsRunningButtonVisibleState) : IProjectDiscoveryAutomationState
{
    private const string CaptureSuffix = ".discovery-client-is-running-button-visible-recovery";
    private readonly ILogger m_Logger = Log.ForContext<RecoverClientIsRunningButtonVisibleState>();

    public DiscoveryAutomationStateKind Kind => DiscoveryAutomationStateKind.RecoverClientIsRunningButtonVisible;

    public DiscoveryAutomationStateTransition Execute(ProjectDiscoveryAutomationContext context, CancellationToken cancellationToken)
    {
        var capturePath = commonRecoverClientIsRunningButtonVisibleState.Execute(
            screenCaptureService,
            CaptureSuffix,
            context.LastAction,
            m_Logger,
            cancellationToken);

        return new DiscoveryAutomationStateTransition(
            Kind,
            DiscoveryAutomationStateKind.StartingGame,
            DiscoveryAutomationActionKind.RestartGame,
            capturePath);
    }
}
