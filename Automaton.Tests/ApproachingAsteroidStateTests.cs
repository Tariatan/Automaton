using Automaton.Helpers;
using Automaton.MiningStates;
using Automaton.Primitives;
using OpenCvSharp;

namespace Automaton.Tests;

public sealed class ApproachingAsteroidStateTests
{

    [Fact]
    public void Execute_DistanceSwitchesToMeters_PressesMiningKeysAndTransitionsToMining()
    {
        // Arrange
        var captureInvocationCount = 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() =>
            {
                captureInvocationCount++;
                return captureInvocationCount < 3
                    ? SyntheticMiningImageFactory.LoadLandedOnAsteroidBeltImage()
                    : SyntheticMiningImageFactory.LoadLandedOnAsteroidBeltImageWithMetersDistance();
            }),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputControllerMock = new StubAutomationInputController();
        var state = new ApproachingAsteroidState();

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, automationInputControllerMock, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.Mining, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.ActivateMiningLasers, transition.Action);
        Assert.True(captureInvocationCount >= 2);
        Assert.Equal(2, automationInputControllerMock.MoveTargets.Count);
        Assert.Equal(1, automationInputControllerMock.ClickCount);
        Assert.Equal(Delays.ApproachAsteroidDistancePollingMs, automationInputControllerMock.Delays[0]);
        Assert.Contains(Delays.LockAsteroidMs, automationInputControllerMock.Delays);
        Assert.Equal(
            [VirtualKeys.F4, VirtualKeys.A, VirtualKeys.Control, VirtualKeys.F1, VirtualKeys.F2],
            automationInputControllerMock.KeyInputs.Select(k => k.VirtualKey));
    }

    [Fact]
    public void Execute_InitialAsteroidListMissing_TransitionsToRecovery()
    {
        // Arrange
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() => SyntheticMiningImageFactory.LoadLandedOnEmptyAsteroidBeltImage()),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputControllerMock = new StubAutomationInputController();
        var state = new ApproachingAsteroidState();

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, automationInputControllerMock, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.Recovery, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Recover, transition.Action);
        Assert.Equal([VirtualKeys.F4], automationInputControllerMock.KeyInputs.Select(k => k.VirtualKey));
        Assert.Empty(automationInputControllerMock.MoveTargets);
        Assert.Equal(0, automationInputControllerMock.ClickCount);
    }

    private sealed class StubScreenCaptureProvider(Func<Mat> captureFactory)
        : ScreenCaptureService.IScreenCaptureProvider
    {
        public Mat CaptureScreen() => captureFactory();
    }

    private sealed class StubAutomationClock : IAutomationClock
    {
        public DateTime UtcNow { get; } = new(2026, 5, 11, 12, 0, 0, DateTimeKind.Utc);
    }
}
