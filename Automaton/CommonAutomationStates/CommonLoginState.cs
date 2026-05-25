using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using OpenCvSharp;
using Serilog;
using System.IO;

namespace Automaton.CommonAutomationStates;

internal sealed class CommonLoginState(
    IAutomationInputController automationInputController,
    ScreenCaptureService screenCaptureService)
{
    private readonly ILogger m_Logger = Log.ForContext<CommonLoginState>();

    public bool TryLoginPilot(int pilotIndex, string capturePath, CancellationToken cancellationToken, out Rect pilotBounds)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!PilotAvatarLocator.TryLocateAndDrawDebugOverlay(capturePath, pilotIndex, out var pilotLocation))
        {
            pilotBounds = default;
            return false;
        }

        pilotBounds = pilotLocation.Bounds;
        var delay = TimeSpan.FromMilliseconds(Delays.PilotLoginMs);
        m_Logger.Information("Logging in pilot {PilotIndex} for {DelaySeconds:0.###} seconds...", pilotIndex, delay.TotalSeconds);
        automationInputController.MoveTo(GeometryHelper.Center(pilotBounds));
        automationInputController.LeftClick(cancellationToken);
        automationInputController.Delay(delay, cancellationToken);

        m_Logger.Information("Hide any active window on post login screen");
        automationInputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.W, cancellationToken);
        automationInputController.Delay(Delays.MinimumClickMs, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        // Capture new screenshot to check file size
        var captureDirectory = Path.GetDirectoryName(capturePath) ?? string.Empty;
        var captureFileNameWithoutExtension = Path.GetFileNameWithoutExtension(capturePath);
        var captureExtension = Path.GetExtension(capturePath);
        var sizeCheckCapturePath = Path.Combine(captureDirectory, $"{captureFileNameWithoutExtension}-size-check{captureExtension}");
        screenCaptureService.CaptureCurrentScreenToFile(sizeCheckCapturePath);

        var captureFileInfo = new FileInfo(sizeCheckCapturePath);
        // Hide UI if captured file size is more than 1Mb
        if (captureFileInfo is { Exists: true, Length: > 1024 * 1024 })
        {
            m_Logger.Information("Hide UI. Capture size = {Size}Mb. Capture = {Capture}", captureFileInfo.Length / 1024 / 1024, sizeCheckCapturePath);
            automationInputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.Shift, VirtualKeys.F9, cancellationToken);
        }

        return true;
    }
}
