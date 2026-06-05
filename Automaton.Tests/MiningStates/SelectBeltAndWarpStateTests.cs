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
    public void Execute_OverviewHasAsteroidBelts_ClicksBeltTabAndWarpToRandomBelt()
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
    public void Execute_BeltOverviewMissing_TransitionsToRecoveryWithoutClicking()
    {
        // Arrange
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(SyntheticMiningImageFactory.LoadUndockedWithoutBeltOverviewImage),
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
    public void Execute_HomeStationMissing_QuitsGameAndTransitionsToRecovery()
    {
        // Arrange
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(SyntheticMiningImageFactory.LoadInSpaceBeltOverviewWithoutHomeStationImage),
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
            _ => 0);

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.SelectBeltAndWarp, transition.State);
        Assert.Equal(MiningAutomationStateKind.Recovery, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Recover, transition.Action);
        Assert.Equal(MiningAutomationFailureReason.None, transition.FailureReason);
        Assert.True(gameActionService.QuitGameCalled);
        Assert.Equal(0, automationInputController.ClickCount);
    }

    [Fact]
    public void Execute_DefaultOverviewActive_ClicksBeltTabAndWarpsToRandomBelt()
    {
        // Arrange
        var captureInvocationCount = 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() =>
            {
                captureInvocationCount++;
                return captureInvocationCount == 1
                    ? SyntheticMiningImageFactory.LoadInSpaceWithDefaultOverviewActiveImage()
                    : captureInvocationCount < 4
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
        Assert.Equal(1, gameActionService.WarpToTargetCallCount);
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
                    : SyntheticMiningImageFactory.LoadLandedOnBusyAsteroidBeltImage();
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
}
