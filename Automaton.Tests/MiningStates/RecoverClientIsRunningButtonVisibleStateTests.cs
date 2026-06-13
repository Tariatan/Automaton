using Automaton.CommonAutomationStates;
using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Infrastructure;
using Automaton.MiningStates;
using Automaton.Primitives;
using Automaton.Tests.Stubs;
using OpenCvSharp;

namespace Automaton.Tests.MiningStates;

public sealed class RecoverClientIsRunningButtonVisibleStateTests
{
    [Fact]
    public void Kind_Default_ReturnsRecoverClientIsRunningButtonVisible()
    {
        // Arrange
        var automationInputController = new StubAutomationInputController();
        using var clientIsRunningButtonDetector = new ClientIsRunningButtonDetector();
        var state = new RecoverClientIsRunningButtonVisibleState(
            new CommonRecoverClientIsRunningButtonVisibleState(automationInputController, clientIsRunningButtonDetector));

        // Act
        var kind = state.Kind;

        // Assert
        Assert.Equal(MiningAutomationStateKind.RecoverClientIsRunningButtonVisible, kind);
    }

    [Fact]
    public void Execute_ClientIsRunningButtonVisible_ClicksButtonAndTransitionsToStartingGame()
    {
        // Arrange
        using var screen = CreateClientIsRunningButtonScreen(out var expectedBounds);
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(screen.Clone),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        using var clientIsRunningButtonDetector = new ClientIsRunningButtonDetector();
        var state = new RecoverClientIsRunningButtonVisibleState(
            new CommonRecoverClientIsRunningButtonVisibleState(automationInputController, clientIsRunningButtonDetector));
        var context = new MiningAutomationContext(screenCaptureService, new StubAutomationClock())
        {
            LastAction = MiningAutomationActionKind.RestartGame
        };

        // Act
        var transition = state.Execute(context, CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.RecoverClientIsRunningButtonVisible, transition.State);
        Assert.Equal(MiningAutomationStateKind.StartingGame, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.RestartGame, transition.Action);
        Assert.Equal(
            [Delays.ClientIsRunningButtonVisibleBeforeClickMs, Delays.ClientIsRunningButtonVisibleAfterClickMs],
            automationInputController.Delays);
        Assert.Equal([GeometryHelper.Center(expectedBounds)], automationInputController.MoveTargets);
        Assert.Equal(1, automationInputController.ClickCount);
    }

    private static Mat CreateClientIsRunningButtonScreen(out Rect expectedBounds)
    {
        using var clientIsRunningButton = EmbeddedResourceLoader.LoadMat("client_is_running.png");
        expectedBounds = new Rect(120, 80, clientIsRunningButton.Width, clientIsRunningButton.Height);
        var screen = new Mat(
            new Size(expectedBounds.Right + 80, expectedBounds.Bottom + 80),
            MatType.CV_8UC3,
            Scalar.Black);
        using var region = new Mat(screen, expectedBounds);
        clientIsRunningButton.CopyTo(region);
        return screen;
    }
}
