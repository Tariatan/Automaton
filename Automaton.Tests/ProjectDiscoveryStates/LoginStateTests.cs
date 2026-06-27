using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.ProjectDiscoveryStates;
using Automaton.Tests.Stubs;
using OpenCvSharp;

namespace Automaton.Tests.ProjectDiscoveryStates;

public sealed class LoginStateTests
{
    [Fact]
    public void Execute_LoginNextPilotAfterFinalPilot_ClosesGameClientAndReturnsNoFurtherPilotsAvailable()
    {
        // Arrange
        using var pilotAvatarDetector = new PilotAvatarDetector();
        using var loggedInPilotDetector = new LoggedInPilotDetector();
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() => new Mat(1, 1, MatType.CV_8UC3, Scalar.Black)),
            new SampleImageProcessor(),
            persistCaptures: false);
        var gameActionService = new StubGameActionService();
        var state = new LoginState(
            screenCaptureService,
            gameActionService,
            new StubAutomationInputController(),
            pilotAvatarDetector,
            loggedInPilotDetector);
        var context = new ProjectDiscoveryAutomationContext(3, keepDebugImages: false)
        {
            LastAction = DiscoveryAutomationActionKind.LoginNextPilot
        };

        // Act
        var transition = state.Execute(context, CancellationToken.None);

        // Assert
        Assert.Equal(DiscoveryAutomationStateKind.Login, transition.State);
        Assert.Equal(DiscoveryAutomationStateKind.Login, transition.NextState);
        Assert.Equal(DiscoveryAutomationActionKind.NoFurtherPilotsAvailable, transition.Action);
        Assert.True(gameActionService.CloseGameClientCalled);
        Assert.False(gameActionService.QuitGameCalled);
    }
}
