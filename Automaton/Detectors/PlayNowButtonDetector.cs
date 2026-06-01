using Automaton.Helpers;
using Automaton.Infrastructure;
using OpenCvSharp;

namespace Automaton.Detectors;

internal sealed class PlayNowButtonDetector : IDisposable
{
    private const double MinimumMatchScore = 0.82;
    private const double EarlyExitScore = 0.95;
    private static readonly double[] TemplateScales = [1.0, 0.95, 1.05, 0.90, 1.10];

    private readonly Mat m_TemplateGray;

    public PlayNowButtonDetector()
    {
        using var template = EmbeddedResourceLoader.LoadMat("play.png");
        m_TemplateGray = new Mat();
        Cv2.CvtColor(template, m_TemplateGray, ColorConversionCodes.BGR2GRAY);
    }

    public void Dispose()
    {
        m_TemplateGray.Dispose();
    }

    public bool Detect(string imagePath, out PlayNowButtonLocation location)
    {
        using var image = Cv2.ImRead(imagePath);
        if (image.Empty())
        {
            location = default;
            return false;
        }

        return TryLocateCore(image, out location);
    }

    private bool TryLocateCore(Mat screen, out PlayNowButtonLocation location)
    {
        location = default;

        using var screenGray = new Mat();
        if (screen.Channels() == 1)
            screen.CopyTo(screenGray);
        else
            Cv2.CvtColor(screen, screenGray, ColorConversionCodes.BGR2GRAY);

        PlayNowButtonLocation? bestLocation = null;
        foreach (var scale in TemplateScales)
        {
            var ownsScaled = !GeometryHelper.IsUnscaled(scale);
            var scaledTemplate = ownsScaled ? BuildScaledTemplate(scale) : m_TemplateGray;
            try
            {
                if (scaledTemplate.Width > screenGray.Width || scaledTemplate.Height > screenGray.Height)
                {
                    continue;
                }

                using var result = new Mat();
                Cv2.MatchTemplate(screenGray, scaledTemplate, result, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(result, out _, out var score, out _, out var locationPoint);
                var bounds = new Rect(locationPoint.X, locationPoint.Y, scaledTemplate.Width, scaledTemplate.Height);
                if (bestLocation is null || score > bestLocation.Value.Score)
                {
                    bestLocation = new PlayNowButtonLocation(bounds, score);
                }

                if (bestLocation.Value.Score >= EarlyExitScore)
                {
                    break;
                }
            }
            finally
            {
                if (ownsScaled)
                {
                    scaledTemplate.Dispose();
                }
            }
        }

        if (bestLocation is null || bestLocation.Value.Score < MinimumMatchScore)
        {
            return false;
        }

        location = bestLocation.Value;
        return true;
    }

    private Mat BuildScaledTemplate(double scale)
    {
        var width = Math.Max(1, (int)Math.Round(m_TemplateGray.Width * scale));
        var height = Math.Max(1, (int)Math.Round(m_TemplateGray.Height * scale));
        var scaledTemplate = new Mat();
        Cv2.Resize(m_TemplateGray, scaledTemplate, new Size(width, height));
        return scaledTemplate;
    }
}

internal readonly record struct PlayNowButtonLocation(Rect Bounds, double Score);
