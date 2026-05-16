using Automaton.Detectors;
using OpenCvSharp;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class LoginState : IMiningAutomationState
{
    private const int PilotIndex = 2;
    private const int PilotLoginDelayMilliseconds = 20_000;
    private const ushort VirtualKeyControl = 0x11;
    private const ushort VirtualKeyW = 0x57;
    private const ushort VirtualKeyShift = 0x10;
    private const ushort VirtualKeyF9 = 0x78;
    private const string CaptureSuffix = ".mining-login";

    private readonly PilotAvatarLocator m_PilotAvatarLocator;
    private readonly ILogger m_Logger;

    public LoginState()
        : this(new PilotAvatarLocator(), Log.ForContext<LoginState>())
    {
    }

    internal LoginState(PilotAvatarLocator pilotAvatarLocator, ILogger? logger = null)
    {
        m_PilotAvatarLocator = pilotAvatarLocator;
        m_Logger = logger ?? Log.ForContext<LoginState>();
    }

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.Login;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        m_Logger.Debug("Executing {State}", Kind);
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

        // Select miner pilot
        m_Logger.Information("Logging in mining pilot {PilotIndex}...", PilotIndex);

        context.AutomationInputController.MoveTo(Center(pilotLocation.Bounds));
        context.AutomationInputController.LeftClick(cancellationToken);

        // Wait for the full login
        context.AutomationInputController.Delay(PilotLoginDelayMilliseconds, cancellationToken);

        // Close any potential spam window
        m_Logger.Information("Close any potential spam window");
        context.AutomationInputController.PressKeyChord(VirtualKeyControl, VirtualKeyW, cancellationToken);

        // Hide GUI
        m_Logger.Information("Hide UI");
        context.AutomationInputController.PressKeyChord(VirtualKeyControl, VirtualKeyShift, VirtualKeyF9, cancellationToken);

        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.UnloadCargo,
            MiningAutomationActionKind.LoginPilot,
            capturePath);
    }

    private static Point Center(Rect bounds)
    {
        return new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
    }
}
