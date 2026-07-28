using System.Reflection;
using OpenCvSharp;

namespace Automaton.Core.Detectors;

internal sealed class ClientIsRunningButtonDetector(Assembly resourceAssembly) : IDisposable
{
    private readonly TemplateButtonDetector m_Detector = new ("client_is_running.png", resourceAssembly);

    public void Dispose()
    {
        m_Detector.Dispose();
    }

    public bool Detect(string imagePath, out ClientIsRunningButtonLocation location)
    {
        if (!m_Detector.Detect(imagePath, out var detectedLocation))
        {
            location = default;
            return false;
        }

        location = new ClientIsRunningButtonLocation(detectedLocation.Bounds, detectedLocation.Score);
        return true;
    }

    public bool Detect(Mat screen, out ClientIsRunningButtonLocation location)
    {
        if (!m_Detector.Detect(screen, out var detectedLocation))
        {
            location = default;
            return false;
        }

        location = new ClientIsRunningButtonLocation(detectedLocation.Bounds, detectedLocation.Score);
        return true;
    }
}

internal readonly record struct ClientIsRunningButtonLocation(Rect Bounds, double Score);
