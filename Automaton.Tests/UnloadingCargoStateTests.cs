using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.MiningStates;
using Automaton.Primitives;
using OpenCvSharp;

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

    [Fact]
    public void Execute_MiningHoldWindowMissingOnFirstAttempt_RetriesAndTransitionsToUndocking()
    {
        // Arrange
        var captureInvocationCount = 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() =>
            {
                captureInvocationCount++;
                return captureInvocationCount == 1
                    ? CreateImageWithoutMiningHoldTitle()
                    : SyntheticMiningImageFactory.LoadDockedItemHangarAndMiningHoldVisibleImage();
            }),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var state = new UnloadingCargoState(automationInputController, new InventoryDetector(), new DowntimeDetector());

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.Undocking, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Undock, transition.Action);
        Assert.Equal(4, captureInvocationCount);
        Assert.Equal(2, automationInputController.KeyInputs.Count(input => input is { ModifierVirtualKey: VirtualKeys.Alt, VirtualKey: VirtualKeys.M }));
        Assert.Equal(1, automationInputController.KeyInputs.Count(input => input is { ModifierVirtualKey: VirtualKeys.Alt, VirtualKey: VirtualKeys.G }));
    }

    private static Mat CreateImageWithoutMiningHoldTitle()
    {
        var image = SyntheticMiningImageFactory.LoadDockedItemHangarAndMiningHoldVisibleImage();
        Cv2.Rectangle(image, Settings.MiningHoldBounds, Scalar.Black, -1);
        return image;
    }
}
