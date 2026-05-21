using Automaton.Helpers;
using Automaton.MiningStates;
using Automaton.Primitives;
using OpenCvSharp;

namespace Automaton.Tests;

public sealed class LoginStateTests
{
    private static readonly int[] Expected = [20_000];

    [Fact]
    public void Execute_PilotTwoFound_LogsInPilotAndTransitionsToDocked()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var pilotDirectory = Path.Combine(workspace.Path, "pilot");
        var pilotSelectionPath = Path.Combine(workspace.Path, "pilot-selection.png");
        WritePilotAvatarTemplates(pilotDirectory, 2);
        WritePilotSelectionScreen(pilotSelectionPath, new Point(240, 180));
        var screenCaptureService = new Helpers.ScreenCaptureService(
            new StubScreenCaptureProvider(outputPath => File.Copy(pilotSelectionPath, outputPath, overwrite: true)),
            new SampleImageProcessor());
        var automationInputControllerMock = new StubAutomationInputController();
        var state = new LoginState();
        MiningAutomationStateTransition transition;

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            transition = state.Execute(
                new MiningAutomationContext(screenCaptureService, automationInputControllerMock, new StubAutomationClock()),
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
        Assert.Equal(new Point(272, 212), automationInputControllerMock.MoveTargets[0]);
        Assert.Equal(1, automationInputControllerMock.ClickCount);
        Assert.Equal(Expected, automationInputControllerMock.Delays);
        Assert.Single(automationInputControllerMock.KeyInputs);
        AssertKeyChord(automationInputControllerMock.KeyInputs[0], VirtualKeys.Control, VirtualKeys.W);
    }

    [Fact]
    public void Execute_PilotTwoMissing_TransitionsToRecoveryWithoutInput()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var pilotDirectory = Path.Combine(workspace.Path, "pilot");
        var pilotSelectionPath = Path.Combine(workspace.Path, "pilot-selection-empty.png");
        WritePilotAvatarTemplates(pilotDirectory, 2);
        WriteBlankScreen(pilotSelectionPath);
        var screenCaptureService = new Helpers.ScreenCaptureService(
            new StubScreenCaptureProvider(outputPath => File.Copy(pilotSelectionPath, outputPath, overwrite: true)),
            new SampleImageProcessor());
        var automationInputControllerMock = new StubAutomationInputController();
        var state = new LoginState();
        MiningAutomationStateTransition transition;

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            transition = state.Execute(
                new MiningAutomationContext(screenCaptureService, automationInputControllerMock, new StubAutomationClock()),
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
        Assert.Empty(automationInputControllerMock.KeyInputs);
    }

    private static void WriteBlankScreen(string outputPath)
    {
        using var image = new Mat(new Size(900, 640), MatType.CV_8UC3, new Scalar(18, 18, 18));
        Cv2.ImWrite(outputPath, image);
    }

    private static void WritePilotAvatarTemplates(string pilotDirectory, int pilotIndex)
    {
        Directory.CreateDirectory(pilotDirectory);
        using var avatar = CreatePilotAvatarTemplate(focused: false);
        using var focusedAvatar = CreatePilotAvatarTemplate(focused: true);
        Cv2.ImWrite(Path.Combine(pilotDirectory, $"{pilotIndex}.png"), avatar);
        Cv2.ImWrite(Path.Combine(pilotDirectory, $"{pilotIndex}_focused.png"), focusedAvatar);
    }

    private static void WritePilotSelectionScreen(string outputPath, Point pilotAvatarLocation)
    {
        using var screen = new Mat(new Size(900, 640), MatType.CV_8UC3, new Scalar(18, 18, 18));
        using var focusedAvatar = CreatePilotAvatarTemplate(focused: true);
        using var region = new Mat(screen, new Rect(pilotAvatarLocation.X, pilotAvatarLocation.Y, focusedAvatar.Width, focusedAvatar.Height));
        focusedAvatar.CopyTo(region);
        Cv2.ImWrite(outputPath, screen);
    }

    private static Mat CreatePilotAvatarTemplate(bool focused)
    {
        var image = new Mat(new Size(64, 64), MatType.CV_8UC3, focused ? new Scalar(42, 70, 120) : new Scalar(85, 85, 85));
        Cv2.Rectangle(image, new Rect(6, 6, 52, 52), focused ? new Scalar(80, 130, 210) : new Scalar(120, 120, 120), -1);
        Cv2.Circle(image, new Point(32, 24), 12, focused ? new Scalar(130, 195, 245) : new Scalar(180, 180, 180), -1, LineTypes.AntiAlias);
        Cv2.Ellipse(image, new Point(32, 48), new Size(18, 10), 0, 0, 360, focused ? new Scalar(35, 95, 185) : new Scalar(65, 65, 65), -1, LineTypes.AntiAlias);
        Cv2.Line(image, new Point(10, 58), new Point(58, 10), focused ? new Scalar(210, 180, 60) : new Scalar(150, 150, 150), 2, LineTypes.AntiAlias);

        if (focused)
        {
            return image;
        }

        var grayImage = new Mat();
        Cv2.CvtColor(image, grayImage, ColorConversionCodes.BGR2GRAY);
        image.Dispose();
        return grayImage;
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

    private static void AssertKeyChord(
        KeyboardInput keyInput,
        ushort firstModifierVirtualKey,
        ushort secondModifierVirtualKey,
        ushort virtualKey)
    {
        Assert.Equal(firstModifierVirtualKey, keyInput.ModifierVirtualKey);
        Assert.Equal(secondModifierVirtualKey, keyInput.SecondModifierVirtualKey);
        Assert.Equal(virtualKey, keyInput.VirtualKey);
    }

    private sealed class StubScreenCaptureProvider(Action<string> captureAction)
        : Helpers.ScreenCaptureService.IScreenCaptureProvider
    {
        public void CaptureToFile(string outputPath)
        {
            captureAction(outputPath);
        }
    }

    private sealed class StubAutomationClock : IAutomationClock
    {
        public DateTime UtcNow { get; } = new(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);
    }
}
