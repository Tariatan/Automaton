using Automaton.Primitives;
using Automaton.Helpers;
using Serilog;

namespace Automaton.ProjectDiscoveryStates;

internal sealed class RecoverSlowDownPopupState(
    IAutomationInputController automationInputController,
    IGameActionService gameActionService) : IProjectDiscoveryAutomationState
{
    private readonly ILogger m_Logger = Log.ForContext<RecoverSlowDownPopupState>();
    public DiscoveryAutomationStateKind Kind => DiscoveryAutomationStateKind.RecoverSlowDownPopup;

    public DiscoveryAutomationStateTransition Execute(ProjectDiscoveryAutomationContext context, CancellationToken cancellationToken)
    {
        m_Logger.Warning(
            "Slow Down popup detected, RecoveryDelayMilliseconds={RecoveryDelayMilliseconds}",
            Delays.SubmissionWindowMs);
        gameActionService.CloseActiveWindow(cancellationToken);
        automationInputController.Delay(Delays.SubmissionWindowMs, cancellationToken);
        automationInputController.Delay(Delays.MinimumClickMs, cancellationToken);
        gameActionService.ToggleProjectDiscoveryWindow(cancellationToken);
        return new DiscoveryAutomationStateTransition(
            Kind,
            DiscoveryAutomationStateKind.Discover,
            DiscoveryAutomationActionKind.RecoverSlowDownPopup);
    }
}
