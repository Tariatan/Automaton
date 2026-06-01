using Automaton.CommonAutomationStates;
using Automaton.Detectors;
using Automaton.Helpers;
using Serilog;

namespace Automaton.ProjectDiscoveryStates;

internal sealed class LoginState(
    ScreenCaptureService screenCaptureService,
    IAutomationInputController automationInputController,
    IGameActionService gameActionService,
    PilotAvatarDetector pilotAvatarDetector) : IProjectDiscoveryAutomationState
{
    private const string CaptureSuffix = ".discovery-login";
    private readonly CommonLoginState m_CommonLoginState = new(automationInputController, pilotAvatarDetector);
    private readonly ILogger m_Logger = Log.ForContext<LoginState>();
    public DiscoveryAutomationStateKind Kind => DiscoveryAutomationStateKind.Login;

    public DiscoveryAutomationStateTransition Execute(ProjectDiscoveryAutomationContext context, CancellationToken cancellationToken)
    {
        if (context.LastAction == DiscoveryAutomationActionKind.LoginNextPilot)
        {
            if (!PilotRegistry.TryGetNextPilotIndex(context.CurrentPilotIndex, out var nextPilotIndex))
            {
                m_Logger.Warning("Failed to resolve next pilot index. CurrentPilotIndex={CurrentPilotIndex}", context.CurrentPilotIndex);
                return new DiscoveryAutomationStateTransition(
                    Kind,
                    Kind,
                    DiscoveryAutomationActionKind.NoFurtherPilotsAvailable);
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
                DiscoveryAutomationStateKind.RecoverConnectionLostPopup,
                DiscoveryAutomationActionKind.StopAutomation,
                capturePath);
        }

        if (context.LastAction == DiscoveryAutomationActionKind.StartGame)
        {
            gameActionService.TryHideUi(capturePath, cancellationToken);
        }

        m_Logger.Information("Pilot {PilotIndex} login succeeded. CapturePath={CapturePath}", context.CurrentPilotIndex, capturePath);
        return new DiscoveryAutomationStateTransition(
            Kind,
            DiscoveryAutomationStateKind.Discover,
            DiscoveryAutomationActionKind.LoginPilot,
            capturePath);
    }
}
