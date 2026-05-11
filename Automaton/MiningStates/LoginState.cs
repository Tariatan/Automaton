using OpenCvSharp;

namespace Automaton.MiningStates;

internal sealed class LoginState : IMiningAutomationState
{
    private const int PilotIndex = 2;
    private const int PilotLoginDelayMilliseconds = 20_000;
    private const int WindowActivationDelayMilliseconds = 2_000;
    private const ushort VirtualKeyControl = 0x11;
    private const ushort VirtualKeyShift = 0x10;
    private const ushort VirtualKeyF9 = 0x78;
    private const ushort VirtualKeyW = 0x57;
    private const string CaptureSuffix = ".mining-login";

    private readonly PilotAvatarLocator m_PilotAvatarLocator;

    public LoginState()
        : this(new PilotAvatarLocator())
    {
    }

    internal LoginState(PilotAvatarLocator pilotAvatarLocator)
    {
        m_PilotAvatarLocator = pilotAvatarLocator;
    }

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.Login;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace($"{CaptureSuffix}-{PilotIndex}");
        cancellationToken.ThrowIfCancellationRequested();

        using var screen = Cv2.ImRead(capturePath);
        if (!m_PilotAvatarLocator.TryLocate(screen, PilotIndex, out var pilotLocation))
        {
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Recover,
                capturePath);
        }

        context.AutomationInputController.MoveTo(Center(pilotLocation.Bounds));
        context.AutomationInputController.LeftClick(cancellationToken);
        context.AutomationInputController.Delay(PilotLoginDelayMilliseconds, cancellationToken);
        context.AutomationInputController.PressKeyChord(VirtualKeyControl, VirtualKeyW, cancellationToken);
        context.AutomationInputController.PressKeyChord(VirtualKeyControl, VirtualKeyShift, VirtualKeyF9, cancellationToken);
        context.AutomationInputController.Delay(WindowActivationDelayMilliseconds, cancellationToken);
        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Docked,
            MiningAutomationActionKind.LoginPilot,
            capturePath);
    }

    private static Point Center(Rect bounds)
    {
        return new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
    }
}
