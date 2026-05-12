using Automaton.MiningStates;
using OpenCvSharp;

namespace Automaton.Tests;

public sealed class DockedStateTests
{
    private const ushort VirtualKeyControl = 0x11;
    private const ushort VirtualKeyShift = 0x10;
    private const ushort VirtualKeyF9 = 0x78;

    [Fact]
    public void Execute_ItemHangarFocused_ClicksMiningHoldAndStaysDocked()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var capturePath = Path.Combine(workspace.Path, "docked-item-hangar.png");
        SyntheticMiningImageFactory.WriteDockedItemHangarFocusedImage(capturePath);
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(outputPath => File.Copy(capturePath, outputPath)),
            new SampleImageProcessor());
        var automationInputController = new StubAutomationInputController();
        var state = new DockedState();
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
        Assert.Equal(MiningAutomationStateKind.Docked, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.FocusMiningHold, transition.Action);
        Assert.Equal(2, automationInputController.MoveTargets.Count);
        Assert.Equal(1, automationInputController.ClickCount);
        Assert.Equal(new[] { 300 }, automationInputController.Delays);
        Assert.NotNull(transition.DockedScreen?.MiningHoldEntryBounds);
        AssertPointInside(automationInputController.MoveTargets[0], transition.DockedScreen.MiningHoldEntryBounds!.Value);
        AssertMouseParked(automationInputController.MoveTargets[1]);
    }

    [Fact]
    public void Execute_MiningHoldFocusedEmpty_ClicksUndockAndTransitionsToUndocking()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var capturePath = Path.Combine(workspace.Path, "docked-empty.png");
        SyntheticMiningImageFactory.WriteDockedMiningHoldFocusedEmptyImage(capturePath);
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(outputPath => File.Copy(capturePath, outputPath)),
            new SampleImageProcessor());
        var automationInputController = new StubAutomationInputController();
        var state = new DockedState();
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
        Assert.Equal(MiningAutomationStateKind.Undocking, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Undock, transition.Action);
        Assert.Equal(2, automationInputController.MoveTargets.Count);
        Assert.Equal(1, automationInputController.ClickCount);
        Assert.Single(automationInputController.KeyInputs);
        Assert.Equal(new KeyboardInput(VirtualKeyControl, VirtualKeyShift, VirtualKeyF9), automationInputController.KeyInputs[0]);
        Assert.Equal(new[] { 300 }, automationInputController.Delays);
        Assert.NotNull(transition.DockedScreen?.UndockButtonBounds);
        AssertPointInside(automationInputController.MoveTargets[0], transition.DockedScreen.UndockButtonBounds!.Value);
        AssertMouseParked(automationInputController.MoveTargets[1]);
    }

    [Fact]
    public void Execute_MiningHoldFocusedNotEmpty_TransitionsToUnloadCargoWithoutClicking()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var capturePath = Path.Combine(workspace.Path, "docked-not-empty.png");
        SyntheticMiningImageFactory.WriteDockedMiningHoldFocusedNotEmptyImage(capturePath);
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(outputPath => File.Copy(capturePath, outputPath)),
            new SampleImageProcessor());
        var automationInputController = new StubAutomationInputController();
        var state = new DockedState();
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
        Assert.Equal(MiningAutomationActionKind.UnloadCargo, transition.Action);
        Assert.Empty(automationInputController.MoveTargets);
        Assert.Equal(0, automationInputController.ClickCount);
        Assert.Empty(automationInputController.KeyInputs);
        Assert.Empty(automationInputController.Delays);
    }

    private static void AssertPointInside(Point point, Rect bounds)
    {
        Assert.InRange(point.X, bounds.Left, bounds.Right - 1);
        Assert.InRange(point.Y, bounds.Top, bounds.Bottom - 1);
    }

    private static void AssertMouseParked(Point point)
    {
        Assert.InRange(point.X, 200, 299);
        Assert.InRange(point.Y, 200, 299);
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

        public void Delay(int milliseconds, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delays.Add(milliseconds);
        }
    }

    private readonly record struct KeyboardInput(
        ushort FirstModifierVirtualKey,
        ushort SecondModifierVirtualKey,
        ushort VirtualKey);

    private sealed class StubAutomationClock : IAutomationClock
    {
        public DateTime UtcNow { get; } = new(2026, 5, 2, 12, 0, 0, DateTimeKind.Utc);
    }
}
