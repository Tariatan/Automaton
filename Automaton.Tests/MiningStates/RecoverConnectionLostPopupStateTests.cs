using Automaton.CommonAutomationStates;
using Automaton.Helpers;
using Automaton.MiningStates;
using Automaton.Primitives;
using Automaton.Tests.Stubs;

namespace Automaton.Tests.MiningStates;

public sealed class RecoverConnectionLostPopupStateTests
{
    [Fact]
    public void Kind_Default_ReturnsRecoverConnectionLostPopup()
    {
        // Arrange
        var automationInputController = new StubAutomationInputController();
        var gameActionService = new StubGameActionService();
        var state = new RecoverConnectionLostPopupState(
            new ConnectionLostPopupRecoveryBehavior(automationInputController, gameActionService));

        // Act
        var kind = state.Kind;

        // Assert
        Assert.Equal(MiningAutomationStateKind.RecoverConnectionLostPopup, kind);
    }

    [Fact]
    public void Execute_Default_PerformsRecoveryAndTransitionsToStartingGame()
    {
        // Arrange
        var automationInputController = new StubAutomationInputController();
        var gameActionService = new StubGameActionService();
        var behavior = new ConnectionLostPopupRecoveryBehavior(automationInputController, gameActionService);
        var state = new RecoverConnectionLostPopupState(behavior);
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() => new OpenCvSharp.Mat(1, 1, OpenCvSharp.MatType.CV_8UC3)),
            new SampleImageProcessor(),
            persistCaptures: false);
        var context = new MiningAutomationContext(screenCaptureService, new StubAutomationClock());

        // Act
        var transition = state.Execute(context, CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.RecoverConnectionLostPopup, transition.State);
        Assert.Equal(MiningAutomationStateKind.StartingGame, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.RecoverConnectionLostPopup, transition.Action);
        Assert.Contains(Delays.ConnectionLostExitMs, automationInputController.Delays);
        Assert.Contains(Delays.RecoveryMs, automationInputController.Delays);
        Assert.True(gameActionService.QuitGameCalled);
    }
}