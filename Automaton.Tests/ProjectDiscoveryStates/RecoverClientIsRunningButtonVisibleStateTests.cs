using Automaton.CommonAutomationStates;
using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Infrastructure;
using Automaton.Primitives;
using Automaton.ProjectDiscoveryStates;
using Automaton.Tests.Stubs;
using OpenCvSharp;

namespace Automaton.Tests.ProjectDiscoveryStates;

public sealed class RecoverClientIsRunningButtonVisibleStateTests
{
    [Fact]
    public void Kind_Default_ReturnsRecoverClientIsRunningButtonVisible()
    {
        // Arrange
        using var screen = CreateClientIsRunningButtonScreen(out _);
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(screen.Clone),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        using var clientIsRunningButtonDetector = new ClientIsRunningButtonDetector();
        var state = new RecoverClientIsRunningButtonVisibleState(
            screenCaptureService,
            new CommonRecoverClientIsRunningButtonVisibleState(automationInputController, clientIsRunningButtonDetector));

        // Act
        var kind = state.Kind;

        // Assert
        Assert.Equal(DiscoveryAutomationStateKind.RecoverClientIsRunningButtonVisible, kind);
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
            screenCaptureService,
            new CommonRecoverClientIsRunningButtonVisibleState(automationInputController, clientIsRunningButtonDetector));
        var context = new ProjectDiscoveryAutomationContext(1)
        {
            LastAction = DiscoveryAutomationActionKind.RestartGame
        };

        // Act
        var transition = state.Execute(context, CancellationToken.None);

        // Assert
        Assert.Equal(DiscoveryAutomationStateKind.RecoverClientIsRunningButtonVisible, transition.State);
        Assert.Equal(DiscoveryAutomationStateKind.StartingGame, transition.NextState);
        Assert.Equal(DiscoveryAutomationActionKind.RestartGame, transition.Action);
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
