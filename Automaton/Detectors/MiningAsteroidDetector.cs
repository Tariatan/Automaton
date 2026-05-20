using OpenCvSharp;

namespace Automaton.Detectors;

internal sealed class MiningAsteroidDetector
{
    private const double MinimumAsteroidMatchScore = 0.74;
    private static readonly double[] TemplateScales = [0.80, 0.90, 1.0, 1.10, 1.20];
    private static readonly Rect AsteroidBounds = new(1800, 60, 76, 66);

    private readonly Mat[] m_AsteroidTemplates =
    [
        EmbeddedResourceLoader.LoadMat("mining.asteroid_pyroxeres.png"),
        EmbeddedResourceLoader.LoadMat("mining.asteroid_scordite.png"),
        EmbeddedResourceLoader.LoadMat("mining.asteroid_veldspar.png")
    ];

    public bool TryLocate(Mat screen)
    {
        if (screen.Empty())
        {
            return false;
        }

        var searchBounds = BuildAsteroidSearchBounds(screen.Size());
        return m_AsteroidTemplates.Any(asteroidTemplate =>
            TryMatchTemplate(screen, asteroidTemplate, searchBounds, MinimumAsteroidMatchScore));
    }

    private static bool TryMatchTemplate(Mat screen, Mat template, Rect bounds, double minimumScore)
    {
        if (!TryCreateRegion(screen, bounds, out var region))
        {
            return false;
        }

        using (region)
        {
            foreach (var scale in TemplateScales)
            {
                using var scaledTemplate = BuildScaledTemplate(template, scale);
                if (scaledTemplate.Width > region.Width || scaledTemplate.Height > region.Height)
                {
                    continue;
                }

                using var result = new Mat();
                Cv2.MatchTemplate(region, scaledTemplate, result, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(result, out _, out var maxScore, out _, out _);
                if (maxScore >= minimumScore)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private static Rect BuildAsteroidSearchBounds(Size imageSize)
    {
        var left = Math.Clamp(AsteroidBounds.X - 420, 0, Math.Max(0, imageSize.Width - 1));
        var top = Math.Clamp(AsteroidBounds.Y - 30, 0, Math.Max(0, imageSize.Height - 1));
        var right = Math.Clamp(AsteroidBounds.Right + 80, left + 1, imageSize.Width);
        var bottom = Math.Clamp(AsteroidBounds.Bottom + 90, top + 1, imageSize.Height);
        return new Rect(left, top, right - left, bottom - top);
    }

    private static bool TryCreateRegion(Mat screen, Rect bounds, out Mat region)
    {
        region = new Mat();
        var x = Math.Clamp(bounds.X, 0, Math.Max(0, screen.Width - 1));
        var y = Math.Clamp(bounds.Y, 0, Math.Max(0, screen.Height - 1));
        var right = Math.Clamp(bounds.Right, x + 1, screen.Width);
        var bottom = Math.Clamp(bounds.Bottom, y + 1, screen.Height);
        if (right <= x || bottom <= y)
        {
            return false;
        }

        region = new Mat(screen, new Rect(x, y, right - x, bottom - y));
        return true;
    }

    private static Mat BuildScaledTemplate(Mat template, double scale)
    {
        if (Math.Abs(scale - 1.0) < double.Epsilon)
        {
            return template.Clone();
        }

        var width = Math.Max(1, (int)Math.Round(template.Width * scale));
        var height = Math.Max(1, (int)Math.Round(template.Height * scale));
        var scaledTemplate = new Mat();
        Cv2.Resize(template, scaledTemplate, new Size(width, height));
        return scaledTemplate;
    }

}
