using Automaton.Helpers;
using Automaton.Primitives;
using Serilog;

namespace Automaton.ProjectDiscoveryStates;

internal sealed class RecoverConnectionLostPopupState(
    IAutomationInputController automationInputController) : IProjectDiscoveryAutomationState
{
    private readonly ILogger m_Logger = Log.ForContext<RecoverConnectionLostPopupState>();
    public DiscoveryAutomationStateKind Kind => DiscoveryAutomationStateKind.RecoverConnectionLostPopup;

    public DiscoveryAutomationStateTransition Execute(ProjectDiscoveryAutomationContext context, CancellationToken cancellationToken)
    {
        m_Logger.Error("Connection Lost popup detected during {DetectionStage}. Stopping automation",
            context.LastAction);
        automationInputController.Delay(Delays.ConnectionLostExitMs, cancellationToken);
        automationInputController.PressKey(VirtualKeys.Enter, cancellationToken);
        return new DiscoveryAutomationStateTransition(
            Kind,
            Kind,
            DiscoveryAutomationActionKind.RecoverConnectionLostPopup);
    }
}
