using Automaton.CommonAutomationStates;
using Automaton.Detectors;
using Automaton.Helpers;
using Serilog;

namespace Automaton.ProjectDiscoveryStates;

internal sealed class LoginState(
    ScreenCaptureService screenCaptureService,
    IAutomationInputController automationInputController) : IProjectDiscoveryAutomationState
{
    private const string CaptureSuffix = ".discovery-login";
    private readonly CommonLoginState m_CommonLoginState = new(automationInputController);
    private readonly ILogger m_Logger = Log.ForContext<LoginState>();
    public DiscoveryAutomationStateKind Kind => DiscoveryAutomationStateKind.Login;

    public DiscoveryAutomationStateTransition Execute(ProjectDiscoveryAutomationContext context, CancellationToken cancellationToken)
    {
        if (context.LastAction == DiscoveryAutomationActionKind.LoginNextPilot)
        {
            if (!PilotAvatarLocator.TryGetNextPilotIndex(context.CurrentPilotIndex, out var nextPilotIndex))
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
        using var capture = screenCaptureService.CaptureCurrentScreen($"{CaptureSuffix}-{context.CurrentPilotIndex}");
        if (!m_CommonLoginState.TryLoginPilot(
            context.CurrentPilotIndex,
            capture.CapturePath,
            cancellationToken,
            out _))
        {
            m_Logger.Error("Pilot {PilotIndex} login failed! CapturePath={CapturePath}", context.CurrentPilotIndex, capture.CapturePath);
            return new DiscoveryAutomationStateTransition(
                Kind,
                DiscoveryAutomationStateKind.RecoverConnectionLostPopup,
                DiscoveryAutomationActionKind.StopAutomation,
                capture.CapturePath);
        }

        m_Logger.Information("Pilot {PilotIndex} login succeeded. CapturePath={CapturePath}", context.CurrentPilotIndex, capture.CapturePath);
        return new DiscoveryAutomationStateTransition(
            Kind,
            DiscoveryAutomationStateKind.Discover,
            DiscoveryAutomationActionKind.LoginPilot,
            capture.CapturePath);
    }
}
