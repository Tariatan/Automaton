namespace Automaton.Detectors;

internal sealed class SlowDownPopupDetector : PopupDetectorBase
{
    protected override PopupState TargetState => PopupState.SlowDown;
    protected override PopupDetection DetectCore(OpenCvSharp.Mat image) => PopupDetectionEngine.DetectPopup(image);
}
