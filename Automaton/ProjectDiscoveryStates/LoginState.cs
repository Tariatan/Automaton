using Automaton.Core.CommonAutomationStates;
using Automaton.Core.Detectors;
using Automaton.Core.Helpers;
using Serilog;

namespace Automaton.ProjectDiscoveryStates;

internal sealed class LoginState(
    ScreenCaptureService screenCaptureService,
    IGameActionService gameActionService,
    IAutomationInputController automationInputController,
    PilotAvatarDetector pilotAvatarDetector,
    LoggedInPilotDetector loggedInPilotDetector) : IProjectDiscoveryAutomationState
{
    private const string CaptureSuffix = ".discovery-login";
    private readonly CommonLoginState m_CommonLoginState = new(gameActionService, automationInputController, pilotAvatarDetector, loggedInPilotDetector);
    private readonly ILogger m_Logger = Log.ForContext<LoginState>();
    public DiscoveryAutomationStateKind Kind => DiscoveryAutomationStateKind.Login;

    public DiscoveryAutomationStateTransition Execute(ProjectDiscoveryAutomationContext context, CancellationToken cancellationToken)
    {
        m_Logger.Information("Attempting pilot {PilotIndex} login", context.CurrentPilotIndex);
        if (!m_CommonLoginState.TryLoginPilot(
            screenCaptureService,
            context.CurrentPilotIndex,
            CaptureSuffix,
            cancellationToken,
            out var capturePath))
        {
            m_Logger.Error("Pilot {PilotIndex} login failed! CapturePath={CapturePath}", context.CurrentPilotIndex, capturePath);
            return new DiscoveryAutomationStateTransition(
                Kind,
                DiscoveryAutomationStateKind.Recovery,
                DiscoveryAutomationActionKind.RestartGame,
                capturePath);
        }

        m_Logger.Information("Pilot {PilotIndex} login succeeded. CapturePath={CapturePath}", context.CurrentPilotIndex, capturePath);
        return new DiscoveryAutomationStateTransition(
            Kind,
            DiscoveryAutomationStateKind.Discover,
            DiscoveryAutomationActionKind.LoginPilot,
            capturePath);
    }
}
