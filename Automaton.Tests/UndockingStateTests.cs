using Automaton.Helpers;
using Automaton.MiningStates;
using OpenCvSharp;

namespace Automaton.Tests;

public sealed class UndockingStateTests
{
    private static readonly int[] Expected = [15_000, 1_000, 1_000];

    [Fact]
    public void Execute_LocationChangeTimerAppears_TransitionsToEmptyOnUndock()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var undockedPath = Path.Combine(workspace.Path, "undocked.png");
        var undockedCompletePath = Path.Combine(workspace.Path, "undocked-complete.png");
        WriteUndockedImage(undockedPath);
        SyntheticMiningImageFactory.WriteUndockedCompleteImage(undockedCompletePath);
        var captureInvocationCount = 0;
        var screenCaptureService = new Helpers.ScreenCaptureService(
            new StubScreenCaptureProvider(outputPath =>
            {
                captureInvocationCount++;
                var sourcePath = captureInvocationCount < 3
                    ? undockedPath
                    : undockedCompletePath;
                File.Copy(sourcePath, outputPath, overwrite: true);
            }),
            new SampleImageProcessor());
        var automationInputController = new StubAutomationInputController();
        var state = new UndockingState();
        MiningAutomationStateTransition transition;

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            transition = state.Execute(
                new MiningAutomationContext(screenCaptureService, automationInputController, new StubAutomationClock()),
                CancellationToken.None);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.Equal(MiningAutomationStateKind.SelectBeltAndWarp, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.CompleteUndock, transition.Action);
        Assert.Equal(3, captureInvocationCount);
        Assert.Equal(Expected, automationInputController.Delays);
    }

    [Fact]
    public void Execute_LocationChangeTimerMissing_TransitionsToRecovery()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var undockedPath = Path.Combine(workspace.Path, "undocked.png");
        WriteUndockedImage(undockedPath);
        var captureInvocationCount = 0;
        var screenCaptureService = new Helpers.ScreenCaptureService(
            new StubScreenCaptureProvider(outputPath =>
            {
                captureInvocationCount++;
                File.Copy(undockedPath, outputPath, overwrite: true);
            }),
            new SampleImageProcessor());
        var automationInputController = new StubAutomationInputController();
        var state = new UndockingState();
        MiningAutomationStateTransition transition;

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            transition = state.Execute(
                new MiningAutomationContext(screenCaptureService, automationInputController, new StubAutomationClock()),
                CancellationToken.None);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.Equal(MiningAutomationStateKind.Recovery, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Recover, transition.Action);
        Assert.Equal(15, captureInvocationCount);
        Assert.Equal(16, automationInputController.Delays.Count);
        Assert.Equal(15_000, automationInputController.Delays[0]);
        Assert.All(automationInputController.Delays.Skip(1), delay => Assert.Equal(1_000, delay));
    }

    private static void WriteUndockedImage(string outputPath)
    {
        using var image = SyntheticMiningImageFactory.CreateUndockedWithoutLocationChangeTimerImage();
        Cv2.ImWrite(outputPath, image);
    }

    private sealed class StubScreenCaptureProvider(Action<string> captureAction)
        : Helpers.ScreenCaptureService.IScreenCaptureProvider
    {
        public void CaptureToFile(string outputPath)
        {
            captureAction(outputPath);
        }
    }

    private sealed class StubAutomationClock : IAutomationClock
    {
        public DateTime UtcNow { get; } = new(2026, 5, 3, 12, 0, 0, DateTimeKind.Utc);
    }
}
