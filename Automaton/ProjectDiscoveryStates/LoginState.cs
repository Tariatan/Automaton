using Automaton.CommonAutomationStates;
using Automaton.Detectors;
using Automaton.Helpers;
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
    private const string NoFurtherPilotsAvailableCaptureSuffix = ".discovery-no-further-pilots-available";
    private readonly CommonLoginState m_CommonLoginState = new(gameActionService, automationInputController, pilotAvatarDetector, loggedInPilotDetector);
    private readonly ILogger m_Logger = Log.ForContext<LoginState>();
    public DiscoveryAutomationStateKind Kind => DiscoveryAutomationStateKind.Login;

    public DiscoveryAutomationStateTransition Execute(ProjectDiscoveryAutomationContext context, CancellationToken cancellationToken)
    {
        if (context.LastAction == DiscoveryAutomationActionKind.LoginNextPilot)
        {
            if (!PilotRegistry.TryGetNextPilotIndex(context.CurrentPilotIndex, out var nextPilotIndex))
            {
                using var capture = screenCaptureService.CaptureCurrentScreen(NoFurtherPilotsAvailableCaptureSuffix);
                m_Logger.Warning(
                    "Failed to resolve next pilot index. CurrentPilotIndex={CurrentPilotIndex}, CapturePath={CapturePath}",
                    context.CurrentPilotIndex,
                    capture.CapturePath);
                gameActionService.CloseGameClient(cancellationToken);
                return new DiscoveryAutomationStateTransition(
                    Kind,
                    Kind,
                    DiscoveryAutomationActionKind.NoFurtherPilotsAvailable,
                    capture.CapturePath);
            }

            m_Logger.Information(
                "Resolved next pilot index. CurrentPilotIndex={CurrentPilotIndex}, NextPilotIndex={NextPilotIndex}",
                context.CurrentPilotIndex,
                nextPilotIndex);
            context.CurrentPilotIndex = nextPilotIndex;
        }

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
