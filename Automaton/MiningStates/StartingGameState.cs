using OpenCvSharp;

namespace Automaton.MiningStates;

internal sealed class StartingGameState : IMiningAutomationState
{
    private const int LauncherStartupDelayMilliseconds = 20_000;
    private const ushort VirtualKeyControl = 0x11;
    private const ushort VirtualKeyW = 0x57;
    private const string CaptureSuffix = ".mining-starting-game";

    private readonly PlayNowButtonLocator m_PlayNowButtonLocator;

    public StartingGameState()
        : this(new PlayNowButtonLocator())
    {
    }

    internal StartingGameState(PlayNowButtonLocator playNowButtonLocator)
    {
        m_PlayNowButtonLocator = playNowButtonLocator;
    }

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.StartingGame;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(CaptureSuffix);
        cancellationToken.ThrowIfCancellationRequested();

        using var screen = Cv2.ImRead(capturePath);
        if (!m_PlayNowButtonLocator.TryLocate(screen, out var playButtonLocation))
        {
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Recover,
                capturePath);
        }

        context.AutomationInputController.MoveTo(Center(playButtonLocation.Bounds));
        context.AutomationInputController.LeftClick(cancellationToken);
        context.AutomationInputController.Delay(LauncherStartupDelayMilliseconds, cancellationToken);
        context.AutomationInputController.PressKeyChord(VirtualKeyControl, VirtualKeyW, cancellationToken);
        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Login,
            MiningAutomationActionKind.StartGame,
            capturePath);
    }

    private static Point Center(Rect bounds)
    {
        return new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
    }
}
