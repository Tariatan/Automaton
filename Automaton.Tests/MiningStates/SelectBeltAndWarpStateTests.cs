using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.MiningStates;
using Automaton.Primitives;
using Automaton.Tests.Stubs;
using OpenCvSharp;

namespace Automaton.Tests.MiningStates;

public sealed class SelectBeltAndWarpStateTests
{
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
        var gameActionService = new StubGameActionService();
        var state = new SelectBeltAndWarpState(
            automationInputController,
            gameActionService,
            new AsteroidBeltOverviewDetector(),
            new MineOverviewDetector(),
            new WarOverviewDetector(),
            _ => 1);

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.SelectBeltAndWarp, transition.State);
        Assert.Equal(MiningAutomationStateKind.ApproachingAsteroid, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.WarpToAsteroidField, transition.Action);
        Assert.Equal(MiningAutomationFailureReason.None, transition.FailureReason);
        Assert.Equal(4, captureInvocationCount);
        Assert.Equal(2, automationInputController.ClickCount);
        Assert.Equal([Delays.LandingPollingMs], automationInputController.Delays);
        Assert.Equal(4, automationInputController.MoveTargets.Count);
        Assert.Equal(1, gameActionService.WarpToTargetCallCount);
        Assert.InRange(automationInputController.MoveTargets[0].X, 2200, 2320);
        Assert.InRange(automationInputController.MoveTargets[0].Y, 330, 370);
        AssertMouseParked(automationInputController.MoveTargets[1]);
        Assert.InRange(automationInputController.MoveTargets[2].X, 1990, 2525);
        Assert.InRange(automationInputController.MoveTargets[2].Y, 490, 530);
        AssertMouseParked(automationInputController.MoveTargets[3]);
    }

    [Fact]
    public void Execute_OverviewMissing_TransitionsToRecoveryWithoutClicking()
    {
        // Arrange
        using var blankScreen = new Mat(new Size(2560, 1440), MatType.CV_8UC3, new Scalar(18, 18, 18));
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(blankScreen.Clone),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var state = new SelectBeltAndWarpState(
            automationInputController,
            new StubGameActionService(),
            new AsteroidBeltOverviewDetector(),
            new MineOverviewDetector(),
            new WarOverviewDetector(),
            _ => 0);

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.SelectBeltAndWarp, transition.State);
        Assert.Equal(MiningAutomationStateKind.Recovery, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Recover, transition.Action);
        Assert.Equal(MiningAutomationFailureReason.DetectionMiss, transition.FailureReason);
        Assert.Equal(0, automationInputController.ClickCount);
        Assert.Empty(automationInputController.MoveTargets);
        Assert.Empty(automationInputController.Delays);
    }

    private static void AssertMouseParked(Point point)
    {
        Assert.InRange(point.X, 200, 299);
        Assert.InRange(point.Y, 200, 299);
    }

    [Fact]
    public void Execute_LandedAndWarOverviewNotEmpty_TransitionsToDockViaGtfo()
    {
        // Arrange
        var captureInvocationCount = 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() =>
            {
                captureInvocationCount++;
                return captureInvocationCount < 4
                    ? SyntheticMiningImageFactory.LoadWarpToAsteroidFieldImage()
                    : BuildLandedGtfoImage();
            }),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var state = new SelectBeltAndWarpState(
            automationInputController,
            new StubGameActionService(),
            new AsteroidBeltOverviewDetector(),
            new MineOverviewDetector(),
            new WarOverviewDetector(),
            _ => 1);
        var context = new MiningAutomationContext(screenCaptureService, new StubAutomationClock());

        // Act
        var transition = state.Execute(context, CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.Dock, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.None, transition.Action);
        Assert.Equal(1, context.BlacklistedAsteroidBeltCount);
    }

    private static Mat BuildLandedGtfoImage()
    {
        var landedImage = SyntheticMiningImageFactory.LoadLandedOnAsteroidBeltImage();
        using var miningGtfoImage = SyntheticMiningImageFactory.LoadMiningGtfoImage();
        var overlayLeft = Math.Clamp((int)Math.Round(landedImage.Width * 0.62), 0, landedImage.Width - 1);
        var overlayBounds = new Rect(overlayLeft, 0, landedImage.Width - overlayLeft, landedImage.Height);
        using var sourceRegion = new Mat(miningGtfoImage, overlayBounds);
        using var targetRegion = new Mat(landedImage, overlayBounds);
        sourceRegion.CopyTo(targetRegion);
        return landedImage;
    }
}
