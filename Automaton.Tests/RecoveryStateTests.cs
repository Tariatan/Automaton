using Automaton.MiningStates;
using OpenCvSharp;

namespace Automaton.Tests;

public sealed class RecoveryStateTests
{
    [Fact]
    public void Execute_UndockButtonFound_TransitionsToUnloadCargo()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var dockedPath = Path.Combine(workspace.Path, "docked.png");
        SyntheticMiningImageFactory.WriteDockedItemHangarAndMiningHoldVisibleImage(dockedPath);
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(outputPath => File.Copy(dockedPath, outputPath, overwrite: true)),
            new SampleImageProcessor());
        var automationInputController = new StubAutomationInputController();
        var state = new RecoveryState();
        MiningAutomationStateTransition transition;

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            transition = state.Execute(
                new MiningAutomationContext(screenCaptureService, automationInputController, new StubAutomationClock()),
                CancellationToken.None);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.Equal(MiningAutomationStateKind.UnloadCargo, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Recover, transition.Action);
        Assert.Equal([60_000], automationInputController.Delays);
    }

    [Fact]
    public void Execute_UndockButtonMissing_TransitionsToDock()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var undockedPath = Path.Combine(workspace.Path, "undocked.png");
        using var image = SyntheticMiningImageFactory.CreateUndockedWithoutLocationChangeTimerImage();
        Cv2.ImWrite(undockedPath, image);
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(outputPath => File.Copy(undockedPath, outputPath, overwrite: true)),
            new SampleImageProcessor());
        var automationInputController = new StubAutomationInputController();
        var state = new RecoveryState();
        MiningAutomationStateTransition transition;

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            transition = state.Execute(
                new MiningAutomationContext(screenCaptureService, automationInputController, new StubAutomationClock()),
                CancellationToken.None);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.Equal(MiningAutomationStateKind.Dock, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Recover, transition.Action);
        Assert.Equal([60_000], automationInputController.Delays);
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
        public List<int> Delays { get; } = [];

        public void MoveTo(Point point)
        {
        }

        public void LeftClick(CancellationToken cancellationToken)
        {
        }

        public void PressKey(ushort virtualKey, CancellationToken cancellationToken)
        {
        }

        public void PressKeyChord(ushort modifierVirtualKey, ushort virtualKey, CancellationToken cancellationToken)
        {
        }

        public void PressKeyChord(
            ushort firstModifierVirtualKey,
            ushort secondModifierVirtualKey,
            ushort virtualKey,
            CancellationToken cancellationToken)
        {
        }

        public void QuitGame(CancellationToken cancellationToken)
        {
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

    private sealed class StubAutomationClock : IAutomationClock
    {
        public DateTime UtcNow { get; } = new(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc);
    }
}
