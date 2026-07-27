using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using OpenCvSharp;
using Serilog;

namespace Automaton.CommonAutomationStates;

internal sealed class CommonRecoverClientIsRunningButtonVisibleState(
    IAutomationInputController automationInputController,
    ClientIsRunningButtonDetector clientIsRunningButtonDetector)
{
    public string Execute(
        ScreenCaptureService screenCaptureService,
        string captureSuffix,
        object detectionStage,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        logger.Warning("Client Is Running button visible during {DetectionStage}", detectionStage);
        automationInputController.Delay(Delays.ClientIsRunningButtonVisibleBeforeClickMs, cancellationToken);

        using var capture = screenCaptureService.CaptureCurrentScreen(captureSuffix);
        cancellationToken.ThrowIfCancellationRequested();
        if (!clientIsRunningButtonDetector.Detect(capture.Image, out var clientIsRunningButtonLocation))
        {
            logger.Warning("Client Is Running button disappeared before recovery click. CapturePath={CapturePath}", capture.CapturePath);
            return capture.CapturePath;
        }

        DrawDebugOverlay(capture.CapturePath, clientIsRunningButtonLocation.Bounds);
        logger.Warning(
            "Clicking Client Is Running button and waiting for launcher recovery. CapturePath={CapturePath}",
            capture.CapturePath);
        automationInputController.MoveTo(GeometryHelper.Center(clientIsRunningButtonLocation.Bounds));
        automationInputController.LeftClick(cancellationToken);
        automationInputController.Delay(Delays.ClientIsRunningButtonVisibleAfterClickMs, cancellationToken);
        return capture.CapturePath;
    }

    private static void DrawDebugOverlay(string imagePath, Rect bounds)
    {
        using var image = Cv2.ImRead(imagePath);
        if (image.Empty())
        {
            return;
        }

        DebugOverlay.Annotate(image, (bounds, OverlayColor.RedOrange));
        DebugOverlay.Label(image, "Client Is Running button found", OverlayColor.RedOrange);
        ImageFileWriter.WriteImage(imagePath, image);
    }
}
