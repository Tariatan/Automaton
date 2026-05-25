namespace Automaton.Detectors;

internal sealed class MaxSubmissionsPopupDetector : PopupDetectorBase
{
    protected override PopupState TargetState => PopupState.MaxSubmissions;
    protected override string DebugOverlayText => "Maximum submissions popup detected";
    protected override PopupDetection DetectCore(OpenCvSharp.Mat image) => PopupDetectionEngine.DetectPopup(image);
}
