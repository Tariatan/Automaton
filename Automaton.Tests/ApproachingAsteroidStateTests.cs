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
        using var workspace = new TemporaryDirectory();
        var landedKilometersPath = SyntheticMiningImageFactory.GetLandedOnAsteroidBeltImagePath();
        var landedMetersPath = SyntheticMiningImageFactory.GetLandedOnAsteroidBeltImageWithMetersDistancePath();
        var captureInvocationCount = 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(outputPath =>
            {
                captureInvocationCount++;
                var sourcePath = captureInvocationCount < 3
                    ? landedKilometersPath
                    : landedMetersPath;
                File.Copy(sourcePath, outputPath, overwrite: true);
            }),
            new SampleImageProcessor());
        var automationInputControllerMock = new StubAutomationInputController();
        var state = new ApproachingAsteroidState();
        MiningAutomationStateTransition transition;

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            transition = state.Execute(
                new MiningAutomationContext(screenCaptureService, automationInputControllerMock, new StubAutomationClock()),
                CancellationToken.None);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.Equal(MiningAutomationStateKind.Mining, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.ActivateMiningLasers, transition.Action);
        Assert.True(captureInvocationCount >= 2);
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
        using var workspace = new TemporaryDirectory();
        var sourcePath = SyntheticMiningImageFactory.GetLandedOnEmptyAsteroidBeltImagePath();
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(outputPath => File.Copy(sourcePath, outputPath, overwrite: true)),
            new SampleImageProcessor());
        var automationInputControllerMock = new StubAutomationInputController();
        var state = new ApproachingAsteroidState();
        MiningAutomationStateTransition transition;

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            transition = state.Execute(
                new MiningAutomationContext(screenCaptureService, automationInputControllerMock, new StubAutomationClock()),
                CancellationToken.None);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.Equal(MiningAutomationStateKind.Recovery, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Recover, transition.Action);
        Assert.Equal([VirtualKeys.F4], automationInputControllerMock.KeyInputs.Select(k => k.VirtualKey));
        Assert.Empty(automationInputControllerMock.MoveTargets);
        Assert.Equal(0, automationInputControllerMock.ClickCount);
    }

    private sealed class StubScreenCaptureProvider(Action<string> captureAction)
        : ScreenCaptureService.IScreenCaptureProvider
    {
        public void CaptureToFile(string outputPath)
        {
            captureAction(outputPath);
        }
    }

    private sealed class StubAutomationClock : IAutomationClock
    {
        public DateTime UtcNow { get; } = new(2026, 5, 11, 12, 0, 0, DateTimeKind.Utc);
    }
}
