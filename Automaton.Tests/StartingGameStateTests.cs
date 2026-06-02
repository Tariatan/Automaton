using System.IO;
using Automaton.Detectors;
using Automaton.Helpers;
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
        if (Directory.Exists("captures")) Directory.Delete("captures", true);
        using var screen = SyntheticCommonImageFactory.LoadPlayButtonScreenImage();
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(screen.Clone),
            new SampleImageProcessor());
        var automationInputControllerMock = new StubAutomationInputController();
        var gameActionService = new StubGameActionService();
        var state = new StartingGameState(automationInputControllerMock, gameActionService, new PlayNowButtonDetector());

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.Login, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.StartGame, transition.Action);
        Assert.Single(automationInputControllerMock.MoveTargets);
        Assert.Equal(1, automationInputControllerMock.ClickCount);
        Assert.Equal([Delays.LauncherStartupMs], automationInputControllerMock.Delays);
        Assert.Empty(automationInputControllerMock.KeyInputs);
        Assert.Equal(1, gameActionService.CloseActiveWindowCallCount);
    }

    [Fact]
    public void Execute_PlayNowButtonMissing_TransitionsToRecoveryWithoutInput()
    {
        // Arrange
        if (Directory.Exists("captures")) Directory.Delete("captures", true);
        using var blankScreen = new Mat(new Size(900, 640), MatType.CV_8UC3, new Scalar(18, 18, 18));
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(blankScreen.Clone),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputControllerMock = new StubAutomationInputController();
        var gameActionService = new StubGameActionService();
        var state = new StartingGameState(automationInputControllerMock, gameActionService, new PlayNowButtonDetector());

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.Recovery, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Relogin, transition.Action);
        Assert.Empty(automationInputControllerMock.MoveTargets);
        Assert.Equal(0, automationInputControllerMock.ClickCount);
        Assert.Empty(automationInputControllerMock.Delays);
        Assert.Empty(automationInputControllerMock.KeyInputs);
        Assert.Equal(0, gameActionService.CloseActiveWindowCallCount);
    }
}
