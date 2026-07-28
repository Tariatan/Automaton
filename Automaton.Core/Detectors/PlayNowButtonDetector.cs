using System.Reflection;
using OpenCvSharp;

namespace Automaton.Core.Detectors;

internal sealed class PlayNowButtonDetector(Assembly resourceAssembly) : IDisposable
{
    private readonly TemplateButtonDetector m_Detector = new ("play.png", resourceAssembly);

    public void Dispose()
    {
        m_Detector.Dispose();
    }

    public bool Detect(string imagePath, out PlayNowButtonLocation location)
    {
        if (!m_Detector.Detect(imagePath, out var detectedLocation))
        {
            location = default;
            return false;
        }

        location = new PlayNowButtonLocation(detectedLocation.Bounds, detectedLocation.Score);
        return true;
    }
}

internal readonly record struct PlayNowButtonLocation(Rect Bounds, double Score);
