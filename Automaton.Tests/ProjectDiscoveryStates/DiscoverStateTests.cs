using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.ProjectDiscoveryStates;
using Automaton.Tests.Stubs;
using OpenCvSharp;

namespace Automaton.Tests.ProjectDiscoveryStates;

[Collection(CurrentDirectorySensitiveCollection.Name)]
public sealed class DiscoverStateTests
{
    [Fact]
    public void Execute_DowntimeIsImminent_QuitsGameAndTransitionsToRecoveryWithShutdown()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var currentDirectory = Directory.GetCurrentDirectory();
        var automationInputController = new StubAutomationInputController();
        var gameActionService = new StubGameActionService();
        var automationClock = new StubAutomationClock(new DateTime(2026, 5, 2, 18, 45, 0, DateTimeKind.Utc));
        DiscoveryAutomationStateTransition transition;

        Directory.SetCurrentDirectory(workspace.Path);
        try
        {
            var state = BuildState(
                automationInputController,
                gameActionService,
                automationClock,
                new DowntimeDetector(new TimeOnly(19, 0), TimeSpan.FromMinutes(20)));

            // Act
            transition = state.Execute(
                new ProjectDiscoveryAutomationContext(1, keepDebugImages: false),
                CancellationToken.None);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.Equal(DiscoveryAutomationStateKind.Discover, transition.State);
        Assert.Equal(DiscoveryAutomationStateKind.Recovery, transition.NextState);
        Assert.Equal(DiscoveryAutomationActionKind.Shutdown, transition.Action);
        Assert.True(gameActionService.QuitGameCalled);
    }

    private static DiscoverState BuildState(
        StubAutomationInputController automationInputController,
        StubGameActionService gameActionService,
        StubAutomationClock automationClock,
        DowntimeDetector downtimeDetector)
    {
        var clickTraceRecorder = new ClickTraceRecorder();
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(BuildCaptureSequence()),
            new SampleImageProcessor(),
            clickTraceRecorder);

        return new DiscoverState(
            screenCaptureService,
            automationInputController,
            clickTraceRecorder,
            gameActionService,
            automationClock,
            new MaxSubmissionsPopupDetector(),
            new SlowDownPopupDetector(),
            downtimeDetector);
    }

    private static Func<Mat> BuildCaptureSequence()
    {
        var captureFactories = new Queue<Func<Mat>>([
            SyntheticDiscoveryImageFactory.LoadTwoClusterImage,
            () => ScreenshotLoader.LoadOrSkip("Discovery/submit_enabled.png"),
            () => ScreenshotLoader.LoadOrSkip("Discovery/submit_enabled.png")
        ]);

        return () => captureFactories.Count > 1
            ? captureFactories.Dequeue()()
            : captureFactories.Peek()();
    }
}
