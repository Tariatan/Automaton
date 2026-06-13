using OpenCvSharp;

namespace Automaton.Detectors;

internal sealed class PlayNowButtonDetector : IDisposable
{
    private readonly TemplateButtonDetector m_Detector = new("play.png");

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
