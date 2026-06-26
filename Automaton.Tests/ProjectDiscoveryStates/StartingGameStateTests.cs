using Automaton.CommonAutomationStates;
using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.ProjectDiscoveryStates;
using Automaton.Tests.Stubs;

namespace Automaton.Tests.ProjectDiscoveryStates;

public sealed class StartingGameStateTests
{
    [Fact]
    public void Execute_GameStarts_ResetsConsecutivePlayfieldMisses()
    {
        // Arrange
        if (Directory.Exists("captures")) Directory.Delete("captures", true);
        using var screen = SyntheticCommonImageFactory.LoadPlayButtonScreenImage();
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(screen.Clone),
            new SampleImageProcessor());
        var automationInputController = new StubAutomationInputController();
        var gameActionService = new StubGameActionService();
        var state = new StartingGameState(
            screenCaptureService,
            new CommonStartGameState(automationInputController, gameActionService, new PlayNowButtonDetector()));
        var context = new ProjectDiscoveryAutomationContext(1, keepDebugImages: false)
        {
            ConsecutivePlayfieldMisses = 4
        };

        // Act
        var transition = state.Execute(context, CancellationToken.None);

        // Assert
        Assert.Equal(DiscoveryAutomationStateKind.StartingGame, transition.State);
        Assert.Equal(DiscoveryAutomationStateKind.Login, transition.NextState);
        Assert.Equal(DiscoveryAutomationActionKind.StartGame, transition.Action);
        Assert.Equal(0, context.ConsecutivePlayfieldMisses);
    }
}
