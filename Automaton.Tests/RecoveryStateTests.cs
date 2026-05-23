using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Infrastructure;
using Automaton.MiningStates;
using Automaton.Primitives;
using OpenCvSharp;

namespace Automaton.Tests;

public sealed class RecoveryStateTests
{
    [Fact]
    public void Execute_HomeStationFound_TransitionsToDock()
    {
        // Arrange
        var beltOverviewDetector = new AsteroidBeltOverviewDetector();
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(SyntheticMiningImageFactory.LoadWarpToAsteroidFieldImage),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var state = new RecoveryState(
            automationInputController,
            beltOverviewDetector,
            new HomeStationDetector(beltOverviewDetector),
            new PlayNowButtonLocator());
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

    [Fact]
    public void Execute_UndockButtonFound_TransitionsToUnloadCargo()
    {
        // Arrange
        var beltOverviewDetector = new AsteroidBeltOverviewDetector();
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(SyntheticMiningImageFactory.LoadDockedItemHangarAndMiningHoldVisibleImage),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var state = new RecoveryState(
            automationInputController,
            beltOverviewDetector,
            new HomeStationDetector(beltOverviewDetector),
            new PlayNowButtonLocator());
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
    public void Execute_PlayNowButtonFound_TransitionsToStartingGame()
    {
        // Arrange
        using var screen = CreatePlayButtonScreen(new Point(260, 340));
        var beltOverviewDetector = new AsteroidBeltOverviewDetector();
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(screen.Clone),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var state = new RecoveryState(
            automationInputController,
            beltOverviewDetector,
            new HomeStationDetector(beltOverviewDetector),
            new PlayNowButtonLocator());
        var context = new MiningAutomationContext(screenCaptureService, new StubAutomationClock());

        // Act
        var transition = state.Execute(context, CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.StartingGame, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Recover, transition.Action);
        Assert.Equal([Delays.RecoveryMs], automationInputController.Delays);
    }

    [Fact]
    public void Execute_HomeStationUndockAndPlayNowMissing_TransitionsToRecovery()
    {
        // Arrange
        using var blankScreen = new Mat(new Size(900, 640), MatType.CV_8UC3, new Scalar(18, 18, 18));
        var beltOverviewDetector = new AsteroidBeltOverviewDetector();
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(blankScreen.Clone),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var state = new RecoveryState(
            automationInputController,
            beltOverviewDetector,
            new HomeStationDetector(beltOverviewDetector),
            new PlayNowButtonLocator());
        var context = new MiningAutomationContext(screenCaptureService, new StubAutomationClock());

        // Act
        var transition = state.Execute(context, CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.Recovery, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.Recover, transition.Action);
        Assert.Equal([Delays.RecoveryMs], automationInputController.Delays);
    }

    [Fact]
    public void Execute_HomeStationMissingAfterQuitGameFromSpace_QuitsGameAndTransitionsToStartingGame()
    {
        // Arrange
        using var blankScreen = new Mat(new Size(900, 640), MatType.CV_8UC3, new Scalar(18, 18, 18));
        var beltOverviewDetector = new AsteroidBeltOverviewDetector();
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(blankScreen.Clone),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var state = new RecoveryState(
            automationInputController,
            beltOverviewDetector,
            new HomeStationDetector(beltOverviewDetector),
            new PlayNowButtonLocator());
        var context = new MiningAutomationContext(screenCaptureService, new StubAutomationClock())
        {
            LastAction = MiningAutomationActionKind.QuitGameFromSpace
        };

        // Act
        var transition = state.Execute(context, CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.StartingGame, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.None, transition.Action);
        Assert.True(automationInputController.QuitGameCalled);
        Assert.Equal([Delays.RecoveryMs, Delays.RecoveryMs], automationInputController.Delays);
    }

    [Fact]
    public void Execute_UndockButtonMissingAfterQuitGameFromDock_QuitsGameAndTransitionsToStartingGame()
    {
        // Arrange
        using var blankScreen = new Mat(new Size(900, 640), MatType.CV_8UC3, new Scalar(18, 18, 18));
        var beltOverviewDetector = new AsteroidBeltOverviewDetector();
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(blankScreen.Clone),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var state = new RecoveryState(
            automationInputController,
            beltOverviewDetector,
            new HomeStationDetector(beltOverviewDetector),
            new PlayNowButtonLocator());
        var context = new MiningAutomationContext(screenCaptureService, new StubAutomationClock())
        {
            LastAction = MiningAutomationActionKind.QuitGameFromDock
        };

        // Act
        var transition = state.Execute(context, CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.StartingGame, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.None, transition.Action);
        Assert.True(automationInputController.QuitGameCalled);
        Assert.Equal([Delays.RecoveryMs, Delays.RecoveryMs], automationInputController.Delays);
    }

    private static Mat CreatePlayButtonScreen(Point playButtonLocation)
    {
        var screen = new Mat(new Size(900, 640), MatType.CV_8UC3, new Scalar(18, 18, 18));
        using var playButton = EmbeddedResourceLoader.LoadMat("play.png");
        using var region = new Mat(screen, new Rect(playButtonLocation.X, playButtonLocation.Y, playButton.Width, playButton.Height));
        playButton.CopyTo(region);
        return screen;
    }
}
