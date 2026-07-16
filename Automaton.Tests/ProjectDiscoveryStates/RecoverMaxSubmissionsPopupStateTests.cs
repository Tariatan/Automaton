using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using Automaton.ProjectDiscoveryStates;
using Automaton.Tests.Stubs;
using OpenCvSharp;

namespace Automaton.Tests.ProjectDiscoveryStates;

public sealed class RecoverMaxSubmissionsPopupStateTests
{
    [Fact]
    public void Kind_Default_ReturnsRecoverMaxSubmissionsPopup()
    {
        // Arrange
        using var pilotAvatarDetector = new PilotAvatarDetector();
        var state = BuildState(new StubAutomationInputController(), new StubGameActionService(), pilotAvatarDetector);

        // Act
        var kind = state.Kind;

        // Assert
        Assert.Equal(DiscoveryAutomationStateKind.RecoverMaxSubmissionsPopup, kind);
    }

    [Fact]
    public void Execute_MaxSubmissionsPopupDetected_LogsOutDelaysAndTransitionsToLogin()
    {
        // Arrange
        var automationInputController = new StubAutomationInputController();
        var gameActionService = new StubGameActionService();
        using var pilotAvatarDetector = new PilotAvatarDetector();
        var state = BuildState(automationInputController, gameActionService, pilotAvatarDetector);
        var context = new ProjectDiscoveryAutomationContext(2)
        {
            LastAction = DiscoveryAutomationActionKind.DiscoverAndSubmit
        };

        // Act
        var transition = state.Execute(context, CancellationToken.None);

        // Assert
        Assert.Equal(DiscoveryAutomationStateKind.RecoverMaxSubmissionsPopup, transition.State);
        Assert.Equal(DiscoveryAutomationStateKind.Login, transition.NextState);
        Assert.Equal(DiscoveryAutomationActionKind.LoginNextPilot, transition.Action);
        Assert.True(gameActionService.LogoutCalled);
        Assert.Equal(1, gameActionService.LogoutCallCount);
    }

    private static RecoverMaxSubmissionsPopupState BuildState(
        StubAutomationInputController automationInputController,
        StubGameActionService gameActionService,
        PilotAvatarDetector pilotAvatarDetector)
    {
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() => new Mat(1, 1, MatType.CV_8UC3, Scalar.Black)),
            new SampleImageProcessor(),
            persistCaptures: false);

        return new RecoverMaxSubmissionsPopupState(
            automationInputController,
            gameActionService,
            screenCaptureService,
            pilotAvatarDetector);
    }
}
