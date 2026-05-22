using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Infrastructure;
using Automaton.MiningStates;
using Automaton.Primitives;
using OpenCvSharp;

namespace Automaton.Tests;

public sealed class StartingGameStateTests
{
    [Fact]
    public void Execute_PlayNowButtonPresent_StartsGameAndTransitionsToLogin()
    {
        // Arrange
        using var screen = CreatePlayButtonScreen(new Point(260, 340));
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(screen.Clone),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputControllerMock = new StubAutomationInputController();
        var state = new StartingGameState(automationInputControllerMock, new PlayNowButtonLocator());

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.Login, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.StartGame, transition.Action);
        Assert.Single(automationInputControllerMock.MoveTargets);
        Assert.Equal(1, automationInputControllerMock.ClickCount);
        Assert.Equal([Delays.MiningLauncherStartupMs], automationInputControllerMock.Delays);
        Assert.Single(automationInputControllerMock.KeyInputs);
        AssertKeyChord(automationInputControllerMock.KeyInputs[0], VirtualKeys.Control, VirtualKeys.W);
    }

    [Fact]
    public void Execute_PlayNowButtonMissing_TransitionsToRecoveryWithoutInput()
    {
        // Arrange
        using var blankScreen = new Mat(new Size(900, 640), MatType.CV_8UC3, new Scalar(18, 18, 18));
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(blankScreen.Clone),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputControllerMock = new StubAutomationInputController();
        var state = new StartingGameState(automationInputControllerMock, new PlayNowButtonLocator());

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.Recovery, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Recover, transition.Action);
        Assert.Empty(automationInputControllerMock.MoveTargets);
        Assert.Equal(0, automationInputControllerMock.ClickCount);
        Assert.Empty(automationInputControllerMock.Delays);
        Assert.Empty(automationInputControllerMock.KeyInputs);
    }

    // ToDo: remove magic
    private static Mat CreatePlayButtonScreen(Point playButtonLocation)
    {
        var screen = new Mat(new Size(900, 640), MatType.CV_8UC3, new Scalar(18, 18, 18));
        using var playButton = EmbeddedResourceLoader.LoadMat("play.png");
        using var region = new Mat(screen, new Rect(playButtonLocation.X, playButtonLocation.Y, playButton.Width, playButton.Height));
        playButton.CopyTo(region);
        return screen;
    }

    private static void AssertKeyChord(
        KeyboardInput keyInput,
        ushort modifierVirtualKey,
        ushort virtualKey)
    {
        Assert.Equal(modifierVirtualKey, keyInput.ModifierVirtualKey);
        Assert.Null(keyInput.SecondModifierVirtualKey);
        Assert.Equal(virtualKey, keyInput.VirtualKey);
    }
}
