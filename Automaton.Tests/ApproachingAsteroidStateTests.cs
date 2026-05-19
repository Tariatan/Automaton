using Automaton.MiningStates;
using OpenCvSharp;

namespace Automaton.Tests;

public sealed class ApproachingAsteroidStateTests
{
    private const ushort VirtualKeyA = 0x41;
    private const ushort VirtualKeyControl = 0x11;
    private const ushort VirtualKeyF1 = 0x70;
    private const ushort VirtualKeyF2 = 0x71;
    private const ushort VirtualKeyF4 = 0x73;

    [Fact]
    public void Execute_DistanceSwitchesToMeters_PressesMiningKeysAndTransitionsToMining()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var landedKilometersPath = Path.Combine(workspace.Path, "landed-km.png");
        var landedMetersPath = Path.Combine(workspace.Path, "landed-m.png");
        SyntheticMiningImageFactory.WriteLandedOnAsteroidBeltImage(landedKilometersPath);
        SyntheticMiningImageFactory.WriteLandedOnAsteroidBeltImageWithMetersDistance(landedMetersPath);
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
        Assert.Equal(300, automationInputControllerMock.Delays[0]);
        Assert.Contains(3_000, automationInputControllerMock.Delays);
        Assert.Equal(1_000, automationInputControllerMock.Delays[^1]);
        Assert.Equal(
            new[] { VirtualKeyF4, VirtualKeyA, VirtualKeyControl, VirtualKeyF1, VirtualKeyF2 },
            automationInputControllerMock.KeyInputs);
    }

    [Fact]
    public void Execute_InitialAsteroidListMissing_TransitionsToRecovery()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var landedEmptyPath = Path.Combine(workspace.Path, "landed-empty.png");
        SyntheticMiningImageFactory.WriteLandedOnEmptyAsteroidBeltImage(landedEmptyPath);
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(outputPath => File.Copy(landedEmptyPath, outputPath, overwrite: true)),
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
        Assert.Equal(new[] { VirtualKeyF4 }, automationInputControllerMock.KeyInputs);
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

        public void QuitGame(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        public void Logout(CancellationToken cancellationToken)
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
        public DateTime UtcNow { get; } = new(2026, 5, 11, 12, 0, 0, DateTimeKind.Utc);
    }
}
