using Automaton.Detectors;
using Automaton.Helpers;
using OpenCvSharp;
using Serilog;

namespace Automaton.CommonAutomationStates;

internal sealed class CommonLoginState(
    IGameActionService gameActionService,
    PilotAvatarDetector pilotAvatarDetector)
{
    private readonly ILogger m_Logger = Log.ForContext<CommonLoginState>();

    public bool TryLoginPilot(
        ScreenCaptureService screenCaptureService,
        int pilotIndex,
        string captureSuffix,
        CancellationToken cancellationToken,
        out string capturePath)
    {
        var pilotLocation = DetectPilotLocation(screenCaptureService, pilotIndex, captureSuffix, cancellationToken, out capturePath);
        if (pilotLocation is null)
        {
            return false;
        }

        gameActionService.Login(pilotIndex, GeometryHelper.Center(pilotLocation.Value), cancellationToken);

        m_Logger.Warning("Workaround wrong pilot login issue by logging in twice");
        {
            gameActionService.Logout(screenCaptureService, pilotAvatarDetector, pilotIndex, cancellationToken);
            pilotLocation = DetectPilotLocation(screenCaptureService, pilotIndex, captureSuffix, cancellationToken, out capturePath);
            if (pilotLocation is null)
            {
                return false;
            }

            gameActionService.Login(pilotIndex, GeometryHelper.Center(pilotLocation.Value), cancellationToken);
        }

        return true;
    }

    private Rect? DetectPilotLocation(
        ScreenCaptureService screenCaptureService,
        int pilotIndex,
        string captureSuffix,
        CancellationToken cancellationToken,
        out string capturePath)
    {
        gameActionService.CloseActiveWindow(cancellationToken);

        using var capture = screenCaptureService.CaptureCurrentScreen($"{captureSuffix}-{pilotIndex}");
        capturePath = capture.CapturePath;
        cancellationToken.ThrowIfCancellationRequested();

        if (!pilotAvatarDetector.Detect(capture.Image, pilotIndex, out var pilotLocation))
        {
            return null;
        }

        DrawDebugOverlay(capturePath, pilotLocation.Bounds, $"Pilot {pilotIndex} found");
        return pilotLocation.Bounds;
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
