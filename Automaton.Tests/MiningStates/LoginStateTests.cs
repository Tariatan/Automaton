using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.MiningStates;
using Automaton.Tests.Stubs;
using OpenCvSharp;

namespace Automaton.Tests.MiningStates;

public sealed class LoginStateTests
{
    [Fact]
    public void Execute_PilotTwoFound_LogsInPilotAndTransitionsToDocked()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var pilotDirectory = Path.Combine(workspace.Path, "pilot");
        WritePilotAvatarTemplates(pilotDirectory, 2);
        using var pilotScreen = SyntheticCommonImageFactory.LoadLoginPilotSelectionScreenImage();
        using var loggedInPilotScreen = SyntheticCommonImageFactory.LoadLoggedInPilotScreenImage();
        var screenCaptureService = BuildScreenCaptureService(
            pilotScreen.Clone,
            loggedInPilotScreen.Clone);
        var gameActionService = new StubGameActionService();
        var state = new LoginState(gameActionService, new PilotAvatarDetector(), new LoggedInPilotDetector());

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        MiningAutomationStateTransition transition;

        try
        {
            transition = state.Execute(
                new MiningAutomationContext(screenCaptureService, new StubAutomationClock()),
                CancellationToken.None);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.Equal(MiningAutomationStateKind.Login, transition.State);
        Assert.Equal(MiningAutomationStateKind.UnloadCargo, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.LoginPilot, transition.Action);
        Assert.Equal(MiningAutomationFailureReason.None, transition.FailureReason);
        Assert.Equal(1, gameActionService.LoginCallCount);
        Assert.False(gameActionService.LogoutCalled);
        Assert.Equal(1, gameActionService.CloseActiveWindowCallCount);
        Assert.All(gameActionService.LoginCalls, call => Assert.Equal(2, call.PilotIndex));
    }

    [Fact]
    public void Execute_LoggedInPilotCheckMissesRequestedPilot_LogsOutAndRetriesLogin()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var pilotDirectory = Path.Combine(workspace.Path, "pilot");
        WritePilotAvatarTemplates(pilotDirectory, 2);
        using var pilotScreen = SyntheticCommonImageFactory.LoadLoginPilotSelectionScreenImage();
        using var loggedInPilotScreen = SyntheticCommonImageFactory.LoadLoggedInPilotScreenImage();
        using var wrongLoggedInPilotScreen = BuildLoggedInScreenWithoutPilotPortrait(loggedInPilotScreen);
        var screenCaptureService = BuildScreenCaptureService(
            pilotScreen.Clone,
            wrongLoggedInPilotScreen.Clone,
            pilotScreen.Clone,
            loggedInPilotScreen.Clone);
        var gameActionService = new StubGameActionService();
        var state = new LoginState(gameActionService, new PilotAvatarDetector(), new LoggedInPilotDetector());

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        MiningAutomationStateTransition transition;

        try
        {
            transition = state.Execute(
                new MiningAutomationContext(screenCaptureService, new StubAutomationClock()),
                CancellationToken.None);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.Equal(MiningAutomationStateKind.Login, transition.State);
        Assert.Equal(MiningAutomationStateKind.UnloadCargo, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.LoginPilot, transition.Action);
        Assert.Equal(MiningAutomationFailureReason.None, transition.FailureReason);
        Assert.Equal(2, gameActionService.LoginCallCount);
        Assert.Equal(1, gameActionService.LogoutCallCount);
        Assert.Equal(2, gameActionService.CloseActiveWindowCallCount);
        Assert.All(gameActionService.LoginCalls, call => Assert.Equal(2, call.PilotIndex));
    }

    [Fact]
    public void Execute_PilotTwoMissing_TransitionsToRecoveryWithoutInput()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var pilotDirectory = Path.Combine(workspace.Path, "pilot");
        WritePilotAvatarTemplates(pilotDirectory, 2);
        using var blankScreen = SyntheticCommonImageFactory.LoadPilotAvatarImage(1);
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(blankScreen.Clone),
            new SampleImageProcessor());
        var automationInputControllerMock = new StubAutomationInputController();
        var gameActionService = new StubGameActionService();
        var state = new LoginState(gameActionService, new PilotAvatarDetector(), new LoggedInPilotDetector());

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        MiningAutomationStateTransition transition;

        try
        {
            transition = state.Execute(
                new MiningAutomationContext(screenCaptureService, new StubAutomationClock()),
                CancellationToken.None);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.Equal(MiningAutomationStateKind.Login, transition.State);
        Assert.Equal(MiningAutomationStateKind.Recovery, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Recover, transition.Action);
        Assert.Equal(MiningAutomationFailureReason.DetectionMiss, transition.FailureReason);
        Assert.Empty(automationInputControllerMock.MoveTargets);
        Assert.Equal(0, automationInputControllerMock.ClickCount);
        Assert.Empty(automationInputControllerMock.Delays);
        Assert.Empty(automationInputControllerMock.KeyInputs);
        Assert.Equal(1, gameActionService.CloseActiveWindowCallCount);
    }

    private static void WritePilotAvatarTemplates(string pilotDirectory, int pilotIndex)
    {
        Directory.CreateDirectory(pilotDirectory);
        using var avatar = SyntheticCommonImageFactory.LoadPilotAvatarImage(pilotIndex);
        using var focusedAvatar = SyntheticCommonImageFactory.LoadFocusedPilotAvatarImage(pilotIndex);
        Cv2.ImWrite(Path.Combine(pilotDirectory, $"{pilotIndex}.png"), avatar);
        Cv2.ImWrite(Path.Combine(pilotDirectory, $"{pilotIndex}_focused.png"), focusedAvatar);
    }

    private static ScreenCaptureService BuildScreenCaptureService(params Func<Mat>[] captureFactories)
    {
        var captures = new Queue<Func<Mat>>(captureFactories);
        return new ScreenCaptureService(
            new StubScreenCaptureProvider(() => captures.Count > 0
                ? captures.Dequeue()()
                : throw new InvalidOperationException("No screen capture queued.")),
            new SampleImageProcessor());
    }

    private static Mat BuildLoggedInScreenWithoutPilotPortrait(Mat source)
    {
        var image = source.Clone();
        image[new Rect(0, 48, 48, 48)].SetTo(Scalar.Black);
        return image;
    }
}
