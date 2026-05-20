using Automaton.Detectors;
using Automaton.MiningStates;
using OpenCvSharp;

namespace Automaton.Tests;

public sealed class SelectBeltAndWarpStateTests
{
    private const ushort VirtualKeyS = 0x53;

    [Fact]
    public void Execute_OverviewHasAsteroidBelts_ClicksBeltTabRandomBeltAndPressesS()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var overviewPath = Path.Combine(workspace.Path, "overview.png");
        var landedPath = Path.Combine(workspace.Path, "landed.png");
        SyntheticMiningImageFactory.WriteWarpToAsteroidFieldImage(overviewPath);
        SyntheticMiningImageFactory.WriteLandedOnAsteroidBeltImage(landedPath);
        var captureInvocationCount = 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(outputPath =>
            {
                captureInvocationCount++;
                var sourcePath = captureInvocationCount < 4
                    ? overviewPath
                    : landedPath;
                File.Copy(sourcePath, outputPath, overwrite: true);
            }),
            new SampleImageProcessor());
        var automationInputController = new StubAutomationInputController();
        var state = new SelectBeltAndWarpState(
            new AsteroidBeltOverviewDetector(),
            new AsteroidBeltLandingDetector(),
            new MineOverviewDetector(),
            new NothingFoundDetector(),
            _ => 1);
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
        Assert.Equal(MiningAutomationStateKind.ApproachingAsteroid, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.WarpToAsteroidField, transition.Action);
        Assert.NotNull(transition.AsteroidBeltOverview);
        Assert.NotNull(transition.AsteroidBeltLanding);
        Assert.True(transition.AsteroidBeltLanding!.LandedOnAsteroidBelt);
        Assert.Equal(4, captureInvocationCount);
        Assert.Equal(2, automationInputController.ClickCount);
        Assert.Equal(new[] { 300, 300, 1_000 }, automationInputController.Delays);
        Assert.Equal(4, automationInputController.MoveTargets.Count);
        Assert.Equal(new[] { VirtualKeyS }, automationInputController.KeyInputs.Select(k => k.VirtualKey));
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
        using var workspace = new TemporaryDirectory();
        var undockedPath = Path.Combine(workspace.Path, "undocked.png");
        SyntheticMiningImageFactory.WriteUndockedCompleteImage(undockedPath);
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(outputPath => File.Copy(undockedPath, outputPath, overwrite: true)),
            new SampleImageProcessor());
        var automationInputController = new StubAutomationInputController();
        var state = new SelectBeltAndWarpState(
            new AsteroidBeltOverviewDetector(),
            new AsteroidBeltLandingDetector(),
            new MineOverviewDetector(),
            new NothingFoundDetector(),
            _ => 0);
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
        Assert.Equal(0, automationInputController.ClickCount);
        Assert.Empty(automationInputController.MoveTargets);
        Assert.Empty(automationInputController.Delays);
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
        public DateTime UtcNow { get; } = new(2026, 5, 3, 12, 0, 0, DateTimeKind.Utc);
    }

    private static void AssertMouseParked(Point point)
    {
        Assert.InRange(point.X, 200, 299);
        Assert.InRange(point.Y, 200, 299);
    }
}
