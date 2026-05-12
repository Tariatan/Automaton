using Automaton.MiningStates;
using OpenCvSharp;

namespace Automaton.Tests;

public sealed class LandedOnAsteroidBeltStateTests
{
    [Fact]
    public void Execute_LandingEvidenceAppearsWithoutNothingFound_TransitionsToApproachingAsteroid()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var warpingPath = Path.Combine(workspace.Path, "warping.png");
        var landedPath = Path.Combine(workspace.Path, "landed.png");
        SyntheticMiningImageFactory.WriteWarpToAsteroidFieldImage(warpingPath);
        SyntheticMiningImageFactory.WriteLandedOnAsteroidBeltImage(landedPath);
        var captureInvocationCount = 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(outputPath =>
            {
                captureInvocationCount++;
                var sourcePath = captureInvocationCount < 3
                    ? warpingPath
                    : landedPath;
                File.Copy(sourcePath, outputPath, overwrite: true);
            }),
            new SampleImageProcessor());
        var automationInputController = new StubAutomationInputController();
        var state = new LandedOnAsteroidBeltState();
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
        Assert.Equal(MiningAutomationActionKind.ApproachAsteroid, transition.Action);
        Assert.NotNull(transition.AsteroidBeltLanding);
        Assert.Equal(3, captureInvocationCount);
        Assert.Equal(new[] { 1_000, 1_000 }, automationInputController.Delays);
        Assert.Empty(automationInputController.MoveTargets);
        Assert.Equal(0, automationInputController.ClickCount);
        Assert.Empty(automationInputController.KeyInputs);
    }

    [Fact]
    public void Execute_LandingEvidenceMissing_TransitionsToRecoveryWithoutClicking()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var warpingPath = Path.Combine(workspace.Path, "warping.png");
        SyntheticMiningImageFactory.WriteWarpToAsteroidFieldImage(warpingPath);
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(outputPath => File.Copy(warpingPath, outputPath, overwrite: true)),
            new SampleImageProcessor());
        var automationInputController = new StubAutomationInputController();
        var state = new LandedOnAsteroidBeltState();
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
        Assert.Equal(60, automationInputController.Delays.Count);
        Assert.All(automationInputController.Delays, delay => Assert.Equal(1_000, delay));
        Assert.Empty(automationInputController.MoveTargets);
        Assert.Equal(0, automationInputController.ClickCount);
        Assert.Empty(automationInputController.KeyInputs);
    }

    [Fact]
    public void Execute_LandedWithEmptyMineOverview_TransitionsToSelectBeltAndWarpWithoutClicking()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var landedPath = Path.Combine(workspace.Path, "landed-empty.png");
        SyntheticMiningImageFactory.WriteLandedOnEmptyAsteroidBeltImage(landedPath);
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(outputPath => File.Copy(landedPath, outputPath, overwrite: true)),
            new SampleImageProcessor());
        var automationInputController = new StubAutomationInputController();
        var state = new LandedOnAsteroidBeltState();
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
        Assert.Equal(MiningAutomationActionKind.None, transition.Action);
        Assert.NotNull(transition.AsteroidBeltLanding);
        Assert.True(transition.AsteroidBeltLanding!.LandedOnAsteroidBelt);
        Assert.NotNull(transition.AsteroidBeltLanding.MineOverviewBounds);
        Assert.True(transition.AsteroidBeltLanding.NothingFoundDetected);
        Assert.Empty(transition.AsteroidBeltLanding.Asteroids);
        Assert.Empty(automationInputController.MoveTargets);
        Assert.Equal(0, automationInputController.ClickCount);
        Assert.Empty(automationInputController.KeyInputs);
    }

    private sealed class StubScreenCaptureProvider(Action<string> captureAction)
        : ScreenCaptureService.IScreenCaptureProvider
    {
        public void CaptureToFile(string outputPath)
        {
            captureAction(outputPath);
        }
    }

    private sealed class StubAutomationInputController : IAutomationInputController
    {
        public List<Point> MoveTargets { get; } = [];

        public List<int> Delays { get; } = [];

        public List<ushort> KeyInputs { get; } = [];

        public int ClickCount { get; private set; }

        public void MoveTo(Point point)
        {
            MoveTargets.Add(point);
        }

        public void LeftClick(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ClickCount++;
        }

        public void PressKey(ushort virtualKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            KeyInputs.Add(virtualKey);
        }

        public void PressKeyChord(ushort modifierVirtualKey, ushort virtualKey, CancellationToken cancellationToken)
        {
        }

        public void PressKeyChord(
            ushort firstModifierVirtualKey,
            ushort secondModifierKey,
            ushort virtualKey,
            CancellationToken cancellationToken)
        {
        }

        public void Delay(int milliseconds, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delays.Add(milliseconds);
        }
    }

    private sealed class StubAutomationClock : IAutomationClock
    {
        public DateTime UtcNow { get; } = new(2026, 5, 5, 12, 0, 0, DateTimeKind.Utc);
    }
}
