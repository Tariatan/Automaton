using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using OpenCvSharp;
using Serilog;

namespace Automaton.CommonAutomationStates;

internal sealed class CommonStartGameState(
    IAutomationInputController automationInputController,
    PlayNowButtonDetector playNowButtonDetector)
{
    private readonly ILogger m_Logger = Log.ForContext<CommonStartGameState>();

    public bool TryStartGame(
        ScreenCaptureService screenCaptureService,
        string captureSuffix,
        CancellationToken cancellationToken,
        out string capturePath)
    {
        using var capture = screenCaptureService.CaptureCurrentScreen(captureSuffix);
        capturePath = capture.CapturePath;

        cancellationToken.ThrowIfCancellationRequested();
        if (!playNowButtonDetector.Detect(capturePath, out var playButtonLocation))
        {
            return false;
        }

        DrawDebugOverlay(capturePath, playButtonLocation.Bounds);

        var delay = TimeSpan.FromMilliseconds(Delays.LauncherStartupMs);
        m_Logger.Information("Starting Game for {DelaySeconds:0.###} seconds...", delay.TotalSeconds);
        automationInputController.MoveTo(GeometryHelper.Center(playButtonLocation.Bounds));
        automationInputController.LeftClick(cancellationToken);
        automationInputController.Delay(delay, cancellationToken);

        m_Logger.Information("Hide any active window on login screen first");
        automationInputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.W, cancellationToken);

        return true;
    }

    private static void DrawDebugOverlay(string imagePath, Rect bounds)
    {
        using var image = Cv2.ImRead(imagePath);
        if (image.Empty())
        {
            return;
        }

        DebugOverlay.Annotate(image, (bounds, OverlayColor.RedOrange));
        DebugOverlay.Label(image, "PLAY NOW found", OverlayColor.RedOrange);
        Cv2.ImWrite(imagePath, image);
    }
}
