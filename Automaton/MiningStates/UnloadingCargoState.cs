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
    DowntimeDetector downtimeDetector)
    : IMiningAutomationState
{
    public const string UnloadingCargoCaptureSuffix = ".mining-unloading-cargo";
    private const int OpenWindowAttemptCount = 5;
    private readonly ILogger m_Logger = Log.ForContext<UnloadingCargoState>();

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.UnloadCargo;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        m_Logger.Information("Executing {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();

        using var captureCheckIfDocked = context.ScreenCaptureService.CaptureCurrentScreen(UnloadingCargoCaptureSuffix);
        cancellationToken.ThrowIfCancellationRequested();

        // TryLocate Undock button
        if (!UndockButtonDetector.Detect(captureCheckIfDocked.Image, out _))
        {
            // Failed to detect Undock button
            m_Logger.Error("Not in Dock => abort unloading");
            return Recover(captureCheckIfDocked.CapturePath, MiningAutomationFailureReason.DetectionMiss);
        }

        if (!TryOpenInventoryWindow(
                context,
                VirtualKeys.M,
                analysis => analysis.MiningHoldTitleBounds is not null,
                "Mining Hold",
                cancellationToken))
        {
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Relogin);
        }

        if (!TryOpenInventoryWindow(
                context,
                VirtualKeys.G,
                analysis => analysis.ItemHangarTitleBounds is not null,
                "Item Hangar",
                cancellationToken))
        {
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Relogin);
        }

        using var capture = context.ScreenCaptureService.CaptureCurrentScreen(UnloadingCargoCaptureSuffix);
        cancellationToken.ThrowIfCancellationRequested();

        var analysis = inventoryDetector.Detect(capture.Image);
        AnnotateHoldTransferCapture(
            capture.CapturePath,
            capture.Image,
            analysis.MiningHoldFirstRowBounds,
            analysis.ItemHangarFirstRowBounds);
        if (analysis.MiningHoldTitleBounds is null || analysis.ItemHangarTitleBounds is null)
        {
            m_Logger.Error("Failed to detect Item Hangar and/or Mining Hold. Capture={CapturePath}", capture.CapturePath);
            return Recover(capture.CapturePath, MiningAutomationFailureReason.DetectionMiss);
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
                return Recover(capture.CapturePath, MiningAutomationFailureReason.DetectionMiss);
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
        var items = new List<(Rect, OverlayColor)>();

        if (miningHoldFirstRowBounds.HasValue)
        {
            items.Add((miningHoldFirstRowBounds.Value, OverlayColor.Lime));
        }

        if (itemHangarFirstRowBounds.HasValue)
        {
            items.Add((itemHangarFirstRowBounds.Value, OverlayColor.Yellow));
        }

        DebugOverlay.Annotate(annotated, items.ToArray());
        Cv2.ImWrite(capturePath, annotated);
    }

    private bool TryOpenInventoryWindow(
        MiningAutomationContext context,
        ushort inventoryVirtualKey,
        Func<InventoryAnalysis, bool> isWindowVisible,
        string windowName,
        CancellationToken cancellationToken)
    {
        using (var initialCapture = context.ScreenCaptureService.CaptureCurrentScreen(UnloadingCargoCaptureSuffix))
        {
            var initialAnalysis = inventoryDetector.Detect(initialCapture.Image);
            if (isWindowVisible(initialAnalysis))
            {
                m_Logger.Information(
                    "{WindowName} inventory window already visible. No open action required.",
                    windowName);
                return true;
            }
        }

        for (var attempt = 1; attempt <= OpenWindowAttemptCount; attempt++)
        {
            automationInputController.PressKeyChord(VirtualKeys.Alt, inventoryVirtualKey, cancellationToken);
            automationInputController.Delay(Delays.LoadWindowMs, cancellationToken);

            using var capture = context.ScreenCaptureService.CaptureCurrentScreen(UnloadingCargoCaptureSuffix);
            var analysis = inventoryDetector.Detect(capture.Image);
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

    private MiningAutomationStateTransition Recover(string? capturePath, MiningAutomationFailureReason failureReason = MiningAutomationFailureReason.None)
    {
        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Recovery,
            MiningAutomationActionKind.Recover,
            capturePath)
        {
            FailureReason = failureReason
        };
    }
}
