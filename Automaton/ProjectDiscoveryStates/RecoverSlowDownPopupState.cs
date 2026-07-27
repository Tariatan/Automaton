using Automaton.Primitives;
using Automaton.Helpers;
using Serilog;

namespace Automaton.ProjectDiscoveryStates;

internal sealed class RecoverSlowDownPopupState(
    IAutomationInputController automationInputController,
    IGameActionService gameActionService,
    IDiscoveryGameActions discoveryGameActions) : IProjectDiscoveryAutomationState
{
    private const int SubmissionWindowMs = 70_000;

    private readonly ILogger m_Logger = Log.ForContext<RecoverSlowDownPopupState>();
    public DiscoveryAutomationStateKind Kind => DiscoveryAutomationStateKind.RecoverSlowDownPopup;

    public DiscoveryAutomationStateTransition Execute(ProjectDiscoveryAutomationContext context, CancellationToken cancellationToken)
    {
        m_Logger.Warning(
            "Slow Down popup detected, RecoveryDelayMilliseconds={RecoveryDelayMilliseconds}",
            SubmissionWindowMs);
        gameActionService.CloseActiveWindow(cancellationToken);
        automationInputController.Delay(SubmissionWindowMs, cancellationToken);
        automationInputController.Delay(Delays.MinimumClickMs, cancellationToken);
        discoveryGameActions.ToggleProjectDiscoveryWindow(cancellationToken);
        return new DiscoveryAutomationStateTransition(
            Kind,
            DiscoveryAutomationStateKind.Discover,
            DiscoveryAutomationActionKind.RecoverSlowDownPopup);
    }
}
