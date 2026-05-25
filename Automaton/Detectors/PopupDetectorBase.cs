using OpenCvSharp;

namespace Automaton.Detectors;

internal abstract class PopupDetectorBase
{
    private const double DebugOverlayTextScale = 0.8;
    private const int DebugOverlayTextThickness = 2;
    private const int DebugOverlayLeftPadding = 30;
    private const int DebugOverlayTopPadding = 40;
    private static readonly Scalar DebugOverlayTextColor = new(80, 120, 255);

    protected abstract PopupState TargetState { get; }
    protected abstract string DebugOverlayText { get; }

    protected abstract PopupDetection DetectCore(Mat image);

    public PopupDetection Detect(string imagePath)
    {
        using var image = Cv2.ImRead(imagePath);
        var coreDetection = DetectCore(image);
        var detection = coreDetection.State == TargetState
            ? coreDetection
            : coreDetection with { State = PopupState.None };
        if (detection.State != TargetState)
        {
            return detection;
        }

        Cv2.Rectangle(image, detection.Bounds, DebugOverlayTextColor, 2);
        Cv2.PutText(
            image,
            DebugOverlayText,
            new Point(DebugOverlayLeftPadding, DebugOverlayTopPadding),
            HersheyFonts.HersheySimplex,
            DebugOverlayTextScale,
            DebugOverlayTextColor,
            DebugOverlayTextThickness,
            LineTypes.AntiAlias);
        Cv2.ImWrite(imagePath, image);
        return detection;
    }
}
