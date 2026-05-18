using Automaton.MiningStates;
using OpenCvSharp;

namespace Automaton.Tests;

public sealed class DockedStateTests
{
    private const ushort VirtualKeyAlt = 0x12;
    private const ushort VirtualKeyControl = 0x11;
    private const ushort VirtualKeyM = 0x4D;
    private const ushort VirtualKeyG = 0x47;
    private const ushort VirtualKeyA = 0x41;
    private const ushort VirtualKeyX = 0x58;
    private const ushort VirtualKeyV = 0x56;
    private const ushort VirtualKeyC = 0x43;

    [Fact]
    public void Execute_ItemHangarFocused_TransitionsToUndockingWithCommonKeySequence()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var capturePath = Path.Combine(workspace.Path, "docked-item-hangar.png");
        SyntheticMiningImageFactory.WriteDockedItemHangarFocusedImage(capturePath);
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(outputPath => File.Copy(capturePath, outputPath)),
            new SampleImageProcessor());
        var automationInputController = new StubAutomationInputController();
        var state = new UnloadingCargoState();
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
        Assert.Contains(new KeyboardInput(VirtualKeyAlt, null, VirtualKeyM), automationInputController.KeyInputs);
        Assert.Contains(new KeyboardInput(VirtualKeyAlt, null, VirtualKeyG), automationInputController.KeyInputs);
        Assert.Contains(1000, automationInputController.Delays);
    }

    [Fact]
    public void Execute_MiningHoldFocusedEmpty_TransitionsToUndockingAndPerformsTransfer()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var capturePath = Path.Combine(workspace.Path, "docked-empty.png");
        SyntheticMiningImageFactory.WriteDockedMiningHoldFocusedEmptyImage(capturePath);
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(outputPath => File.Copy(capturePath, outputPath)),
            new SampleImageProcessor());
        var automationInputController = new StubAutomationInputController();
        var state = new UnloadingCargoState();
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
        Assert.True(automationInputController.MoveTargets.Count >= 2);
        Assert.True(automationInputController.ClickCount >= 2);
        Assert.Contains(new KeyboardInput(VirtualKeyControl, null, VirtualKeyA), automationInputController.KeyInputs);
        Assert.Contains(new KeyboardInput(VirtualKeyControl, null, VirtualKeyX), automationInputController.KeyInputs);
        Assert.Contains(new KeyboardInput(VirtualKeyControl, null, VirtualKeyV), automationInputController.KeyInputs);
        Assert.Contains(new KeyboardInput(VirtualKeyControl, null, VirtualKeyC), automationInputController.KeyInputs);
        Assert.Contains(new KeyboardInput(VirtualKeyAlt, null, VirtualKeyM), automationInputController.KeyInputs);
        Assert.Contains(new KeyboardInput(VirtualKeyAlt, null, VirtualKeyG), automationInputController.KeyInputs);
        Assert.Contains(1000, automationInputController.Delays);
    }

    [Fact]
    public void Execute_MiningHoldFocusedNotEmpty_TransitionsToUndockingAndPerformsTransfer()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var capturePath = Path.Combine(workspace.Path, "docked-not-empty.png");
        SyntheticMiningImageFactory.WriteDockedMiningHoldFocusedNotEmptyImage(capturePath);
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(outputPath => File.Copy(capturePath, outputPath)),
            new SampleImageProcessor());
        var automationInputController = new StubAutomationInputController();
        var state = new UnloadingCargoState();
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
        Assert.True(automationInputController.MoveTargets.Count >= 2);
        Assert.True(automationInputController.ClickCount >= 2);
        Assert.Contains(new KeyboardInput(VirtualKeyControl, null, VirtualKeyA), automationInputController.KeyInputs);
        Assert.Contains(new KeyboardInput(VirtualKeyControl, null, VirtualKeyX), automationInputController.KeyInputs);
        Assert.Contains(new KeyboardInput(VirtualKeyControl, null, VirtualKeyV), automationInputController.KeyInputs);
        Assert.Contains(new KeyboardInput(VirtualKeyControl, null, VirtualKeyC), automationInputController.KeyInputs);
        Assert.Contains(new KeyboardInput(VirtualKeyAlt, null, VirtualKeyM), automationInputController.KeyInputs);
        Assert.Contains(new KeyboardInput(VirtualKeyAlt, null, VirtualKeyG), automationInputController.KeyInputs);
        Assert.Contains(1000, automationInputController.Delays);
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

        public void Delay(int milliseconds, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delays.Add(milliseconds);
        }
    }

    private readonly record struct KeyboardInput(
        ushort FirstModifierVirtualKey,
        ushort? SecondModifierVirtualKey,
        ushort VirtualKey);

    private sealed class StubAutomationClock : IAutomationClock
    {
        public DateTime UtcNow { get; } = new(2026, 5, 2, 12, 0, 0, DateTimeKind.Utc);
    }
}
