using OpenCvSharp;

namespace Automaton.Detectors;

internal abstract class PopupDetectorBase
{
    protected abstract PopupState TargetState { get; }

    protected abstract PopupDetection DetectCore(Mat image);

    public PopupDetection Detect(Mat image)
    {
        var coreDetection = DetectCore(image);
        return coreDetection.State == TargetState
            ? coreDetection
            : coreDetection with { State = PopupState.None };
    }
}
