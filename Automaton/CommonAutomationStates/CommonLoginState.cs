using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using OpenCvSharp;
using Serilog;

namespace Automaton.CommonAutomationStates;

internal sealed class CommonLoginState(
    IAutomationInputController automationInputController)
{
    private readonly ILogger m_Logger = Log.ForContext<CommonLoginState>();

    public bool TryLoginPilot(int pilotIndex, string capturePath, CancellationToken cancellationToken, out Rect pilotBounds)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!PilotAvatarLocator.Detect(capturePath, pilotIndex, out var pilotLocation))
        {
            pilotBounds = default;
            return false;
        }

        DrawDebugOverlay(capturePath, pilotLocation.Bounds, $"Pilot {pilotIndex} found");

        pilotBounds = pilotLocation.Bounds;
        var delay = TimeSpan.FromMilliseconds(Delays.PilotLoginMs);
        m_Logger.Information("Logging in pilot {PilotIndex} for {DelaySeconds:0.###} seconds...", pilotIndex, delay.TotalSeconds);
        automationInputController.MoveTo(GeometryHelper.Center(pilotBounds));
        automationInputController.LeftClick(cancellationToken);
        automationInputController.Delay(delay, cancellationToken);

        m_Logger.Information("Hide any active window on post login screen");
        automationInputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.W, cancellationToken);
        automationInputController.Delay(Delays.MinimumClickMs, cancellationToken);

        return true;
    }

    private static void DrawDebugOverlay(string imagePath, Rect bounds, string label)
    {
        using var image = Cv2.ImRead(imagePath);
        if (image.Empty())
        {
            return;
        }

        DebugOverlay.Annotate(image, (bounds, OverlayColor.RedOrange));
        DebugOverlay.Label(image, label, OverlayColor.RedOrange);
        Cv2.ImWrite(imagePath, image);
    }
}
