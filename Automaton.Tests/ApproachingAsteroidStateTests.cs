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
        var firstAsteroidWithinReachDetector = new StubFirstAsteroidWithinReachDetector(
            (OpenCvSharp.Mat _, OpenCvSharp.Rect _, OpenCvSharp.Rect _, out DistanceUnitDetectionTelemetry telemetry) =>
            {
                detectInvocationCount++;
                telemetry = new DistanceUnitDetectionTelemetry(null, null, 0.99, 1.0, detectInvocationCount >= 2);
                return detectInvocationCount >= 2;
            });
        var state = new ApproachingAsteroidState(
            automationInputControllerMock,
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
        Assert.Equal(
            [VirtualKeys.F4, VirtualKeys.A, VirtualKeys.Control, VirtualKeys.F1, VirtualKeys.F2],
            automationInputControllerMock.KeyInputs.Select(k => k.VirtualKey));
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
        var state = new ApproachingAsteroidState(automationInputControllerMock, new MineOverviewDetector(), new FirstAsteroidWithinReachDetector());

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.Recovery, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Recover, transition.Action);
        Assert.Equal([VirtualKeys.F4], automationInputControllerMock.KeyInputs.Select(k => k.VirtualKey));
        Assert.Empty(automationInputControllerMock.MoveTargets);
        Assert.Equal(0, automationInputControllerMock.ClickCount);
    }

    private sealed class StubFirstAsteroidWithinReachDetector(
        StubFirstAsteroidWithinReachDetector.DetectWithTelemetryHandler detectWithTelemetry)
        : IFirstAsteroidWithinReachDetector
    {
        public bool Detect(OpenCvSharp.Mat screen, OpenCvSharp.Rect mineOverviewBounds, OpenCvSharp.Rect firstAsteroidRowBounds, bool drawDebugOverlay = true)
        {
            return Detect(screen, mineOverviewBounds, firstAsteroidRowBounds, out _, drawDebugOverlay);
        }

        public bool Detect(
            OpenCvSharp.Mat screen,
            OpenCvSharp.Rect mineOverviewBounds,
            OpenCvSharp.Rect firstAsteroidRowBounds,
            out DistanceUnitDetectionTelemetry telemetry,
            bool drawDebugOverlay = true)
        {
            return detectWithTelemetry(screen, mineOverviewBounds, firstAsteroidRowBounds, out telemetry);
        }

        internal delegate bool DetectWithTelemetryHandler(
            OpenCvSharp.Mat screen,
            OpenCvSharp.Rect mineOverviewBounds,
            OpenCvSharp.Rect firstAsteroidRowBounds,
            out DistanceUnitDetectionTelemetry telemetry);
    }
}
