using Automaton.Detectors;
using OpenCvSharp;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class UnloadingCargoState : IMiningAutomationState
{
    private const string CaptureSuffix = ".mining-docked";
    private const int OpenHoldDelayMilliseconds = 1_000;
    private const ushort VirtualKeyAlt = 0x12;
    private const ushort VirtualKeyControl = 0x11;
    private const ushort VirtualKeyM = 0x4D;
    private const ushort VirtualKeyG = 0x47;
    private const ushort VirtualKeyX = 0x58;
    private const ushort VirtualKeyA = 0x41;
    private const ushort VirtualKeyV = 0x56;
    private const ushort VirtualKeyC = 0x43;

    private readonly MiningHoldDetector m_MiningHoldDetector;
    private readonly UndockButtonDetector m_UndockButtonDetector;
    private readonly ILogger m_Logger;

    public UnloadingCargoState()
        : this(new MiningHoldDetector(), new UndockButtonDetector(), Log.ForContext<UnloadingCargoState>())
    {
    }

    internal UnloadingCargoState(
        MiningHoldDetector miningHoldDetector,
        UndockButtonDetector undockButtonDetector,
        ILogger? logger = null)
    {
        m_MiningHoldDetector = miningHoldDetector;
        m_UndockButtonDetector = undockButtonDetector;
        m_Logger = logger ?? Log.ForContext<UnloadingCargoState>();
    }

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.UnloadCargo;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        m_Logger.Debug("Executing {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();

        // Open inventory windows
        context.AutomationInputController.PressKeyChord(VirtualKeyAlt, VirtualKeyM, cancellationToken);
        context.AutomationInputController.Delay(OpenHoldDelayMilliseconds, cancellationToken);
        context.AutomationInputController.PressKeyChord(VirtualKeyAlt, VirtualKeyG, cancellationToken);

        var capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(CaptureSuffix);
        cancellationToken.ThrowIfCancellationRequested();

        using var screen = Cv2.ImRead(capturePath);

        // TryLocate Undock button
        if (!m_UndockButtonDetector.TryLocate(screen, out var _))
        {
            // Failed to detect Undock button
            m_Logger.Error("Not in Dock => abort unloading");
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Recover,
                capturePath);
        }

        var analysis = m_MiningHoldDetector.Analyze(screen);
        if (analysis.MiningHoldTitleBounds is null || analysis.ItemHangarTitleBounds is null)
        {
            m_Logger.Error("Failed to detect Item Hangar and/or Mining Hold");
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Recover,
                capturePath,
                analysis);
        }

        if (analysis.MiningHoldFirstRowBounds is not null)
        {
            m_Logger.Information("Transferring ore from Mining Hold to Item Hangar");
            context.ClickUiElement(Center(analysis.MiningHoldFirstRowBounds.Value), cancellationToken);
            context.AutomationInputController.PressKeyChord(VirtualKeyControl, VirtualKeyA, cancellationToken);
            context.AutomationInputController.PressKeyChord(VirtualKeyControl, VirtualKeyX, cancellationToken);

            if (analysis.ItemHangarFirstRowBounds is null)
            {
                m_Logger.Error("Failed to detect Item Hangar first row");
                return new MiningAutomationStateTransition(
                    Kind,
                    MiningAutomationStateKind.Recovery,
                    MiningAutomationActionKind.Recover,
                    capturePath,
                    analysis);
            }

            context.ClickUiElement(Center(analysis.ItemHangarFirstRowBounds.Value), cancellationToken);
            context.AutomationInputController.PressKeyChord(VirtualKeyControl, VirtualKeyV, cancellationToken);
            context.AutomationInputController.PressKeyChord(VirtualKeyControl, VirtualKeyC, cancellationToken);
            context.AutomationInputController.PressKeyChord(VirtualKeyControl, VirtualKeyV, cancellationToken);
        }

        // Close inventory windows
        context.AutomationInputController.PressKeyChord(VirtualKeyAlt, VirtualKeyM, cancellationToken);
        context.AutomationInputController.PressKeyChord(VirtualKeyAlt, VirtualKeyG, cancellationToken);

        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Undocking,
            MiningAutomationActionKind.Undock,
            capturePath,
            analysis);
    }

    private static Point Center(Rect bounds) => new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
}
