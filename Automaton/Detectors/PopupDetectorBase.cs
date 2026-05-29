using OpenCvSharp;

namespace Automaton.Detectors;

internal abstract class PopupDetectorBase
{
    protected abstract PopupState TargetState { get; }

    protected abstract PopupDetection DetectCore(Mat image);

    public PopupDetection Detect(string imagePath)
    {
        using var image = Cv2.ImRead(imagePath);
        var coreDetection = DetectCore(image);
        return coreDetection.State == TargetState
            ? coreDetection
            : coreDetection with { State = PopupState.None };
    }
}
