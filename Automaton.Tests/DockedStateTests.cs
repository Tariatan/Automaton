using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.MiningStates;
using Automaton.Primitives;

namespace Automaton.Tests;

public sealed class DockedStateTests
{

    [Fact]
    public void Execute_Docked_PerformsTransferAndTransitionsToUndocking()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var capturePath = Path.Combine(workspace.Path, "docked.png");
        SyntheticMiningImageFactory.WriteDockedItemHangarAndMiningHoldVisibleImage(capturePath);
        var screenCaptureService = new Helpers.ScreenCaptureService(
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
        Assert.True(automationInputController.MoveTargets.Count >= 2);
        Assert.True(automationInputController.ClickCount >= 2);
        Assert.Contains(new KeyboardInput(VirtualKeys.Alt, null, VirtualKeys.M), automationInputController.KeyInputs);
        Assert.Contains(new KeyboardInput(VirtualKeys.Alt, null, VirtualKeys.G), automationInputController.KeyInputs);
        Assert.Contains(new KeyboardInput(VirtualKeys.Control, null, VirtualKeys.A), automationInputController.KeyInputs);
        Assert.Contains(new KeyboardInput(VirtualKeys.Control, null, VirtualKeys.X), automationInputController.KeyInputs);
        Assert.Contains(new KeyboardInput(VirtualKeys.Control, null, VirtualKeys.V), automationInputController.KeyInputs);
        Assert.Contains(new KeyboardInput(VirtualKeys.Control, null, VirtualKeys.C), automationInputController.KeyInputs);
        Assert.Contains(1000, automationInputController.Delays);

        Assert.Equal(MiningAutomationStateKind.Undocking, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Undock, transition.Action);
    }

    [Fact]
    public void Execute_DowntimeIsImminent_QuitsGameAndRequestsApplicationExit()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var capturePath = Path.Combine(workspace.Path, "docked.png");
        SyntheticMiningImageFactory.WriteDockedItemHangarAndMiningHoldVisibleImage(capturePath);
        var screenCaptureService = new Helpers.ScreenCaptureService(
            new StubScreenCaptureProvider(outputPath => File.Copy(capturePath, outputPath)),
            new SampleImageProcessor());
        var automationInputController = new StubAutomationInputController();
        var state = new UnloadingCargoState(
            new MiningHoldDetector(),
            new DowntimeDetector(new TimeOnly(19, 0), TimeSpan.FromMinutes(20)));
        MiningAutomationStateTransition transition;

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            transition = state.Execute(
                new MiningAutomationContext(screenCaptureService, automationInputController, new ImminentDowntimeAutomationClock()),
                CancellationToken.None);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.Equal(MiningAutomationStateKind.Recovery, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.QuitGameAndExitApplication, transition.Action);
        Assert.True(automationInputController.QuitGameCalled);
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
        public DateTime UtcNow { get; } = new(2026, 5, 2, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class ImminentDowntimeAutomationClock : IAutomationClock
    {
        public DateTime UtcNow { get; } = new(2026, 5, 2, 18, 45, 0, DateTimeKind.Utc);
    }
}
