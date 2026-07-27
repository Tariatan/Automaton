using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.MiningStates;
using Automaton.Primitives;
using Automaton.Tests.Stubs;

namespace Automaton.Tests.MiningStates;

public sealed class DockingStateTests
{
    [Fact]
    public void Kind_Default_ReturnsDock()
    {
        // Arrange
        var state = new DockingState(
            new StubAutomationInputController(),
            new StubMiningGameActions(),
            new AsteroidBeltOverviewDetector());

        // Act
        var kind = state.Kind;

        // Assert
        Assert.Equal(MiningAutomationStateKind.Dock, kind);
    }

    [Fact]
    public void Execute_HomeStationFoundAndDockedAfterPolling_TransitionsToUnloadCargo()
    {
        // Arrange
        var captureInvocationCount = 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() =>
            {
                captureInvocationCount++;
                return captureInvocationCount <= 2
                    ? SyntheticMiningImageFactory.LoadWarpToAsteroidFieldImage()
                    : SyntheticMiningImageFactory.LoadDockedItemHangarAndMiningHoldVisibleImage();
            }),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var gameActionService = new StubGameActionService();
        var miningGameActions = new StubMiningGameActions();
        var state = new DockingState(automationInputController, miningGameActions, new AsteroidBeltOverviewDetector());

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.Dock, transition.State);
        Assert.Equal(MiningAutomationStateKind.UnloadCargo, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Dock, transition.Action);
        Assert.Equal(1, miningGameActions.WarpToTargetAndDockCallCount);
        Assert.Equal(1, automationInputController.ClickCount);
        Assert.Contains(MiningDelays.BeforeDockMs, automationInputController.Delays);
        Assert.Contains(MiningDelays.DockedBounceMs, automationInputController.Delays);
    }

    [Fact]
    public void Execute_HomeStationNotFound_TransitionsToRecoveryWithDetectionMiss()
    {
        // Arrange
        using var blankScreen = new OpenCvSharp.Mat(
            new OpenCvSharp.Size(2560, 1440),
            OpenCvSharp.MatType.CV_8UC3,
            new OpenCvSharp.Scalar(18, 18, 18));
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(blankScreen.Clone),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var gameActionService = new StubGameActionService();
        var miningGameActions = new StubMiningGameActions();
        var state = new DockingState(automationInputController, miningGameActions, new AsteroidBeltOverviewDetector());

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.Dock, transition.State);
        Assert.Equal(MiningAutomationStateKind.Recovery, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Recover, transition.Action);
        Assert.Equal(MiningAutomationFailureReason.DetectionMiss, transition.FailureReason);
        Assert.Equal(0, automationInputController.ClickCount);
        Assert.Equal(0, miningGameActions.WarpToTargetAndDockCallCount);
    }

    [Fact]
    public void Execute_CancellationRequestedBeforeExecution_ThrowsOperationCanceledException()
    {
        // Arrange
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(SyntheticMiningImageFactory.LoadWarpToAsteroidFieldImage),
            persistCaptures: false);
        var state = new DockingState(
            new StubAutomationInputController(),
            new StubMiningGameActions(),
            new AsteroidBeltOverviewDetector());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        Assert.Throws<OperationCanceledException>(() =>
            state.Execute(
                new MiningAutomationContext(screenCaptureService, new StubAutomationClock()),
                cts.Token));
    }
}