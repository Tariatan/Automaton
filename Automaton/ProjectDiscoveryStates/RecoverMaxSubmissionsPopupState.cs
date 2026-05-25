using Automaton.Helpers;
using Automaton.Primitives;
using Serilog;

namespace Automaton.ProjectDiscoveryStates;

internal sealed class RecoverMaxSubmissionsPopupState(
    IAutomationInputController automationInputController) : IProjectDiscoveryAutomationState
{
    private readonly ILogger m_Logger = Log.ForContext<RecoverMaxSubmissionsPopupState>();
    public DiscoveryAutomationStateKind Kind => DiscoveryAutomationStateKind.RecoverMaxSubmissionsPopup;

    public DiscoveryAutomationStateTransition Execute(ProjectDiscoveryAutomationContext context, CancellationToken cancellationToken)
    {
        m_Logger.Warning("Maximum submissions popup detected during {DetectionStage}, CurrentPilotIndex={CurrentPilotIndex}",
            context.LastAction,
            context.CurrentPilotIndex);

        // Logout current pilot
        var delay = TimeSpan.FromMilliseconds(Delays.PilotLogoutMs);
        m_Logger.Information("Logging out pilot {CurrentPilotIndex} for {DelaySeconds:0.###} seconds...", context.CurrentPilotIndex, delay.TotalSeconds);
        automationInputController.Logout(cancellationToken);

        return new DiscoveryAutomationStateTransition(
            Kind,
            DiscoveryAutomationStateKind.Login,
            DiscoveryAutomationActionKind.LoginNextPilot);
    }
}
