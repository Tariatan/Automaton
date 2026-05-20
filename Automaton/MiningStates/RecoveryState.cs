using Automaton.Detectors;
using OpenCvSharp;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class RecoveryState : IMiningAutomationState
{
    private const string CaptureSuffix = ".mining-recovery";
    private const int RecoveryDelayMilliseconds = 60_000;

    private readonly UndockButtonDetector m_UndockButtonDetector;
    private readonly ILogger m_Logger;

    public RecoveryState()
        : this(new UndockButtonDetector(), Log.ForContext<RecoveryState>())
    {
    }

    internal RecoveryState(UndockButtonDetector undockButtonDetector, ILogger? logger = null)
    {
        m_UndockButtonDetector = undockButtonDetector;
        m_Logger = logger ?? Log.ForContext<RecoveryState>();
    }

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.Recovery;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        m_Logger.Debug("Executing {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();
        // Debounce
        context.AutomationInputController.Delay(RecoveryDelayMilliseconds, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(CaptureSuffix);
        using var screen = Cv2.ImRead(capturePath);

        // Unload cargo and undock if Undock button found.
        // Dock otherwise.
        var nextState = m_UndockButtonDetector.TryLocate(screen, out _)
            ? MiningAutomationStateKind.UnloadCargo
            : MiningAutomationStateKind.Dock;

        return new MiningAutomationStateTransition(
            Kind,
            nextState,
            MiningAutomationActionKind.Recover,
            capturePath);
    }
}
