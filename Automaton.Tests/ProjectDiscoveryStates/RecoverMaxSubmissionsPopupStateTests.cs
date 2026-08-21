using Automaton.Core.Detectors;
using Automaton.Core.Helpers;
using Automaton.Core.Infrastructure;
using Automaton.ProjectDiscoveryStates;
using Automaton.Tests.Stubs;
using OpenCvSharp;

namespace Automaton.Tests.ProjectDiscoveryStates;

[Collection(CurrentDirectorySensitiveCollection.Name)]
public sealed class RecoverMaxSubmissionsPopupStateTests
{
    [Fact]
    public void Kind_Default_ReturnsRecoverMaxSubmissionsPopup()
    {
        // Arrange
        using var pilotAvatarDetector = new PilotAvatarDetector();
        var state = BuildState(
            new StubAutomationInputController(),
            new StubGameActionService(),
            CreateScreenCaptureService(),
            pilotAvatarDetector);

        // Act
        var kind = state.Kind;

        // Assert
        Assert.Equal(DiscoveryAutomationStateKind.RecoverMaxSubmissionsPopup, kind);
    }

    [Fact]
    public void Execute_NextPilotExists_SetsCurrentPilotClosesGameDelaysAndTransitionsToStartingGame()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var automationInputController = new StubAutomationInputController();
        var gameActionService = new StubGameActionService();
        using var pilotAvatarDetector = new PilotAvatarDetector();
        var screenCaptureService = CreateScreenCaptureService();
        var state = BuildState(
            automationInputController,
            gameActionService,
            screenCaptureService,
            pilotAvatarDetector);
        var context = new ProjectDiscoveryAutomationContext(2)
        {
            LastAction = DiscoveryAutomationActionKind.DiscoverAndSubmit
        };
        var originalAvatarDirectory = UserSettings.Default.PilotAvatarDirectory;

        DiscoveryAutomationStateTransition transition;
        try
        {
            UserSettings.Default.PilotAvatarDirectory = Path.Combine(workspace.Path, "avatars");
            var pilotDirectory = AvatarsDirectory.GetDirectory();
            Directory.CreateDirectory(pilotDirectory);
            File.WriteAllText(Path.Combine(pilotDirectory, "3.png"), string.Empty);

            // Act
            transition = state.Execute(context, CancellationToken.None);
        }
        finally
        {
            UserSettings.Default.PilotAvatarDirectory = originalAvatarDirectory;
        }

        // Assert
        Assert.Equal(DiscoveryAutomationStateKind.RecoverMaxSubmissionsPopup, transition.State);
        Assert.Equal(DiscoveryAutomationStateKind.StartingGame, transition.NextState);
        Assert.Equal(DiscoveryAutomationActionKind.RestartGame, transition.Action);
        Assert.Equal(3, context.CurrentPilotIndex);
        Assert.True(gameActionService.CloseGameClientCalled);
        Assert.False(gameActionService.LogoutCalled);
        Assert.False(gameActionService.QuitGameCalled);
        Assert.Equal([120_000], automationInputController.Delays);
    }

    [Fact]
    public void Execute_NoNextPilotExists_LogsOutCapturesFinalPilotScreenAndReturnsNoFurtherPilotsAvailable()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var automationInputController = new StubAutomationInputController();
        var events = new List<string>();
        var gameActionService = new StubGameActionService
        {
            OnLogout = () => events.Add("logout")
        };
        var screenCaptureService = CreateScreenCaptureService(
            () =>
            {
                events.Add("capture");
                return new Mat(1, 1, MatType.CV_8UC3, Scalar.Black);
            });
        using var pilotAvatarDetector = new PilotAvatarDetector();
        var state = BuildState(
            automationInputController,
            gameActionService,
            screenCaptureService,
            pilotAvatarDetector);
        var context = new ProjectDiscoveryAutomationContext(3)
        {
            LastAction = DiscoveryAutomationActionKind.DiscoverAndSubmit
        };
        var originalAvatarDirectory = UserSettings.Default.PilotAvatarDirectory;
        var originalTelemetryRootBase = UserSettings.Default.TelemetryRootBase;
        var currentDirectory = Directory.GetCurrentDirectory();

        DiscoveryAutomationStateTransition transition;
        try
        {
            UserSettings.Default.PilotAvatarDirectory = Path.Combine(workspace.Path, "avatars");
            UserSettings.Default.TelemetryRootBase = string.Empty;
            var pilotDirectory = AvatarsDirectory.GetDirectory();
            Directory.CreateDirectory(pilotDirectory);
            File.WriteAllText(Path.Combine(pilotDirectory, "3.png"), string.Empty);
            Directory.SetCurrentDirectory(workspace.Path);

            // Act
            transition = state.Execute(context, CancellationToken.None);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
            UserSettings.Default.TelemetryRootBase = originalTelemetryRootBase;
            UserSettings.Default.PilotAvatarDirectory = originalAvatarDirectory;
        }

        // Assert
        Assert.Equal(DiscoveryAutomationStateKind.RecoverMaxSubmissionsPopup, transition.State);
        Assert.Equal(DiscoveryAutomationStateKind.RecoverMaxSubmissionsPopup, transition.NextState);
        Assert.Equal(DiscoveryAutomationActionKind.NoFurtherPilotsAvailable, transition.Action);
        var capturePath = Assert.IsType<string>(transition.CapturePath);
        Assert.EndsWith(".discovery-no-further-pilots-available.png", capturePath);
        Assert.True(File.Exists(capturePath));
        Assert.Equal(["logout", "capture"], events);
        Assert.True(gameActionService.LogoutCalled);
        Assert.Equal(1, gameActionService.LogoutCallCount);
        Assert.False(gameActionService.CloseGameClientCalled);
        Assert.False(gameActionService.QuitGameCalled);
        Assert.Empty(automationInputController.Delays);
        Assert.Equal(3, context.CurrentPilotIndex);
    }

    private static RecoverMaxSubmissionsPopupState BuildState(
        StubAutomationInputController automationInputController,
        StubGameActionService gameActionService,
        ScreenCaptureService screenCaptureService,
        PilotAvatarDetector pilotAvatarDetector)
    {
        return new RecoverMaxSubmissionsPopupState(
            gameActionService,
            screenCaptureService,
            pilotAvatarDetector,
            automationInputController);
    }

    private static ScreenCaptureService CreateScreenCaptureService(Func<Mat>? captureScreen = null)
    {
        return new ScreenCaptureService(
            new StubScreenCaptureProvider(captureScreen ?? (() => new Mat(1, 1, MatType.CV_8UC3, Scalar.Black))));
    }
}
