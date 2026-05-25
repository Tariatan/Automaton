using Automaton.Primitives;
using Automaton.Helpers;
using Serilog;

namespace Automaton.ProjectDiscoveryStates;

internal sealed class RecoverSlowDownPopupState(IAutomationInputController automationInputController) : IProjectDiscoveryAutomationState
{
    private readonly ILogger m_Logger = Log.ForContext<RecoverSlowDownPopupState>();
    public DiscoveryAutomationStateKind Kind => DiscoveryAutomationStateKind.RecoverSlowDownPopup;

    public DiscoveryAutomationStateTransition Execute(ProjectDiscoveryAutomationContext context, CancellationToken cancellationToken)
    {
        m_Logger.Warning(
            "Slow Down popup detected, RecoveryDelayMilliseconds={RecoveryDelayMilliseconds}",
            Delays.SubmissionWindowMs);
        automationInputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.W, cancellationToken);
        automationInputController.Delay(Delays.SubmissionWindowMs, cancellationToken);
        automationInputController.Delay(Delays.MinimumClickMs, cancellationToken);
        automationInputController.PressKeyChord(VirtualKeys.Alt, VirtualKeys.L, cancellationToken);
        return new DiscoveryAutomationStateTransition(
            Kind,
            DiscoveryAutomationStateKind.Discover,
            DiscoveryAutomationActionKind.RecoverSlowDownPopup);
    }
}
