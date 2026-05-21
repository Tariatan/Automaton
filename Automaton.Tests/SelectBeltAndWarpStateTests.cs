using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.MiningStates;
using Automaton.Primitives;
using OpenCvSharp;

namespace Automaton.Tests;

public sealed class SelectBeltAndWarpStateTests
{
    private static readonly int[] Expected = [300, 300, 1_000];

    [Fact]
    public void Execute_OverviewHasAsteroidBelts_ClicksBeltTabRandomBeltAndPressesS()
    {
        // Arrange
        var captureInvocationCount = 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() =>
            {
                captureInvocationCount++;
                return captureInvocationCount < 4
                    ? SyntheticMiningImageFactory.LoadWarpToAsteroidFieldImage()
                    : SyntheticMiningImageFactory.LoadLandedOnAsteroidBeltImage();
            }),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var state = new SelectBeltAndWarpState(
            new AsteroidBeltOverviewDetector(),
            new MineOverviewDetector(),
            _ => 1);

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, automationInputController, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.ApproachingAsteroid, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.WarpToAsteroidField, transition.Action);
        Assert.Equal(4, captureInvocationCount);
        Assert.Equal(2, automationInputController.ClickCount);
        Assert.Equal(Expected, automationInputController.Delays);
        Assert.Equal(4, automationInputController.MoveTargets.Count);
        Assert.Equal([VirtualKeys.S], automationInputController.KeyInputs.Select(k => k.VirtualKey));
        Assert.InRange(automationInputController.MoveTargets[0].X, 2270, 2315);
        Assert.InRange(automationInputController.MoveTargets[0].Y, 330, 365);
        AssertMouseParked(automationInputController.MoveTargets[1]);
        Assert.InRange(automationInputController.MoveTargets[2].X, 1990, 2525);
        Assert.InRange(automationInputController.MoveTargets[2].Y, 490, 530);
        AssertMouseParked(automationInputController.MoveTargets[3]);
    }

    [Fact]
    public void Execute_OverviewMissing_TransitionsToRecoveryWithoutClicking()
    {
        // Arrange
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() => SyntheticMiningImageFactory.LoadUndockedCompleteImage()),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var state = new SelectBeltAndWarpState(
            new AsteroidBeltOverviewDetector(),
            new MineOverviewDetector(),
            _ => 0);

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, automationInputController, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.Recovery, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Recover, transition.Action);
        Assert.Equal(0, automationInputController.ClickCount);
        Assert.Empty(automationInputController.MoveTargets);
        Assert.Empty(automationInputController.Delays);
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

    private static void AssertMouseParked(Point point)
    {
        Assert.InRange(point.X, 200, 299);
        Assert.InRange(point.Y, 200, 299);
    }
}
