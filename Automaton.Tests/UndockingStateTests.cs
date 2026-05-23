using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.MiningStates;
using Automaton.Primitives;

namespace Automaton.Tests;

public sealed class UndockingStateTests
{
    [Fact]
    public void Execute_LocationChangeTimerAppears_TransitionsToEmptyOnUndock()
    {
        // Arrange
        var captureInvocationCount = 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() =>
            {
                captureInvocationCount++;
                return captureInvocationCount switch
                {
                    1 => SyntheticMiningImageFactory.LoadDockedItemHangarAndMiningHoldVisibleImage(),
                    < 4 => SyntheticMiningImageFactory.LoadUndockedWithoutLocationChangeTimerImage(),
                    _ => SyntheticMiningImageFactory.LoadUndockedCompleteImage()
                };
            }),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var state = new UndockingState(automationInputController, new LocationChangeTimerDetector());

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.SelectBeltAndWarp, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.CompleteUndock, transition.Action);
        Assert.Equal(4, captureInvocationCount);
        Assert.Equal(
        [
            Delays.UndockingWindowActivationMs,
            Delays.InitialUndockMs,
            Delays.LocationChangeTimerPollingMs,
            Delays.LocationChangeTimerPollingMs,
        ],
        automationInputController.Delays);
    }

    [Fact]
    public void Execute_LocationChangeTimerMissing_TransitionsToRecovery()
    {
        // Arrange
        var captureInvocationCount = 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() =>
            {
                captureInvocationCount++;
                return captureInvocationCount == 1
                    ? SyntheticMiningImageFactory.LoadDockedItemHangarAndMiningHoldVisibleImage()
                    : SyntheticMiningImageFactory.LoadUndockedWithoutLocationChangeTimerImage();
            }),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var state = new UndockingState(automationInputController, new LocationChangeTimerDetector());

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.Recovery, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.QuitGameFromDock, transition.Action);
        Assert.Equal(16, captureInvocationCount);
        Assert.Equal(17, automationInputController.Delays.Count);
        Assert.Equal(Delays.UndockingWindowActivationMs, automationInputController.Delays[0]);
        Assert.Equal(Delays.InitialUndockMs, automationInputController.Delays[1]);
        Assert.All(automationInputController.Delays.Skip(2), delay => Assert.Equal(Delays.LocationChangeTimerPollingMs, delay));
    }
}
