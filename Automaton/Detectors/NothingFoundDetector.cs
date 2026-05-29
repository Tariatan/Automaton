using Automaton.Infrastructure;
using OpenCvSharp;

namespace Automaton.Detectors;

internal static class NothingFoundDetector
{
    private const double MinimumTemplateMatchScore = 0.62;
    private static readonly double[] TemplateScales = [1.0, 0.95, 1.05, 0.9, 1.1, 0.85, 1.15];
    private static readonly Mat[] ScaledTemplates = BuildScaledTemplates();

    public static bool Detect(Mat screen, Rect mineOverviewBounds)
    {
        if (screen.Empty())
        {
            return false;
        }

        var searchBounds = BuildSearchBounds(screen.Size(), mineOverviewBounds);
        if (MatchesAnyTemplate(screen, searchBounds))
        {
            return true;
        }

        var expandedBounds = BuildExpandedSearchBounds(screen.Size(), mineOverviewBounds);
        return MatchesAnyTemplate(screen, expandedBounds);
    }

    private static bool MatchesAnyTemplate(Mat screen, Rect searchBounds)
    {
        using var searchRegion = new Mat(screen, searchBounds);
        using var searchRegionGray = new Mat();
        Cv2.CvtColor(searchRegion, searchRegionGray, ColorConversionCodes.BGR2GRAY);

        foreach (var template in ScaledTemplates)
        {
            if (template.Width > searchRegionGray.Width || template.Height > searchRegionGray.Height)
            {
                continue;
            }

            using var result = new Mat();
            Cv2.MatchTemplate(searchRegionGray, template, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out var score, out _, out _);
            if (score >= MinimumTemplateMatchScore)
            {
                return true;
            }
        }

        return false;
    }

    private static Rect BuildSearchBounds(Size imageSize, Rect mineOverviewBounds)
    {
        var left = Math.Clamp(mineOverviewBounds.X + 8, 0, Math.Max(0, imageSize.Width - 1));
        var top = Math.Clamp(mineOverviewBounds.Y + 40, 0, Math.Max(0, imageSize.Height - 1));
        var right = Math.Clamp(mineOverviewBounds.Right - 8, left + 1, imageSize.Width);
        var bottom = Math.Clamp(mineOverviewBounds.Bottom - 10, top + 1, imageSize.Height);
        return new Rect(left, top, right - left, bottom - top);
    }

    private static Rect BuildExpandedSearchBounds(Size imageSize, Rect mineOverviewBounds)
    {
        var left = Math.Clamp(mineOverviewBounds.X + 8, 0, Math.Max(0, imageSize.Width - 1));
        var top = Math.Clamp(mineOverviewBounds.Y + 20, 0, Math.Max(0, imageSize.Height - 1));
        var right = Math.Clamp(mineOverviewBounds.Right - 8, left + 1, imageSize.Width);
        var bottom = Math.Clamp(imageSize.Height - 10, top + 1, imageSize.Height);
        return new Rect(left, top, right - left, bottom - top);
    }

    private static Mat[] BuildScaledTemplates()
    {
        using var original = EmbeddedResourceLoader.LoadMat("overview.nothing_found.png");
        using var originalGray = new Mat();
        Cv2.CvtColor(original, originalGray, ColorConversionCodes.BGR2GRAY);

        var templates = new Mat[TemplateScales.Length];
        for (var i = 0; i < TemplateScales.Length; i++)
        {
            var scale = TemplateScales[i];
            if (Math.Abs(scale - 1.0) < double.Epsilon)
            {
                templates[i] = originalGray.Clone();
            }
            else
            {
                var width = Math.Max(1, (int)Math.Round(originalGray.Width * scale));
                var height = Math.Max(1, (int)Math.Round(originalGray.Height * scale));
                var scaled = new Mat();
                Cv2.Resize(originalGray, scaled, new Size(width, height));
                templates[i] = scaled;
            }
        }

        return templates;
    }
}
