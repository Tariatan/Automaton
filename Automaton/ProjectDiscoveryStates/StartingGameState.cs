using Automaton.CommonAutomationStates;
using Automaton.Helpers;

namespace Automaton.ProjectDiscoveryStates;

internal sealed class StartingGameState(
    ScreenCaptureService screenCaptureService,
    CommonStartGameState commonStartGameState) : IProjectDiscoveryAutomationState
{
    private const string CaptureSuffix = ".discovery-starting-game";
    public DiscoveryAutomationStateKind Kind => DiscoveryAutomationStateKind.StartingGame;

    public DiscoveryAutomationStateTransition Execute(ProjectDiscoveryAutomationContext context, CancellationToken cancellationToken)
    {
        using var capture = screenCaptureService.CaptureCurrentScreen(CaptureSuffix);
        if (!commonStartGameState.TryStartGame(capture.CapturePath, cancellationToken, out _))
        {
            return new DiscoveryAutomationStateTransition(
                Kind,
                DiscoveryAutomationStateKind.RecoverConnectionLostPopup,
                DiscoveryAutomationActionKind.StopAutomation,
                capture.CapturePath);
        }

        return new DiscoveryAutomationStateTransition(
            Kind,
            DiscoveryAutomationStateKind.Login,
            DiscoveryAutomationActionKind.StartGame,
            capture.CapturePath);
    }
}
