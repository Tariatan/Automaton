using Automaton.Helpers;
using Automaton.MiningStates;
using Automaton.Primitives;
using OpenCvSharp;

namespace Automaton.Tests;

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
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(pilotScreen.Clone),
            new SampleImageProcessor());
        var automationInputControllerMock = new StubAutomationInputController();
        var state = new LoginState(automationInputControllerMock);

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
        Assert.Equal(MiningAutomationStateKind.UnloadCargo, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.LoginPilot, transition.Action);
        Assert.Single(automationInputControllerMock.MoveTargets);
        Assert.Equal(new Point(854, 782), automationInputControllerMock.MoveTargets[0]);
        Assert.Equal(1, automationInputControllerMock.ClickCount);
        Assert.Equal([Delays.PilotLoginMs, Delays.MinimumClickMs], automationInputControllerMock.Delays);
        Assert.Equal(2, automationInputControllerMock.KeyInputs.Count);
        AssertKeyChord(automationInputControllerMock.KeyInputs[0], VirtualKeys.Control, VirtualKeys.W);
        AssertKeyChord(automationInputControllerMock.KeyInputs[1], VirtualKeys.Control, VirtualKeys.W);
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
        var state = new LoginState(automationInputControllerMock);

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
        Assert.Equal(MiningAutomationStateKind.Recovery, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Recover, transition.Action);
        Assert.Empty(automationInputControllerMock.MoveTargets);
        Assert.Equal(0, automationInputControllerMock.ClickCount);
        Assert.Empty(automationInputControllerMock.Delays);
        Assert.Single(automationInputControllerMock.KeyInputs);
        AssertKeyChord(automationInputControllerMock.KeyInputs[0], VirtualKeys.Control, VirtualKeys.W);
    }

    private static void WritePilotAvatarTemplates(string pilotDirectory, int pilotIndex)
    {
        Directory.CreateDirectory(pilotDirectory);
        using var avatar = SyntheticCommonImageFactory.LoadPilotAvatarImage(pilotIndex);
        using var focusedAvatar = SyntheticCommonImageFactory.LoadFocusedPilotAvatarImage(pilotIndex);
        Cv2.ImWrite(Path.Combine(pilotDirectory, $"{pilotIndex}.png"), avatar);
        Cv2.ImWrite(Path.Combine(pilotDirectory, $"{pilotIndex}_focused.png"), focusedAvatar);
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

    private static void AssertTripleKeyChord(
        KeyboardInput keyInput,
        ushort firstModifierVirtualKey,
        ushort secondModifierVirtualKey,
        ushort virtualKey)
    {
        Assert.Equal(firstModifierVirtualKey, keyInput.ModifierVirtualKey);
        Assert.Equal(secondModifierVirtualKey, keyInput.SecondModifierVirtualKey);
        Assert.Equal(virtualKey, keyInput.VirtualKey);
    }
}
