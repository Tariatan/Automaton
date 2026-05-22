using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using OpenCvSharp;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class StartingGameState(
    IAutomationInputController automationInputController,
    PlayNowButtonLocator playNowButtonLocator,
    ILogger? logger = null)
    : IMiningAutomationState
{
    private const string CaptureSuffix = ".mining-starting-game";

    private readonly ILogger m_Logger = logger ?? Log.ForContext<StartingGameState>();

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.StartingGame;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        m_Logger.Debug("Executing {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();
        using var capture = context.ScreenCaptureService.CaptureCurrentScreen(CaptureSuffix);
        cancellationToken.ThrowIfCancellationRequested();

        if (!playNowButtonLocator.TryLocate(capture.Image, out var playButtonLocation))
        {
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Recover,
                capture.CapturePath);
        }

        automationInputController.MoveTo(GeometryHelper.Center(playButtonLocation.Bounds));
        automationInputController.LeftClick(cancellationToken);
        automationInputController.Delay(Delays.MiningLauncherStartupMs, cancellationToken);
        automationInputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.W, cancellationToken);
        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Login,
            MiningAutomationActionKind.StartGame,
            capture.CapturePath);
    }

}
