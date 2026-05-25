namespace Automaton.Detectors;

internal sealed class ConnectionLostPopupDetector : PopupDetectorBase
{
    protected override PopupState TargetState => PopupState.ConnectionLost;
    protected override string DebugOverlayText => "Connection lost popup detected";
    protected override PopupDetection DetectCore(OpenCvSharp.Mat image) => ConnectionLostPopupDetectionEngine.DetectPopup(image);
}
