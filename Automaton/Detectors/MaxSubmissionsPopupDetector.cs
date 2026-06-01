namespace Automaton.Detectors;

internal sealed class MaxSubmissionsPopupDetector : PopupDetectorBase
{
    protected override PopupState TargetState => PopupState.MaxSubmissions;
    protected override PopupDetection DetectCore(OpenCvSharp.Mat image) => PopupDetectionEngine.Detect(image);
}
