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
        if (!commonStartGameState.TryStartGame(
            screenCaptureService,
            CaptureSuffix,
            cancellationToken,
            out var capturePath))
        {
            return new DiscoveryAutomationStateTransition(
                Kind,
                DiscoveryAutomationStateKind.RecoverConnectionLostPopup,
                DiscoveryAutomationActionKind.StopAutomation,
                capturePath);
        }

        return new DiscoveryAutomationStateTransition(
            Kind,
            DiscoveryAutomationStateKind.Login,
            DiscoveryAutomationActionKind.StartGame,
            capturePath);
    }
}
