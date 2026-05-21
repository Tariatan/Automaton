using Automaton.Helpers;
using Automaton.MiningStates;
using OpenCvSharp;

namespace Automaton.Tests;

public sealed class UndockingStateTests
{
    private static readonly int[] Expected = [15_000, 1_000, 1_000];

    [Fact]
    public void Execute_LocationChangeTimerAppears_TransitionsToEmptyOnUndock()
    {
        // Arrange
        var captureInvocationCount = 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() =>
            {
                captureInvocationCount++;
                return captureInvocationCount < 3
                    ? SyntheticMiningImageFactory.LoadUndockedWithoutLocationChangeTimerImage()
                    : SyntheticMiningImageFactory.LoadUndockedCompleteImage();
            }),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var state = new UndockingState();

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, automationInputController, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.SelectBeltAndWarp, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.CompleteUndock, transition.Action);
        Assert.Equal(3, captureInvocationCount);
        Assert.Equal(Expected, automationInputController.Delays);
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
                return SyntheticMiningImageFactory.LoadUndockedWithoutLocationChangeTimerImage();
            }),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var state = new UndockingState();

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, automationInputController, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.Recovery, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Recover, transition.Action);
        Assert.Equal(15, captureInvocationCount);
        Assert.Equal(16, automationInputController.Delays.Count);
        Assert.Equal(15_000, automationInputController.Delays[0]);
        Assert.All(automationInputController.Delays.Skip(1), delay => Assert.Equal(1_000, delay));
    }

    private sealed class StubScreenCaptureProvider(Func<Mat> captureFactory)
        : ScreenCaptureService.IScreenCaptureProvider
    {
        public Mat CaptureScreen() => captureFactory();
    }

    private sealed class StubAutomationClock : IAutomationClock
    {
        public DateTime UtcNow { get; } = new(2026, 5, 3, 12, 0, 0, DateTimeKind.Utc);
    }
}
