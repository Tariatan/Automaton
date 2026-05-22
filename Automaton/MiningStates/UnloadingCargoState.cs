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
    private const int OpenWindowAttemptCount = 5;
    private readonly ILogger m_Logger = logger ?? Log.ForContext<UnloadingCargoState>();

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.UnloadCargo;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        m_Logger.Debug("Executing {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryOpenInventoryWindow(
                context,
                cancellationToken,
                VirtualKeys.M,
                analysis => analysis.MiningHoldTitleBounds is not null,
                "Mining Hold"))
        {
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Recover);
        }

        if (!TryOpenInventoryWindow(
                context,
                cancellationToken,
                VirtualKeys.G,
                analysis => analysis.ItemHangarTitleBounds is not null,
                "Item Hangar"))
        {
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Recover);
        }

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
            automationInputController.ClickUiElement(GeometryHelper.Center(analysis.MiningHoldFirstRowBounds.Value), cancellationToken);
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

            automationInputController.ClickUiElement(GeometryHelper.Center(analysis.ItemHangarFirstRowBounds.Value), cancellationToken);
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

    private bool TryOpenInventoryWindow(
        MiningAutomationContext context,
        CancellationToken cancellationToken,
        ushort inventoryVirtualKey,
        Func<InventoryAnalysis, bool> isWindowVisible,
        string windowName)
    {
        for (var attempt = 1; attempt <= OpenWindowAttemptCount; attempt++)
        {
            automationInputController.PressKeyChord(VirtualKeys.Alt, inventoryVirtualKey, cancellationToken);
            automationInputController.Delay(Delays.OpenHoldMs, cancellationToken);

            using var capture = context.ScreenCaptureService.CaptureCurrentScreen(Settings.UnloadingCargoCaptureSuffix);
            var analysis = inventoryDetector.Analyze(capture.Image);
            if (isWindowVisible(analysis))
            {
                m_Logger.Information(
                    "{WindowName} inventory window opened. Attempt={Attempt}/{MaxAttempts}",
                    windowName,
                    attempt,
                    OpenWindowAttemptCount);
                return true;
            }

            m_Logger.Warning(
                "{WindowName} inventory window not visible after open attempt. Attempt={Attempt}/{MaxAttempts}",
                windowName,
                attempt,
                OpenWindowAttemptCount);
        }

        m_Logger.Error(
            "Failed to open {WindowName} inventory window after {AttemptCount} attempts",
            windowName,
            OpenWindowAttemptCount);
        return false;
    }

}
