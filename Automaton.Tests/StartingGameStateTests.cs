using Automaton.MiningStates;
using OpenCvSharp;

namespace Automaton.Tests;

public sealed class StartingGameStateTests
{
    private const ushort VirtualKeyControl = 0x11;
    private const ushort VirtualKeyW = 0x57;

    [Fact]
    public void Execute_PlayNowButtonPresent_StartsGameAndTransitionsToLogin()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var startupCapturePath = Path.Combine(workspace.Path, "startup-screen.png");
        WritePlayButtonScreen(startupCapturePath, new Point(260, 340));
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(outputPath => File.Copy(startupCapturePath, outputPath, overwrite: true)),
            new SampleImageProcessor());
        var automationInputControllerMock = new StubAutomationInputController();
        var state = new StartingGameState();
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
        Assert.Equal(MiningAutomationStateKind.Login, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.StartGame, transition.Action);
        Assert.Single(automationInputControllerMock.MoveTargets);
        Assert.Equal(1, automationInputControllerMock.ClickCount);
        Assert.Equal(new[] { 20_000 }, automationInputControllerMock.Delays);
        Assert.Single(automationInputControllerMock.KeyInputs);
        AssertKeyChord(automationInputControllerMock.KeyInputs[0], VirtualKeyControl, VirtualKeyW);
    }

    [Fact]
    public void Execute_PlayNowButtonMissing_TransitionsToRecoveryWithoutInput()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var startupCapturePath = Path.Combine(workspace.Path, "startup-screen-empty.png");
        WriteBlankScreen(startupCapturePath);
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(outputPath => File.Copy(startupCapturePath, outputPath, overwrite: true)),
            new SampleImageProcessor());
        var automationInputControllerMock = new StubAutomationInputController();
        var state = new StartingGameState();
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

    private static void WritePlayButtonScreen(string outputPath, Point playButtonLocation)
    {
        using var screen = new Mat(new Size(900, 640), MatType.CV_8UC3, new Scalar(18, 18, 18));
        using var playButton = LoadPlayButtonImage();
        using var region = new Mat(screen, new Rect(playButtonLocation.X, playButtonLocation.Y, playButton.Width, playButton.Height));
        playButton.CopyTo(region);
        Cv2.ImWrite(outputPath, screen);
    }

    private static Mat LoadPlayButtonImage()
    {
        return EmbeddedResourceLoader.LoadMat("play.png");
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

    private sealed class StubScreenCaptureProvider(Action<string> captureAction)
        : ScreenCaptureService.IScreenCaptureProvider
    {
        public void CaptureToFile(string outputPath)
        {
            captureAction(outputPath);
        }
    }

    private sealed class StubAutomationInputController : IAutomationInputController
    {
        public List<Point> MoveTargets { get; } = [];

        public List<int> Delays { get; } = [];

        public List<KeyboardInput> KeyInputs { get; } = [];

        public int ClickCount { get; private set; }

        public void MoveTo(Point point)
        {
            MoveTargets.Add(point);
        }

        public void LeftClick(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ClickCount++;
        }

        public void PressKey(ushort virtualKey, CancellationToken cancellationToken)
        {
        }

        public void PressKeyChord(ushort modifierVirtualKey, ushort virtualKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            KeyInputs.Add(new KeyboardInput(modifierVirtualKey, null, virtualKey));
        }

        public void PressKeyChord(
            ushort firstModifierVirtualKey,
            ushort secondModifierKey,
            ushort virtualKey,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            KeyInputs.Add(new KeyboardInput(firstModifierVirtualKey, secondModifierKey, virtualKey));
        }

        public void QuitGame(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        public void Logout(CancellationToken cancellationToken)
        {
        }

        public void Delay(int milliseconds, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delays.Add(milliseconds);
        }
    }

    private readonly record struct KeyboardInput(
        ushort? ModifierVirtualKey,
        ushort? SecondModifierVirtualKey,
        ushort VirtualKey);

    private sealed class StubAutomationClock : IAutomationClock
    {
        public DateTime UtcNow { get; } = new(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);
    }
}
