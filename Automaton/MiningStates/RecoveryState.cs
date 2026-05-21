using Automaton.Detectors;
using Automaton.Primitives;
using OpenCvSharp;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class RecoveryState : IMiningAutomationState
{
    private const string CaptureSuffix = ".mining-recovery";

    private readonly ILogger m_Logger;

    public RecoveryState()
        : this(Log.ForContext<RecoveryState>())
    {
    }

    private RecoveryState(ILogger? logger = null)
    {
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
        context.AutomationInputController.Delay(Delays.RecoveryMs, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(CaptureSuffix);
        using var screen = Cv2.ImRead(capturePath);

        // Unload cargo and undock if Undock button found.
        // Dock otherwise.
        var nextState = UndockButtonDetector.TryLocate(screen, out _)
            ? MiningAutomationStateKind.UnloadCargo
            : MiningAutomationStateKind.Dock;

        return new MiningAutomationStateTransition(
            Kind,
            nextState,
            MiningAutomationActionKind.Recover,
            capturePath);
    }
}
