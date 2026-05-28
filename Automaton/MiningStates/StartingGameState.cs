using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.CommonAutomationStates;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class StartingGameState(
    IAutomationInputController automationInputController,
    PlayNowButtonDetector playNowButtonDetector)
    : IMiningAutomationState
{
    private const string CaptureSuffix = ".mining-starting-game";
    private readonly CommonStartGameState m_CommonStartGameState = new(automationInputController, playNowButtonDetector);

    private readonly ILogger m_Logger = Log.ForContext<StartingGameState>();

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.StartingGame;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        m_Logger.Debug("Executing {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();
        using var capture = context.ScreenCaptureService.CaptureCurrentScreen(CaptureSuffix);
        cancellationToken.ThrowIfCancellationRequested();

        if (!m_CommonStartGameState.TryStartGame(capture.CapturePath, cancellationToken, out _))
        {
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.QuitGameAndExitApplication,
                capture.CapturePath);
        }

        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Login,
            MiningAutomationActionKind.StartGame,
            capture.CapturePath);
    }
}
