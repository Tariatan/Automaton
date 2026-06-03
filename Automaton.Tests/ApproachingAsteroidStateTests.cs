using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.MiningStates;
using Automaton.Primitives;

namespace Automaton.Tests;

public sealed class ApproachingAsteroidStateTests
{

    [Fact]
    public void Execute_DistanceSwitchesToMeters_PressesMiningKeysAndTransitionsToMining()
    {
        // Arrange
        var captureInvocationCount = 0;
        var detectInvocationCount = 0;
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
        var gameActionService = new StubGameActionService();
        var firstAsteroidWithinReachDetector = new StubFirstAsteroidWithinReachDetector(
            () =>
            {
                detectInvocationCount++;
                var isWithinReach = detectInvocationCount >= 2;
                return new FirstAsteroidWithinReachAnalysis(isWithinReach, null, null, 0.99, 1.0);
            });
        var state = new ApproachingAsteroidState(
            automationInputControllerMock,
            gameActionService,
            new MineOverviewDetector(),
            firstAsteroidWithinReachDetector);

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.Mining, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.ActivateMiningLasers, transition.Action);
        Assert.True(captureInvocationCount >= 2);
        Assert.Equal(2, detectInvocationCount);
        Assert.Equal(2, automationInputControllerMock.MoveTargets.Count);
        Assert.Equal(1, automationInputControllerMock.ClickCount);
        Assert.Equal(Delays.ApproachAsteroidDistancePollingMs, automationInputControllerMock.Delays[0]);
        Assert.Contains(Delays.LockAsteroidMs, automationInputControllerMock.Delays);
        Assert.Equal(2, gameActionService.TogglePropulsionModuleCallCount);
        Assert.Equal(1, gameActionService.TriggerTargetApproachCallCount);
        Assert.Equal(1, gameActionService.TriggerTargetLockCallCount);
        Assert.Equal(1, gameActionService.ToggleFirstLaserCallCount);
        Assert.Equal(1, gameActionService.ToggleSecondLaserCallCount);
    }

    [Fact]
    public void Execute_InitialAsteroidListMissing_TransitionsToRecovery()
    {
        // Arrange
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(SyntheticMiningImageFactory.LoadLandedOnEmptyAsteroidBeltImage),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputControllerMock = new StubAutomationInputController();
        var gameActionService = new StubGameActionService();
        var state = new ApproachingAsteroidState(automationInputControllerMock, gameActionService, new MineOverviewDetector(), new FirstAsteroidWithinReachDetector());

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.Recovery, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Recover, transition.Action);
        Assert.Equal(1, gameActionService.TogglePropulsionModuleCallCount);
        Assert.Empty(automationInputControllerMock.MoveTargets);
        Assert.Equal(0, automationInputControllerMock.ClickCount);
    }

    [Fact]
    public void Execute_AsteroidNeverWithinReach_RetriesApproachAtHalfAttempts()
    {
        // Arrange
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(SyntheticMiningImageFactory.LoadLandedOnAsteroidBeltImage),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputControllerMock = new StubAutomationInputController();
        var gameActionService = new StubGameActionService();
        var state = new ApproachingAsteroidState(
            automationInputControllerMock,
            gameActionService,
            new MineOverviewDetector(),
            new StubFirstAsteroidWithinReachDetector(
                () => new FirstAsteroidWithinReachAnalysis(false, null, null, 0.99, 1.0)));

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.Recovery, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Recover, transition.Action);
        Assert.Equal(2, automationInputControllerMock.ClickCount);
        Assert.Equal(1, gameActionService.TogglePropulsionModuleCallCount);
        Assert.Equal(2, gameActionService.TriggerTargetApproachCallCount);
    }

    private sealed class StubFirstAsteroidWithinReachDetector(
        Func<FirstAsteroidWithinReachAnalysis> detectHandler)
        : FirstAsteroidWithinReachDetector
    {
        public override FirstAsteroidWithinReachAnalysis Detect(
            OpenCvSharp.Mat screen,
            OpenCvSharp.Rect mineOverviewBounds,
            OpenCvSharp.Rect firstAsteroidRowBounds)
        {
            return detectHandler();
        }
    }
}
