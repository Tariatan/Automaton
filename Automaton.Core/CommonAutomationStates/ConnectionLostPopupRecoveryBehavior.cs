using Automaton.Helpers;
using Automaton.Primitives;
using Serilog;

namespace Automaton.CommonAutomationStates;

internal sealed class ConnectionLostPopupRecoveryBehavior(
    IAutomationInputController automationInputController,
    IGameActionService gameActionService)
{
    public void Execute(object detectionStage, ILogger logger, CancellationToken cancellationToken)
    {
        logger.Error("Connection Lost popup detected during {DetectionStage}", detectionStage);
        automationInputController.Delay(Delays.ConnectionLostExitMs, cancellationToken);
        gameActionService.QuitGame(cancellationToken);

        var delay = TimeSpan.FromMilliseconds(Delays.RecoveryMs);
        logger.Error("Waiting for {DelaySeconds:0.###} seconds to recover", delay.TotalSeconds);
        automationInputController.Delay(delay, cancellationToken);
    }
}
