using System.Reflection;

namespace Automaton.Detectors;

internal sealed class ConnectionLostPopupDetector : PopupDetectorBase, IDisposable
{
    private readonly ConnectionLostPopupDetectionEngine m_Engine;

    public ConnectionLostPopupDetector(Assembly resourceAssembly)
    {
        m_Engine = new ConnectionLostPopupDetectionEngine(resourceAssembly);
    }

    public void Dispose() => m_Engine.Dispose();

    protected override PopupState TargetState => PopupState.ConnectionLost;
    protected override PopupDetection DetectCore(OpenCvSharp.Mat image) => m_Engine.DetectPopup(image);
}