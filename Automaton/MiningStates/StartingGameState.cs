using Automaton.Detectors;
using Automaton.Primitives;
using OpenCvSharp;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class StartingGameState : IMiningAutomationState
{
    private const string CaptureSuffix = ".mining-starting-game";

    private readonly PlayNowButtonLocator m_PlayNowButtonLocator;
    private readonly ILogger m_Logger;

    public StartingGameState()
        : this(new PlayNowButtonLocator(), Log.ForContext<StartingGameState>())
    {
    }

    private StartingGameState(PlayNowButtonLocator playNowButtonLocator, ILogger? logger = null)
    {
        m_PlayNowButtonLocator = playNowButtonLocator;
        m_Logger = logger ?? Log.ForContext<StartingGameState>();
    }

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.StartingGame;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        m_Logger.Debug("Executing {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();
        using var capture = context.ScreenCaptureService.CaptureCurrentScreen(CaptureSuffix);
        cancellationToken.ThrowIfCancellationRequested();

        if (!m_PlayNowButtonLocator.TryLocate(capture.Image, out var playButtonLocation))
        {
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Recover,
                capture.CapturePath);
        }

        context.AutomationInputController.MoveTo(Center(playButtonLocation.Bounds));
        context.AutomationInputController.LeftClick(cancellationToken);
        context.AutomationInputController.Delay(Delays.MiningLauncherStartupMs, cancellationToken);
        context.AutomationInputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.W, cancellationToken);
        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Login,
            MiningAutomationActionKind.StartGame,
            capture.CapturePath);
    }

    private static Point Center(Rect bounds)
    {
        return new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
    }
}
