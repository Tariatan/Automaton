using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.MiningStates;
using Automaton.Primitives;

namespace Automaton.Tests;

public sealed class UnloadingCargoStateTests
{
    [Fact]
    public void Execute_Docked_PerformsTransferAndTransitionsToUndocking()
    {
        // Arrange
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(SyntheticMiningImageFactory.LoadDockedItemHangarAndMiningHoldVisibleImage),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var state = new UnloadingCargoState(automationInputController, new InventoryDetector(), new DowntimeDetector());

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.True(automationInputController.MoveTargets.Count >= 2);
        Assert.True(automationInputController.ClickCount >= 2);
        Assert.Contains(new KeyboardInput(VirtualKeys.Alt, null, VirtualKeys.M), automationInputController.KeyInputs);
        Assert.Contains(new KeyboardInput(VirtualKeys.Alt, null, VirtualKeys.G), automationInputController.KeyInputs);
        Assert.Contains(new KeyboardInput(VirtualKeys.Control, null, VirtualKeys.A), automationInputController.KeyInputs);
        Assert.Contains(new KeyboardInput(VirtualKeys.Control, null, VirtualKeys.X), automationInputController.KeyInputs);
        Assert.Contains(new KeyboardInput(VirtualKeys.Control, null, VirtualKeys.V), automationInputController.KeyInputs);
        Assert.Contains(new KeyboardInput(VirtualKeys.Control, null, VirtualKeys.C), automationInputController.KeyInputs);
        Assert.Contains(new KeyboardInput(VirtualKeys.Control, null, VirtualKeys.V), automationInputController.KeyInputs);
        Assert.Contains(Delays.OpenHoldMs, automationInputController.Delays);

        Assert.Equal(MiningAutomationStateKind.Undocking, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Undock, transition.Action);
    }

    [Fact]
    public void Execute_DowntimeIsImminent_QuitsGameAndRequestsApplicationExit()
    {
        // Arrange
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(SyntheticMiningImageFactory.LoadDockedItemHangarAndMiningHoldVisibleImage),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var state = new UnloadingCargoState(
            automationInputController,
            new InventoryDetector(),
            new DowntimeDetector(new TimeOnly(19, 0), TimeSpan.FromMinutes(20)));

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, new StubAutomationClock(new DateTime(2026, 5, 2, 18, 45, 0, DateTimeKind.Utc))),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.Recovery, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.QuitGameAndExitApplication, transition.Action);
        Assert.True(automationInputController.QuitGameCalled);
    }
}
