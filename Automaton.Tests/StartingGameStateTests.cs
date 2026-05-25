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
        using var screen = SyntheticCommonImageFactory.LoadPlayButtonScreenImage();
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
        Assert.Equal([Delays.LauncherStartupMs], automationInputControllerMock.Delays);
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
        Assert.Equal(MiningAutomationActionKind.QuitGameAndExitApplication, transition.Action);
        Assert.Empty(automationInputControllerMock.MoveTargets);
        Assert.Equal(0, automationInputControllerMock.ClickCount);
        Assert.Empty(automationInputControllerMock.Delays);
        Assert.Empty(automationInputControllerMock.KeyInputs);
    }

    // ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local
    private static void AssertKeyChord(
        KeyboardInput keyInput,
        ushort modifierVirtualKey,
        ushort virtualKey)
    // ReSharper restore ParameterOnlyUsedForPreconditionCheck.Local
    {
        Assert.Equal(modifierVirtualKey, keyInput.ModifierVirtualKey);
        Assert.Null(keyInput.SecondModifierVirtualKey);
        Assert.Equal(virtualKey, keyInput.VirtualKey);
    }
}
