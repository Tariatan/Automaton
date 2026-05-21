using Automaton.Detectors;
using Automaton.Primitives;
using OpenCvSharp;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class LoginState : IMiningAutomationState
{
    private const int PilotIndex = 2;
    private const string CaptureSuffix = ".mining-login";

    private readonly ILogger m_Logger;

    public LoginState()
        : this(Log.ForContext<LoginState>())
    {
    }

    private LoginState(ILogger? logger = null)
    {
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
        if (!PilotAvatarLocator.TryLocate(screen, PilotIndex, out var pilotLocation))
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
        context.AutomationInputController.Delay(Delays.MiningPilotLoginMs, cancellationToken);

        // Close any potential spam window
        m_Logger.Information("Close any potential spam window");
        context.AutomationInputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.W, cancellationToken);

        // Hide GUI
        m_Logger.Information("Hide UI");
        context.AutomationInputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.Shift, VirtualKeys.F9, cancellationToken);

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
