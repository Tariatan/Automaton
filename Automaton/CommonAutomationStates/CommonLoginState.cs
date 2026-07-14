using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using OpenCvSharp;
using Serilog;

namespace Automaton.CommonAutomationStates;

internal sealed class CommonLoginState(
    IGameActionService gameActionService,
    IAutomationInputController automationInputController,
    PilotAvatarDetector pilotAvatarDetector,
    LoggedInPilotDetector loggedInPilotDetector)
{
    private const string LoggedInPilotCheckCaptureSuffix = ".logged-in-pilot-check";
    private readonly ILogger m_Logger = Log.ForContext<CommonLoginState>();

    public bool TryLoginPilot(
        ScreenCaptureService screenCaptureService,
        int requestedPilotIndex,
        string captureSuffix,
        CancellationToken cancellationToken,
        out string capturePath)
    {
        capturePath = string.Empty;
        var loginAttempt = 0;
        while (loginAttempt++ < Settings.MaxLoginAttempts)
        {
            // Detect requested Pilot on Login screen
            var pilotLocation = DetectPilotLocation(screenCaptureService, requestedPilotIndex, captureSuffix, cancellationToken, out capturePath);
            if (pilotLocation is null)
            {
                return false;
            }

            // Click on the requested pilot and wait for the game to load
            var delay = TimeSpan.FromMilliseconds(Delays.PilotLoginDebounceMs);
            m_Logger.Information("Logging in pilot {PilotIndex} for {DelaySeconds:0.###} seconds...", requestedPilotIndex, delay.TotalSeconds);
            automationInputController.ClickUiElement(GeometryHelper.Center(pilotLocation.Value), cancellationToken);
            automationInputController.Delay(delay, cancellationToken);

            // Loop until the requested pilot's portrait is detected in game
            var detectedPilotIndex = -1;
            var elapsedMs = 0;
            while (elapsedMs < Delays.PilotLoginTimeoutMs)
            {
                automationInputController.Delay(Delays.PilotLoginPollingMs, cancellationToken);
                elapsedMs += Delays.PilotLoginPollingMs;

                // Close any window in game after login
                gameActionService.CloseActiveWindow(cancellationToken);
                automationInputController.Delay(Delays.MinimumClickMs, cancellationToken);

                using var capture = screenCaptureService.CaptureCurrentScreen($"{captureSuffix}{LoggedInPilotCheckCaptureSuffix}-{requestedPilotIndex}");
                capturePath = capture.CapturePath;
                cancellationToken.ThrowIfCancellationRequested();

                // Still black logging screen...
                if (!loggedInPilotDetector.Detect(capture.Image, out var detection))
                {
                    continue;
                }

                DrawDebugOverlay(capturePath, detection.Bounds, $"Logged in pilot {detection.PilotIndex} found");
                m_Logger.Information("Logged in pilot {PilotIndex} found. CapturePath={CapturePath}", detection.PilotIndex, capturePath);
                detectedPilotIndex = detection.PilotIndex;
                break;
            }

            if (detectedPilotIndex == requestedPilotIndex)
            {
                return true;
            }

            m_Logger.Warning(
                "Requested pilot {PilotIndex} login verification failed. Logging out and retrying. CapturePath={CapturePath}",
                requestedPilotIndex,
                capturePath);

            gameActionService.Logout(screenCaptureService, pilotAvatarDetector, requestedPilotIndex, cancellationToken);
        }

        return false;
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
        ImageFileWriter.WriteImage(imagePath, image);
    }
}
