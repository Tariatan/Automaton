using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.MiningStates;
using Automaton.Primitives;
using Automaton.Tests.Stubs;

namespace Automaton.Tests.MiningStates;

public sealed class ApproachingAsteroidStateTests
{
    [Fact]
    public void Kind_Default_ReturnsApproachingAsteroid()
    {
        // Arrange
        var state = new ApproachingAsteroidState(
            new StubAutomationInputController(),
            new StubGameActionService(),
            new MineOverviewDetector(),
            new FirstAsteroidWithinReachDetector());

        // Act
        var kind = state.Kind;

        // Assert
        Assert.Equal(MiningAutomationStateKind.ApproachingAsteroid, kind);
    }

    [Fact]
    public void Execute_MineOverviewNotDetected_TransitionsToRecoveryWithDetectionMiss()
    {
        // Arrange
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(SyntheticCommonImageFactory.LoadPlayButtonScreenImage),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputControllerMock = new StubAutomationInputController();
        var gameActionServiceMock = new StubGameActionService();
        var state = new ApproachingAsteroidState(
            automationInputControllerMock,
            gameActionServiceMock,
            new MineOverviewDetector(),
            new FirstAsteroidWithinReachDetector());

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.ApproachingAsteroid, transition.State);
        Assert.Equal(MiningAutomationStateKind.Recovery, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Recover, transition.Action);
        Assert.Equal(MiningAutomationFailureReason.DetectionMiss, transition.FailureReason);
        Assert.Equal(1, gameActionServiceMock.TogglePropulsionModuleCallCount);
        Assert.Equal(0, automationInputControllerMock.ClickCount);
        Assert.Equal(0, gameActionServiceMock.TriggerTargetApproachCallCount);
        Assert.Equal(0, gameActionServiceMock.TriggerTargetLockCallCount);
        Assert.Equal(0, gameActionServiceMock.ToggleFirstLaserCallCount);
        Assert.Equal(0, gameActionServiceMock.ToggleSecondLaserCallCount);
    }

    [Fact]
    public void Execute_EmptyAsteroidBelt_TransitionsToRecovery()
    {
        // Arrange
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(SyntheticMiningImageFactory.LoadLandedOnEmptyAsteroidBeltImage),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputControllerMock = new StubAutomationInputController();
        var gameActionServiceMock = new StubGameActionService();
        var state = new ApproachingAsteroidState(
            automationInputControllerMock,
            gameActionServiceMock,
            new MineOverviewDetector(),
            new FirstAsteroidWithinReachDetector());

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.ApproachingAsteroid, transition.State);
        Assert.Equal(MiningAutomationStateKind.Recovery, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Recover, transition.Action);
        Assert.Equal(MiningAutomationFailureReason.None, transition.FailureReason);
        Assert.Equal(1, gameActionServiceMock.TogglePropulsionModuleCallCount);
        Assert.Empty(automationInputControllerMock.MoveTargets);
        Assert.Equal(0, automationInputControllerMock.ClickCount);
    }

    [Fact]
    public void Execute_AsteroidWithinReachOnFirstAttempt_TransitionsToMiningImmediately()
    {
        // Arrange
        var detectInvocationCount = 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(SyntheticMiningImageFactory.LoadLandedOnAsteroidBeltImage),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputControllerMock = new StubAutomationInputController();
        var gameActionServiceMock = new StubGameActionService();
        var state = new ApproachingAsteroidState(
            automationInputControllerMock,
            gameActionServiceMock,
            new MineOverviewDetector(),
            new StubFirstAsteroidWithinReachDetector(
                () =>
                {
                    detectInvocationCount++;
                    return new FirstAsteroidWithinReachAnalysis(true, null, null, 0.99, 1.0);
                }));

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.ApproachingAsteroid, transition.State);
        Assert.Equal(MiningAutomationStateKind.Mining, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.ActivateMiningLasers, transition.Action);
        Assert.Equal(MiningAutomationFailureReason.None, transition.FailureReason);
        Assert.NotNull(transition.CapturePath);
        Assert.Equal(1, detectInvocationCount);
        Assert.Equal(1, automationInputControllerMock.ClickCount);
        Assert.Single(automationInputControllerMock.Delays);
        Assert.Equal(Delays.LockAsteroidMs, automationInputControllerMock.Delays[0]);
        Assert.Equal(2, gameActionServiceMock.TogglePropulsionModuleCallCount);
        Assert.Equal(1, gameActionServiceMock.TriggerTargetApproachCallCount);
        Assert.Equal(1, gameActionServiceMock.TriggerTargetLockCallCount);
        Assert.Equal(1, gameActionServiceMock.ToggleFirstLaserCallCount);
        Assert.Equal(1, gameActionServiceMock.ToggleSecondLaserCallCount);
    }

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
        var gameActionServiceMock = new StubGameActionService();
        var firstAsteroidWithinReachDetectorMock = new StubFirstAsteroidWithinReachDetector(
            () =>
            {
                detectInvocationCount++;
                var isWithinReach = detectInvocationCount >= 2;
                return new FirstAsteroidWithinReachAnalysis(isWithinReach, null, null, 0.99, 1.0);
            });
        var state = new ApproachingAsteroidState(
            automationInputControllerMock,
            gameActionServiceMock,
            new MineOverviewDetector(),
            firstAsteroidWithinReachDetectorMock);

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.ApproachingAsteroid, transition.State);
        Assert.Equal(MiningAutomationStateKind.Mining, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.ActivateMiningLasers, transition.Action);
        Assert.Equal(MiningAutomationFailureReason.None, transition.FailureReason);
        Assert.NotNull(transition.CapturePath);
        Assert.Equal(3, captureInvocationCount);
        Assert.Equal(2, detectInvocationCount);
        Assert.Equal(2, automationInputControllerMock.MoveTargets.Count);
        Assert.Equal(1, automationInputControllerMock.ClickCount);
        Assert.Equal(Delays.ApproachAsteroidDistancePollingMs, automationInputControllerMock.Delays[0]);
        Assert.Equal(Delays.LockAsteroidMs, automationInputControllerMock.Delays[1]);
        Assert.Equal(2, gameActionServiceMock.TogglePropulsionModuleCallCount);
        Assert.Equal(1, gameActionServiceMock.TriggerTargetApproachCallCount);
        Assert.Equal(1, gameActionServiceMock.TriggerTargetLockCallCount);
        Assert.Equal(1, gameActionServiceMock.ToggleFirstLaserCallCount);
        Assert.Equal(1, gameActionServiceMock.ToggleSecondLaserCallCount);
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
        var gameActionServiceMock = new StubGameActionService();
        var state = new ApproachingAsteroidState(
            automationInputControllerMock,
            gameActionServiceMock,
            new MineOverviewDetector(),
            new StubFirstAsteroidWithinReachDetector(
                () => new FirstAsteroidWithinReachAnalysis(false, null, null, 0.99, 1.0)));

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.ApproachingAsteroid, transition.State);
        Assert.Equal(MiningAutomationStateKind.Recovery, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Recover, transition.Action);
        Assert.Equal(MiningAutomationFailureReason.DetectionMiss, transition.FailureReason);
        Assert.Equal(2, automationInputControllerMock.ClickCount);
        Assert.Equal(1, gameActionServiceMock.TogglePropulsionModuleCallCount);
        Assert.Equal(2, gameActionServiceMock.TriggerTargetApproachCallCount);
        Assert.Equal(0, gameActionServiceMock.TriggerTargetLockCallCount);
        Assert.Equal(0, gameActionServiceMock.ToggleFirstLaserCallCount);
        Assert.Equal(0, gameActionServiceMock.ToggleSecondLaserCallCount);
    }

    [Fact]
    public void Execute_CancellationRequestedBeforeExecution_ThrowsOperationCanceledException()
    {
        // Arrange
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(SyntheticMiningImageFactory.LoadLandedOnAsteroidBeltImage),
            new SampleImageProcessor(),
            persistCaptures: false);
        var gameActionServiceMock = new StubGameActionService();
        var state = new ApproachingAsteroidState(
            new StubAutomationInputController(),
            gameActionServiceMock,
            new MineOverviewDetector(),
            new FirstAsteroidWithinReachDetector());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        Assert.Throws<OperationCanceledException>(() =>
            state.Execute(
                new MiningAutomationContext(screenCaptureService, new StubAutomationClock()),
                cts.Token));
        Assert.Equal(0, gameActionServiceMock.TogglePropulsionModuleCallCount);
    }

    [Fact]
    public void Execute_CancellationRequestedDuringPolling_ThrowsOperationCanceledException()
    {
        // Arrange
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(SyntheticMiningImageFactory.LoadLandedOnAsteroidBeltImage),
            new SampleImageProcessor(),
            persistCaptures: false);
        using var cts = new CancellationTokenSource();
        var automationInputControllerMock = new StubAutomationInputController
        {
            OnDelay = _ => cts.Cancel()
        };
        var gameActionServiceMock = new StubGameActionService();
        var state = new ApproachingAsteroidState(
            automationInputControllerMock,
            gameActionServiceMock,
            new MineOverviewDetector(),
            new StubFirstAsteroidWithinReachDetector(
                () => new FirstAsteroidWithinReachAnalysis(false, null, null, 0.99, 1.0)));

        // Act & Assert
        Assert.Throws<OperationCanceledException>(() =>
            state.Execute(
                new MiningAutomationContext(screenCaptureService, new StubAutomationClock()),
                cts.Token));
        Assert.Equal(1, gameActionServiceMock.TogglePropulsionModuleCallCount);
        Assert.Equal(1, gameActionServiceMock.TriggerTargetApproachCallCount);
        Assert.Equal(1, automationInputControllerMock.ClickCount);
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