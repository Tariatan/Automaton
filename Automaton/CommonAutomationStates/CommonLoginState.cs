using Automaton.Detectors;
using Automaton.Helpers;
using OpenCvSharp;
using Serilog;

namespace Automaton.CommonAutomationStates;

internal sealed class CommonLoginState(
    IGameActionService gameActionService,
    PilotAvatarDetector pilotAvatarDetector,
    LoggedInPilotDetector loggedInPilotDetector)
{
    private const string LoggedInPilotCheckCaptureSuffix = ".logged-in-pilot-check";
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

        while (true)
        {
            gameActionService.Login(pilotIndex, GeometryHelper.Center(pilotLocation.Value), cancellationToken);

            if (IsRequestedPilotLoggedIn(screenCaptureService, pilotIndex, captureSuffix, cancellationToken, out capturePath))
            {
                return true;
            }

            m_Logger.Warning(
                "Requested pilot login verification failed. Logging out and retrying. PilotIndex={PilotIndex}, CapturePath={CapturePath}",
                pilotIndex,
                capturePath);

            gameActionService.Logout(screenCaptureService, pilotAvatarDetector, pilotIndex, cancellationToken);
            pilotLocation = DetectPilotLocation(screenCaptureService, pilotIndex, captureSuffix, cancellationToken, out capturePath);
            if (pilotLocation is null)
            {
                return false;
            }
        }
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

    private bool IsRequestedPilotLoggedIn(
        ScreenCaptureService screenCaptureService,
        int pilotIndex,
        string captureSuffix,
        CancellationToken cancellationToken,
        out string capturePath)
    {
        using var capture = screenCaptureService.CaptureCurrentScreen($"{captureSuffix}{LoggedInPilotCheckCaptureSuffix}-{pilotIndex}");
        capturePath = capture.CapturePath;
        cancellationToken.ThrowIfCancellationRequested();

        if (!loggedInPilotDetector.Detect(capture.Image, out var detection))
        {
            return false;
        }

        DrawDebugOverlay(capturePath, detection.Bounds, $"Logged in pilot {detection.PilotIndex} found");
        return detection.PilotIndex == pilotIndex;
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
