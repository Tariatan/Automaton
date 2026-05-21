using Automaton.Detectors;
using Automaton.Primitives;
using OpenCvSharp;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class UnloadingCargoState : IMiningAutomationState
{
    private const string CaptureSuffix = ".mining-docked";

    private readonly MiningHoldDetector m_MiningHoldDetector;
    private readonly DowntimeDetector m_DowntimeDetector;
    private readonly ILogger m_Logger;

    public UnloadingCargoState()
        : this(new MiningHoldDetector(), new DowntimeDetector(), Log.ForContext<UnloadingCargoState>())
    {
    }

    internal UnloadingCargoState(
        MiningHoldDetector miningHoldDetector,
        DowntimeDetector downtimeDetector,
        ILogger? logger = null)
    {
        m_MiningHoldDetector = miningHoldDetector;
        m_DowntimeDetector = downtimeDetector;
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
        context.AutomationInputController.PressKeyChord(VirtualKeys.Alt, VirtualKeys.M, cancellationToken);
        context.AutomationInputController.Delay(Delays.OpenHoldMs, cancellationToken);
        context.AutomationInputController.PressKeyChord(VirtualKeys.Alt, VirtualKeys.G, cancellationToken);

        var capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(CaptureSuffix);
        cancellationToken.ThrowIfCancellationRequested();

        using var screen = Cv2.ImRead(capturePath);

        // TryLocate Undock button
        if (!UndockButtonDetector.TryLocate(screen, out _))
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
        AnnotateHoldTransferCapture(
            capturePath,
            screen,
            analysis.MiningHoldFirstRowBounds,
            analysis.ItemHangarFirstRowBounds);
        if (analysis.MiningHoldTitleBounds is null || analysis.ItemHangarTitleBounds is null)
        {
            m_Logger.Error("Failed to detect Item Hangar and/or Mining Hold");
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Recover,
                capturePath);
        }

        if (analysis.MiningHoldFirstRowBounds is not null)
        {
            m_Logger.Information("Transferring ore from Mining Hold to Item Hangar");
            context.ClickUiElement(Center(analysis.MiningHoldFirstRowBounds.Value), cancellationToken);
            context.AutomationInputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.A, cancellationToken);
            context.AutomationInputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.X, cancellationToken);

            if (analysis.ItemHangarFirstRowBounds is null)
            {
                m_Logger.Error("Failed to detect Item Hangar first row");
                return new MiningAutomationStateTransition(
                    Kind,
                    MiningAutomationStateKind.Recovery,
                    MiningAutomationActionKind.Recover,
                    capturePath);
            }

            context.ClickUiElement(Center(analysis.ItemHangarFirstRowBounds.Value), cancellationToken);
            context.AutomationInputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.V, cancellationToken);
            context.AutomationInputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.C, cancellationToken);
            context.AutomationInputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.V, cancellationToken);
        }

        // Close inventory windows
        context.AutomationInputController.PressKeyChord(VirtualKeys.Alt, VirtualKeys.M, cancellationToken);
        context.AutomationInputController.PressKeyChord(VirtualKeys.Alt, VirtualKeys.G, cancellationToken);

        if (m_DowntimeDetector.IsDowntimeImminent(context.AutomationClock.UtcNow))
        {
            m_Logger.Warning("Downtime imminent => quit game and exit application. Now={Now}", context.AutomationClock.UtcNow);
            context.AutomationInputController.QuitGame(cancellationToken);
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.QuitGameAndExitApplication,
                capturePath);
        }

        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Undocking,
            MiningAutomationActionKind.Undock,
            capturePath);
    }

    private static void AnnotateHoldTransferCapture(
        string capturePath,
        Mat screen,
        Rect? miningHoldFirstRowBounds,
        Rect? itemHangarFirstRowBounds)
    {
        using var annotated = screen.Clone();
        if (miningHoldFirstRowBounds.HasValue)
        {
            Cv2.Rectangle(annotated, miningHoldFirstRowBounds.Value, new Scalar(0, 255, 0), 2);
        }

        if (itemHangarFirstRowBounds.HasValue)
        {
            Cv2.Rectangle(annotated, itemHangarFirstRowBounds.Value, new Scalar(0, 255, 255), 2);
        }

        Cv2.ImWrite(capturePath, annotated);
    }

    private static Point Center(Rect bounds) => new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
}
