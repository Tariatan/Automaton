using System.IO;
using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using OpenCvSharp;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class UnloadingCargoState(
    IAutomationInputController automationInputController,
    InventoryDetector inventoryDetector,
    DowntimeDetector downtimeDetector,
    ILogger? logger = null)
    : IMiningAutomationState
{
    private readonly ILogger m_Logger = logger ?? Log.ForContext<UnloadingCargoState>();

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.UnloadCargo;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        m_Logger.Debug("Executing {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();

        // Open inventory windows
        automationInputController.PressKeyChord(VirtualKeys.Alt, VirtualKeys.M, cancellationToken);
        automationInputController.PressKeyChord(VirtualKeys.Alt, VirtualKeys.G, cancellationToken);
        automationInputController.Delay(Delays.OpenHoldMs, cancellationToken);

        using var capture = context.ScreenCaptureService.CaptureCurrentScreen(Settings.UnloadingCargoCaptureSuffix);
        cancellationToken.ThrowIfCancellationRequested();

        // TryLocate Undock button
        if (!UndockButtonDetector.TryLocate(capture.Image, out _))
        {
            // Failed to detect Undock button
            m_Logger.Error("Not in Dock => abort unloading");
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.QuitGameFromDock,
                capture.CapturePath);
        }

        var analysis = inventoryDetector.Analyze(capture.Image);
        AnnotateHoldTransferCapture(
            capture.CapturePath,
            capture.Image,
            analysis.MiningHoldFirstRowBounds,
            analysis.ItemHangarFirstRowBounds);
        if (analysis.MiningHoldTitleBounds is null || analysis.ItemHangarTitleBounds is null)
        {
            m_Logger.Error("Failed to detect Item Hangar and/or Mining Hold");
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Recover,
                capture.CapturePath);
        }

        if (analysis.MiningHoldFirstRowBounds is not null)
        {
            m_Logger.Information("Transferring ore from Mining Hold to Item Hangar");
            automationInputController.ClickUiElement(Center(analysis.MiningHoldFirstRowBounds.Value), cancellationToken);
            automationInputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.A, cancellationToken);
            automationInputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.X, cancellationToken);

            if (analysis.ItemHangarFirstRowBounds is null)
            {
                m_Logger.Error("Failed to detect Item Hangar first row");
                return new MiningAutomationStateTransition(
                    Kind,
                    MiningAutomationStateKind.Recovery,
                    MiningAutomationActionKind.Recover,
                    capture.CapturePath);
            }

            automationInputController.ClickUiElement(Center(analysis.ItemHangarFirstRowBounds.Value), cancellationToken);
            automationInputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.V, cancellationToken);
            automationInputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.C, cancellationToken);
            automationInputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.V, cancellationToken);
        }

        // Close inventory windows
        automationInputController.PressKeyChord(VirtualKeys.Alt, VirtualKeys.M, cancellationToken);
        automationInputController.PressKeyChord(VirtualKeys.Alt, VirtualKeys.G, cancellationToken);

        if (downtimeDetector.IsDowntimeImminent(context.AutomationClock.UtcNow))
        {
            m_Logger.Warning("Downtime imminent => quit game and exit application. Now={Now}", context.AutomationClock.UtcNow);
            automationInputController.QuitGame(cancellationToken);
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.QuitGameAndExitApplication,
                capture.CapturePath);
        }

        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Undocking,
            MiningAutomationActionKind.Undock,
            capture.CapturePath);
    }

    private static void AnnotateHoldTransferCapture(
        string capturePath,
        Mat screen,
        Rect? miningHoldFirstRowBounds,
        Rect? itemHangarFirstRowBounds)
    {
        if (!File.Exists(capturePath))
        {
            return;
        }

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
