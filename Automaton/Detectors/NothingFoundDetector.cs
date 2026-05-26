using Automaton.Infrastructure;
using OpenCvSharp;

namespace Automaton.Detectors;

internal static class NothingFoundDetector
{
    private const double MinimumTemplateMatchScore = 0.62;
    private static readonly double[] TemplateScales = [1.0, 0.95, 1.05, 0.9, 1.1, 0.85, 1.15];
    private static readonly Mat NothingFoundTemplate = EmbeddedResourceLoader.LoadMat("overview.nothing_found.png");
    private static readonly Scalar FoundBoundsColor = new(120, 220, 255);

    public static bool Detect(Mat screen, Rect mineOverviewBounds)
    {
        var detected = TryLocate(screen, mineOverviewBounds, out var foundBounds);
        if (detected)
        {
            Cv2.Rectangle(screen, foundBounds, FoundBoundsColor, 2);
        }

        return detected;
    }

    private static bool TryLocate(Mat screen, Rect mineOverviewBounds, out Rect foundBounds)
    {
        foundBounds = default;
        if (screen.Empty())
        {
            return false;
        }

        var searchBounds = BuildNothingFoundSearchBounds(screen.Size(), mineOverviewBounds);
        if (TryMatchTemplate(screen, searchBounds, out foundBounds))
        {
            return true;
        }

        var expandedSearchBounds = BuildExpandedNothingFoundSearchBounds(screen.Size(), mineOverviewBounds);
        return TryMatchTemplate(screen, expandedSearchBounds, out foundBounds);
    }

    private static Rect BuildNothingFoundSearchBounds(Size imageSize, Rect mineOverviewBounds)
    {
        var left = Math.Clamp(mineOverviewBounds.X + 8, 0, Math.Max(0, imageSize.Width - 1));
        var top = Math.Clamp(mineOverviewBounds.Y + 40, 0, Math.Max(0, imageSize.Height - 1));
        var right = Math.Clamp(mineOverviewBounds.Right - 8, left + 1, imageSize.Width);
        var bottom = Math.Clamp(mineOverviewBounds.Bottom - 10, top + 1, imageSize.Height);
        return new Rect(left, top, right - left, bottom - top);
    }

    private static Rect BuildExpandedNothingFoundSearchBounds(Size imageSize, Rect mineOverviewBounds)
    {
        var left = Math.Clamp(mineOverviewBounds.X + 8, 0, Math.Max(0, imageSize.Width - 1));
        var top = Math.Clamp(mineOverviewBounds.Y + 20, 0, Math.Max(0, imageSize.Height - 1));
        var right = Math.Clamp(mineOverviewBounds.Right - 8, left + 1, imageSize.Width);
        var bottom = Math.Clamp(imageSize.Height - 10, top + 1, imageSize.Height);
        return new Rect(left, top, right - left, bottom - top);
    }

    private static bool TryMatchTemplate(Mat screen, Rect searchBounds, out Rect foundBounds)
    {
        foundBounds = default;
        using var searchRegion = new Mat(screen, searchBounds);
        using var searchRegionGray = new Mat();
        Cv2.CvtColor(searchRegion, searchRegionGray, ColorConversionCodes.BGR2GRAY);
        TemplateLocation? bestLocation = null;
        foreach (var scale in TemplateScales)
        {
            using var scaledTemplate = BuildScaledTemplate(NothingFoundTemplate, scale);
            using var scaledTemplateGray = new Mat();
            Cv2.CvtColor(scaledTemplate, scaledTemplateGray, ColorConversionCodes.BGR2GRAY);
            if (scaledTemplateGray.Width > searchRegionGray.Width || scaledTemplateGray.Height > searchRegionGray.Height)
            {
                continue;
            }

            using var result = new Mat();
            Cv2.MatchTemplate(searchRegionGray, scaledTemplateGray, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out var score, out _, out var locationPoint);
            var bounds = new Rect(
                searchBounds.X + locationPoint.X,
                searchBounds.Y + locationPoint.Y,
                scaledTemplateGray.Width,
                scaledTemplateGray.Height);
            if (bestLocation is null || score > bestLocation.Value.Score)
            {
                bestLocation = new TemplateLocation(bounds, score);
            }
        }

        if (bestLocation is null || bestLocation.Value.Score < MinimumTemplateMatchScore)
        {
            return false;
        }

        foundBounds = bestLocation.Value.Bounds;
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

    private readonly record struct TemplateLocation(Rect Bounds, double Score);
}
