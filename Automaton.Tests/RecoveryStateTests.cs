using Automaton.Helpers;
using Automaton.MiningStates;
using OpenCvSharp;

namespace Automaton.Tests;

public sealed class RecoveryStateTests
{
    [Fact]
    public void Execute_UndockButtonFound_TransitionsToUnloadCargo()
    {
        // Arrange
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() => SyntheticMiningImageFactory.LoadDockedItemHangarAndMiningHoldVisibleImage()),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var state = new RecoveryState();

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, automationInputController, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.UnloadCargo, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Recover, transition.Action);
        Assert.Equal([60_000], automationInputController.Delays);
    }

    [Fact]
    public void Execute_UndockButtonMissing_TransitionsToDock()
    {
        // Arrange
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() => SyntheticMiningImageFactory.LoadUndockedWithoutLocationChangeTimerImage()),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var state = new RecoveryState();

        // Act
        var transition = state.Execute(
            new MiningAutomationContext(screenCaptureService, automationInputController, new StubAutomationClock()),
            CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.Dock, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Recover, transition.Action);
        Assert.Equal([60_000], automationInputController.Delays);
    }

    private sealed class StubScreenCaptureProvider(Func<Mat> captureFactory)
        : ScreenCaptureService.IScreenCaptureProvider
    {
        public Mat CaptureScreen() => captureFactory();
    }

    private sealed class StubAutomationInputController : IAutomationInputController
    {
        public List<int> Delays { get; } = [];

        public void MoveTo(Point point)
        {
        }

        public void LeftClick(CancellationToken cancellationToken)
        {
        }

        public void PressKey(ushort virtualKey, CancellationToken cancellationToken)
        {
        }

        public void PressKeyChord(ushort modifierVirtualKey, ushort virtualKey, CancellationToken cancellationToken)
        {
        }

        public void PressKeyChord(
            ushort firstModifierVirtualKey,
            ushort secondModifierVirtualKey,
            ushort virtualKey,
            CancellationToken cancellationToken)
        {
        }

        public void QuitGame(CancellationToken cancellationToken)
        {
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
        public DateTime UtcNow { get; } = new(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc);
    }
}
