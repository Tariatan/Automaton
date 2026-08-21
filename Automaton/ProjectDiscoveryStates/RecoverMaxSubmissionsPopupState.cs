using Automaton.Core.Detectors;
using Automaton.Core.Helpers;
using Automaton.Detectors;
using Serilog;

namespace Automaton.ProjectDiscoveryStates;

internal sealed class RecoverMaxSubmissionsPopupState(
    IGameActionService gameActionService,
    ScreenCaptureService screenCaptureService,
    PilotAvatarDetector pilotAvatarDetector,
    IAutomationInputController automationInputController) : IProjectDiscoveryAutomationState
{
    private const int ClientRestartDelayMs = 120_000;
    private const string NoFurtherPilotsAvailableCaptureSuffix = ".discovery-no-further-pilots-available";
    private readonly ILogger m_Logger = Log.ForContext<RecoverMaxSubmissionsPopupState>();
    public DiscoveryAutomationStateKind Kind => DiscoveryAutomationStateKind.RecoverMaxSubmissionsPopup;

    public DiscoveryAutomationStateTransition Execute(ProjectDiscoveryAutomationContext context, CancellationToken cancellationToken)
    {
        m_Logger.Warning("Maximum submissions popup detected during {DetectionStage}, CurrentPilotIndex={CurrentPilotIndex}",
            context.LastAction,
            context.CurrentPilotIndex);

        if (!PilotRegistry.TryGetNextPilotIndex(context.CurrentPilotIndex, out var nextPilotIndex))
        {
            m_Logger.Warning("Maximum submissions popup detected for final configured pilot. Logging out before final capture. CurrentPilotIndex={CurrentPilotIndex}", context.CurrentPilotIndex);

            // Logout to take the final wallets capture
            gameActionService.Logout(screenCaptureService, pilotAvatarDetector, context.CurrentPilotIndex, cancellationToken);
            using var capture = screenCaptureService.CaptureCurrentScreen(NoFurtherPilotsAvailableCaptureSuffix);

            m_Logger.Warning("No further pilots are available. CurrentPilotIndex={CurrentPilotIndex}, CapturePath={CapturePath}", context.CurrentPilotIndex, capture.CapturePath);

            // Transition to NoFurtherPilotsAvailable to stop automation
            return new DiscoveryAutomationStateTransition(
                Kind,
                Kind,
                DiscoveryAutomationActionKind.NoFurtherPilotsAvailable,
                capture.CapturePath);
        }

        m_Logger.Information("Resolved next pilot index after maximum submissions. CurrentPilotIndex={CurrentPilotIndex}, NextPilotIndex={NextPilotIndex}", context.CurrentPilotIndex, nextPilotIndex);
        
        // Start with the next pilot after game restart
        context.CurrentPilotIndex = nextPilotIndex;

        // Restart the game with a reasonable delay to raise the chances of pilot's submissions quota reset
        gameActionService.CloseGameClient(cancellationToken);
        m_Logger.Information("Waiting {DelaySeconds:0.###} seconds before restarting game after maximum submissions popup.", TimeSpan.FromMilliseconds(ClientRestartDelayMs).TotalSeconds);
        automationInputController.Delay(ClientRestartDelayMs, cancellationToken);

        return new DiscoveryAutomationStateTransition(
            Kind,
            DiscoveryAutomationStateKind.StartingGame,
            DiscoveryAutomationActionKind.RestartGame);
    }
}
