using Automaton.Helpers;
using Automaton.Primitives;
using Serilog;

namespace Automaton.ProjectDiscoveryStates;

internal sealed class RecoverMaxSubmissionsPopupState(
    IGameActionService gameActionService) : IProjectDiscoveryAutomationState
{
    private readonly ILogger m_Logger = Log.ForContext<RecoverMaxSubmissionsPopupState>();
    public DiscoveryAutomationStateKind Kind => DiscoveryAutomationStateKind.RecoverMaxSubmissionsPopup;

    public DiscoveryAutomationStateTransition Execute(ProjectDiscoveryAutomationContext context, CancellationToken cancellationToken)
    {
        m_Logger.Warning("Maximum submissions popup detected during {DetectionStage}, CurrentPilotIndex={CurrentPilotIndex}",
            context.LastAction,
            context.CurrentPilotIndex);

        var delay = TimeSpan.FromMilliseconds(Delays.PilotLogoutMs);
        m_Logger.Information("Logging out pilot {CurrentPilotIndex} for {DelaySeconds:0.###} seconds...", context.CurrentPilotIndex, delay.TotalSeconds);
        gameActionService.Logout(cancellationToken);

        return new DiscoveryAutomationStateTransition(
            Kind,
            DiscoveryAutomationStateKind.Login,
            DiscoveryAutomationActionKind.LoginNextPilot);
    }
}
