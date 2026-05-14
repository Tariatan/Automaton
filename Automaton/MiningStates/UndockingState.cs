using Automaton.Detectors;
using OpenCvSharp;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class UndockingState : IMiningAutomationState
{
    private const int InitialUndockDelayMilliseconds = 15_000;
    private const int LocationChangeTimerPollingMilliseconds = 1_000;
    private const int LocationChangeTimerPollingAttemptCount = 15;
    private const int WindowActivationDelayMilliseconds = 2_000;
    private const string CaptureSuffix = ".mining-undocking";
    private const ushort VirtualKeyControl = 0x11;
    private const ushort VirtualKeyShift = 0x10;
    private const ushort VirtualKeyF9 = 0x78;

    private readonly LocationChangeTimerDetector m_Detector;
    private readonly UndockButtonDetector m_UndockButtonDetector;
    private readonly ILogger m_Logger;

    public UndockingState()
        : this(new LocationChangeTimerDetector(), new UndockButtonDetector(), Log.ForContext<UndockingState>())
    {
    }

    internal UndockingState(
        LocationChangeTimerDetector detector,
        UndockButtonDetector undockButtonDetector,
        ILogger? logger = null)
    {
        m_Detector = detector;
        m_UndockButtonDetector = undockButtonDetector;
        m_Logger = logger ?? Log.ForContext<UndockingState>();
    }

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.Undocking;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        m_Logger.Debug("Executing {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();
        
        // Hide GUI
        context.AutomationInputController.PressKeyChord(VirtualKeyControl, VirtualKeyShift, VirtualKeyF9, cancellationToken);

        var capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(CaptureSuffix);
        cancellationToken.ThrowIfCancellationRequested();

        context.AutomationInputController.Delay(WindowActivationDelayMilliseconds, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        using var initialScreen = Cv2.ImRead(capturePath);

        // TryLocate Undock button
        if (!m_UndockButtonDetector.TryLocate(initialScreen, out var undockButtonBounds))
        {
            // Failed to detect Undock button
            m_Logger.Error("Not in Dock => abort undocking");
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Recover,
                capturePath);
        }

        // Undocking
        context.ClickUiElement(Center(undockButtonBounds), cancellationToken);

        context.AutomationInputController.Delay(InitialUndockDelayMilliseconds, cancellationToken);

        // Try to locate Location Change Timer icon with 1 second interval
        for (var attempt = 0; attempt < LocationChangeTimerPollingAttemptCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(CaptureSuffix);
            using var screen = Cv2.ImRead(capturePath);
            if (m_Detector.TryLocate(screen, out var location))
            {
                m_Logger.Information("Location Change Timer located");

                // Located => warp to asteroid belt
                return new MiningAutomationStateTransition(
                    Kind,
                    MiningAutomationStateKind.SelectBeltAndWarp,
                    MiningAutomationActionKind.CompleteUndock,
                    capturePath,
                    LocationChangeTimer: location);
            }

            context.AutomationInputController.Delay(LocationChangeTimerPollingMilliseconds, cancellationToken);
        }

        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Recovery,
            MiningAutomationActionKind.Recover,
            capturePath);
    }

    private static Point Center(Rect bounds) => new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
}
