using Automaton.Infrastructure;
using OpenCvSharp;

namespace Automaton.Detectors;

internal sealed class PlayNowButtonDetector : IDisposable
{
    private const double MinimumMatchScore = 0.82;
    private const double EarlyExitScore = 0.95;
    private static readonly double[] TemplateScales = [1.0, 0.95, 1.05, 0.90, 1.10];

    private readonly Mat m_Template = EmbeddedResourceLoader.LoadMat("play.png");

    public void Dispose()
    {
        m_Template.Dispose();
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
        if (screen.Empty())
        {
            return false;
        }

        using var searchableScreen = BuildSearchableScreen(screen);
        using var searchableScreenGray = new Mat();
        Cv2.CvtColor(searchableScreen, searchableScreenGray, ColorConversionCodes.BGR2GRAY);
        PlayNowButtonLocation? bestLocation = null;
        foreach (var scale in TemplateScales)
        {
            var ownsScaled = !IsUnscaled(scale);
            var scaledTemplate = ownsScaled ? BuildScaledTemplate(scale) : m_Template;
            try
            {
                using var scaledTemplateGray = new Mat();
                Cv2.CvtColor(scaledTemplate, scaledTemplateGray, ColorConversionCodes.BGR2GRAY);
                if (scaledTemplateGray.Width > searchableScreenGray.Width || scaledTemplateGray.Height > searchableScreenGray.Height)
                {
                    continue;
                }

                using var result = new Mat();
                Cv2.MatchTemplate(searchableScreenGray, scaledTemplateGray, result, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(result, out _, out var score, out _, out var locationPoint);
                var bounds = new Rect(locationPoint.X, locationPoint.Y, scaledTemplateGray.Width, scaledTemplateGray.Height);
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

    private static Mat BuildSearchableScreen(Mat screen)
    {
        if (screen.Channels() == 3)
        {
            return screen.Clone();
        }

        var colorScreen = new Mat();
        Cv2.CvtColor(screen, colorScreen, ColorConversionCodes.GRAY2BGR);
        return colorScreen;
    }

    private static bool IsUnscaled(double scale) => Math.Abs(scale - 1.0) < double.Epsilon;

    private Mat BuildScaledTemplate(double scale)
    {
        var width = Math.Max(1, (int)Math.Round(m_Template.Width * scale));
        var height = Math.Max(1, (int)Math.Round(m_Template.Height * scale));
        var scaledTemplate = new Mat();
        Cv2.Resize(m_Template, scaledTemplate, new Size(width, height));
        return scaledTemplate;
    }
}

internal readonly record struct PlayNowButtonLocation(Rect Bounds, double Score);
