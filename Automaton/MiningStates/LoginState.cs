using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using OpenCvSharp;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class LoginState(IAutomationInputController automationInputController, ILogger? logger = null)
    : IMiningAutomationState
{
    private const int PilotIndex = 2;
    private const string CaptureSuffix = ".mining-login";

    private readonly ILogger m_Logger = logger ?? Log.ForContext<LoginState>();

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.Login;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        m_Logger.Debug("Executing {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();
        using var capture = context.ScreenCaptureService.CaptureCurrentScreen($"{CaptureSuffix}-{PilotIndex}");
        cancellationToken.ThrowIfCancellationRequested();

        if (!PilotAvatarLocator.TryLocate(capture.Image, PilotIndex, out var pilotLocation))
        {
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Recover,
                capture.CapturePath);
        }

        // Select miner pilot
        m_Logger.Information("Logging in mining pilot {PilotIndex}...", PilotIndex);

        automationInputController.MoveTo(Center(pilotLocation.Bounds));
        automationInputController.LeftClick(cancellationToken);

        // Wait for the full login
        automationInputController.Delay(Delays.MiningPilotLoginMs, cancellationToken);

        // Close any potential spam window
        m_Logger.Information("Close any potential spam window");
        automationInputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.W, cancellationToken);

        // Hide GUI
        m_Logger.Information("Hide UI");
        automationInputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.Shift, VirtualKeys.F9, cancellationToken);

        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.UnloadCargo,
            MiningAutomationActionKind.LoginPilot,
            capture.CapturePath);
    }

    private static Point Center(Rect bounds)
    {
        return new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
    }
}
