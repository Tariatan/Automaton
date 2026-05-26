using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.MiningStates;
using OpenCvSharp;

namespace Automaton.Tests;

public sealed class MiningStateTests
{
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
        Assert.Equal(MiningAutomationStateKind.Dock, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.None, transition.Action);
        Assert.Equal(1, context.BlacklistedAsteroidBeltCount);
    }
}
