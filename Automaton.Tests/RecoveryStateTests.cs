using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.MiningStates;
using Automaton.Primitives;

namespace Automaton.Tests;

public sealed class RecoveryStateTests
{
    [Fact]
    public void Execute_UndockButtonFound_TransitionsToUnloadCargo()
    {
        // Arrange
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(SyntheticMiningImageFactory.LoadDockedItemHangarAndMiningHoldVisibleImage),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var state = new RecoveryState(automationInputController, new AsteroidBeltOverviewDetector());
        var context = new MiningAutomationContext(screenCaptureService, new StubAutomationClock())
        {
            LastAction = MiningAutomationActionKind.QuitGameFromDock
        };

        // Act
        var transition = state.Execute(context, CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.UnloadCargo, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Recover, transition.Action);
        Assert.Equal([Delays.RecoveryMs], automationInputController.Delays);
    }

    [Fact]
    public void Execute_HomeStationFound_TransitionsToDock()
    {
        // Arrange
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(SyntheticMiningImageFactory.LoadWarpToAsteroidFieldImage),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var state = new RecoveryState(automationInputController, new AsteroidBeltOverviewDetector());
        var context = new MiningAutomationContext(screenCaptureService, new StubAutomationClock())
        {
            LastAction = MiningAutomationActionKind.QuitGameFromSpace
        };

        // Act
        var transition = state.Execute(context, CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.Dock, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Recover, transition.Action);
        Assert.Equal([Delays.RecoveryMs], automationInputController.Delays);
    }
}
