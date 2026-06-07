using Automaton.Detectors;
using Automaton.Helpers;
using Serilog;

namespace Automaton.ProjectDiscoveryStates;

internal sealed class RecoverMaxSubmissionsPopupState(
    IGameActionService gameActionService,
    ScreenCaptureService screenCaptureService,
    PilotAvatarDetector pilotAvatarDetector) : IProjectDiscoveryAutomationState
{
    private readonly ILogger m_Logger = Log.ForContext<RecoverMaxSubmissionsPopupState>();
    public DiscoveryAutomationStateKind Kind => DiscoveryAutomationStateKind.RecoverMaxSubmissionsPopup;

    public DiscoveryAutomationStateTransition Execute(ProjectDiscoveryAutomationContext context, CancellationToken cancellationToken)
    {
        m_Logger.Warning("Maximum submissions popup detected during {DetectionStage}, CurrentPilotIndex={CurrentPilotIndex}",
            context.LastAction,
            context.CurrentPilotIndex);

        gameActionService.Logout(screenCaptureService, pilotAvatarDetector, context.CurrentPilotIndex, cancellationToken);

        return new DiscoveryAutomationStateTransition(
            Kind,
            DiscoveryAutomationStateKind.Login,
            DiscoveryAutomationActionKind.LoginNextPilot);
    }
}
