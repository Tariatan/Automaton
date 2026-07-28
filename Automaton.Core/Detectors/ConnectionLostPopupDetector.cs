using System.Reflection;

namespace Automaton.Core.Detectors;

internal sealed class ConnectionLostPopupDetector(Assembly resourceAssembly) : PopupDetectorBase, IDisposable
{
    private readonly ConnectionLostPopupDetectionEngine m_Engine = new (resourceAssembly);

    public void Dispose() => m_Engine.Dispose();

    protected override PopupState TargetState => PopupState.ConnectionLost;
    protected override PopupDetection DetectCore(OpenCvSharp.Mat image) => m_Engine.DetectPopup(image);
}