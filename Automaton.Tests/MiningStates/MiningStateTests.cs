using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.MiningStates;
using Automaton.Primitives;
using Automaton.Tests.Stubs;
using OpenCvSharp;

namespace Automaton.Tests.MiningStates;

public sealed class MiningStateTests
{
    [Fact]
    public void Kind_Default_ReturnsMining()
    {
        // Arrange
        var state = new MiningState(
            new StubAutomationInputController(),
            new MiningAsteroidDetector(),
            new MiningLaserDetector(),
            new WarOverviewDetector());

        // Act
        var kind = state.Kind;

        // Assert
        Assert.Equal(MiningAutomationStateKind.Mining, kind);
    }

    [Fact]
    public void Execute_WarOverviewHasNoNothingFound_TransitionsToDockViaGtfo()
    {
        // Arrange
        var captureInvocationCount = 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() =>
            {
                captureInvocationCount++;
                return captureInvocationCount == 1
                    ? SyntheticMiningImageFactory.LoadMiningGtfoImage()
                    : new Mat();
            }),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var state = new MiningState(
            automationInputController,
            new MiningAsteroidDetector(),
            new MiningLaserDetector(),
            new WarOverviewDetector());
        var context = new MiningAutomationContext(screenCaptureService, new StubAutomationClock());
        context.SetCurrentAsteroidBelt(new Rect(2000, 500, 220, 24));

        // Act
        var transition = state.Execute(context, CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.Mining, transition.State);
        Assert.Equal(MiningAutomationStateKind.Dock, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.None, transition.Action);
        Assert.Equal(1, context.BlacklistedAsteroidBeltCount);
    }

    [Fact]
    public void Execute_EmptyImage_TransitionsToRecoveryWithDetectionMiss()
    {
        // Arrange
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() => new Mat()),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var state = new MiningState(
            automationInputController,
            new MiningAsteroidDetector(),
            new MiningLaserDetector(),
            new WarOverviewDetector());
        var context = new MiningAutomationContext(screenCaptureService, new StubAutomationClock());

        // Act
        var transition = state.Execute(context, CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.Mining, transition.State);
        Assert.Equal(MiningAutomationStateKind.Recovery, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Recover, transition.Action);
        Assert.Equal(MiningAutomationFailureReason.DetectionMiss, transition.FailureReason);
        Assert.Equal(Delays.MiningPollingMs, automationInputController.Delays[0]);
    }

    [Fact]
    public void Execute_AsteroidNotDetected_TransitionsToDockWithAsteroidDepleted()
    {
        // Arrange
        using var blankScreen = new Mat(new Size(2560, 2160), MatType.CV_8UC3, new Scalar(18, 18, 18));
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(blankScreen.Clone),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var state = new MiningState(
            automationInputController,
            new MiningAsteroidDetector(),
            new MiningLaserDetector(),
            new WarOverviewDetector());
        var context = new MiningAutomationContext(screenCaptureService, new StubAutomationClock());

        // Act
        var transition = state.Execute(context, CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.Mining, transition.State);
        Assert.Equal(MiningAutomationStateKind.Dock, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.None, transition.Action);
        Assert.Equal(MiningAutomationFailureReason.None, transition.FailureReason);
    }

    [Fact]
    public void Execute_CancellationRequestedDuringPolling_ThrowsOperationCanceledException()
    {
        // Arrange
        using var blankScreen = new Mat(new Size(2560, 2160), MatType.CV_8UC3, new Scalar(18, 18, 18));
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(blankScreen.Clone),
            new SampleImageProcessor(),
            persistCaptures: false);
        using var cts = new CancellationTokenSource();
        var automationInputController = new StubAutomationInputController
        {
            OnDelay = _ => cts.Cancel()
        };
        var state = new MiningState(
            automationInputController,
            new MiningAsteroidDetector(),
            new MiningLaserDetector(),
            new WarOverviewDetector());
        var context = new MiningAutomationContext(screenCaptureService, new StubAutomationClock());

        // Act & Assert
        Assert.Throws<OperationCanceledException>(() => state.Execute(context, cts.Token));
    }
}
