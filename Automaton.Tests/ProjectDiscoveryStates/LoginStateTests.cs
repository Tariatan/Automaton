using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.ProjectDiscoveryStates;
using Automaton.Tests.Stubs;
using OpenCvSharp;

namespace Automaton.Tests.ProjectDiscoveryStates;

[Collection(CurrentDirectorySensitiveCollection.Name)]
public sealed class LoginStateTests
{
    [Fact]
    public void Execute_LoginNextPilotAfterFinalPilot_ClosesGameClientAndReturnsNoFurtherPilotsAvailable()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        using var pilotAvatarDetector = new PilotAvatarDetector();
        using var loggedInPilotDetector = new LoggedInPilotDetector();
        var currentDirectory = Directory.GetCurrentDirectory();
        var captureExistsBeforeClose = false;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() => new Mat(1, 1, MatType.CV_8UC3, Scalar.Black)),
            new SampleImageProcessor());
        var gameActionService = new StubGameActionService
        {
            OnCloseGameClient = () =>
            {
                var capturesDirectory = Path.Combine(workspace.Path, "captures");
                if (!Directory.Exists(capturesDirectory))
                {
                    captureExistsBeforeClose = false;
                    return;
                }

                captureExistsBeforeClose = Directory.EnumerateFiles(
                    capturesDirectory,
                    "*.discovery-no-further-pilots-available.png").Any();
            }
        };
        var state = new LoginState(
            screenCaptureService,
            gameActionService,
            new StubAutomationInputController(),
            pilotAvatarDetector,
            loggedInPilotDetector);
        var context = new ProjectDiscoveryAutomationContext(3)
        {
            LastAction = DiscoveryAutomationActionKind.LoginNextPilot
        };

        // Act
        Directory.SetCurrentDirectory(workspace.Path);
        DiscoveryAutomationStateTransition transition;
        try
        {
            transition = state.Execute(context, CancellationToken.None);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.Equal(DiscoveryAutomationStateKind.Login, transition.State);
        Assert.Equal(DiscoveryAutomationStateKind.Login, transition.NextState);
        Assert.Equal(DiscoveryAutomationActionKind.NoFurtherPilotsAvailable, transition.Action);
        var capturePath = Assert.IsType<string>(transition.CapturePath);
        Assert.EndsWith(".discovery-no-further-pilots-available.png", capturePath);
        Assert.True(File.Exists(Path.Combine(workspace.Path, capturePath)));
        Assert.True(captureExistsBeforeClose);
        Assert.True(gameActionService.CloseGameClientCalled);
        Assert.False(gameActionService.QuitGameCalled);
    }
}
