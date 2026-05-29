using Automaton.Infrastructure;
using OpenCvSharp;

namespace Automaton.Detectors;

internal sealed class WarOverviewDetector : IDisposable
{
    private const double MinimumHeaderMatchScore = 0.90;
    private const double EarlyExitScore = 0.95;
    private static readonly double[] TemplateScales = [1.0, 0.95, 1.05];

    private readonly Mat m_OverviewWarTemplate = EmbeddedResourceLoader.LoadMat("overview.overview_war.png");

    public void Dispose()
    {
        m_OverviewWarTemplate.Dispose();
    }

    public bool Detect(Mat screen, out Rect warOverviewBounds)
    {
        warOverviewBounds = default;
        if (screen.Empty())
        {
            return false;
        }

        if (!TryLocateByTemplate(screen, out warOverviewBounds, out _))
        {
            return false;
        }

        return true;
    }

    private bool TryLocateByTemplate(Mat screen, out Rect warOverviewBounds, out Rect headerBounds)
    {
        warOverviewBounds = default;
        headerBounds = default;
        TemplateLocation? bestHeaderLocation = null;
        foreach (var searchBounds in BuildFallbackSearchBounds(screen.Size()))
        {
            if (!TryMatchTemplate(screen, m_OverviewWarTemplate, searchBounds, out var headerLocation))
            {
                continue;
            }

            if (bestHeaderLocation is null || headerLocation.Score > bestHeaderLocation.Value.Score)
            {
                bestHeaderLocation = headerLocation;
            }
        }

        if (bestHeaderLocation is null)
        {
            return false;
        }

        headerBounds = bestHeaderLocation.Value.Bounds;
        warOverviewBounds = BuildWarOverviewBounds(screen.Size(), bestHeaderLocation.Value.Bounds);
        return true;
    }

    private static bool TryMatchTemplate(Mat screen, Mat template, Rect searchBounds, out TemplateLocation location)
    {
        location = default;
        using var searchRegion = new Mat(screen, searchBounds);
        TemplateLocation? bestLocation = null;
        foreach (var scale in TemplateScales)
        {
            var ownsScaled = !IsUnscaled(scale);
            var scaledTemplate = ownsScaled ? BuildScaledTemplate(template, scale) : template;
            try
            {
                if (scaledTemplate.Width > searchRegion.Width ||
                    scaledTemplate.Height > searchRegion.Height)
                {
                    continue;
                }

                using var result = new Mat();
                Cv2.MatchTemplate(searchRegion, scaledTemplate, result, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(result, out _, out var score, out _, out var locationPoint);
                var bounds = new Rect(
                    searchBounds.X + locationPoint.X,
                    searchBounds.Y + locationPoint.Y,
                    scaledTemplate.Width,
                    scaledTemplate.Height);
                if (bestLocation is null || score > bestLocation.Value.Score)
                {
                    bestLocation = new TemplateLocation(bounds, score);
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

        if (bestLocation is null || bestLocation.Value.Score < MinimumHeaderMatchScore)
        {
            return false;
        }

        location = bestLocation.Value;
        return true;
    }

    private static Rect BuildWarOverviewBounds(Size imageSize, Rect overviewWarHeaderBounds)
    {
        var left = Math.Clamp(overviewWarHeaderBounds.X - 10, 0, Math.Max(0, imageSize.Width - 1));
        var top = Math.Clamp(overviewWarHeaderBounds.Y - 40, 0, Math.Max(0, imageSize.Height - 1));
        var right = Math.Clamp(left + 180, left + 1, imageSize.Width);
        var bottom = Math.Clamp(top + 420, top + 1, imageSize.Height);
        return new Rect(left, top, right - left, bottom - top);
    }

    private static bool IsUnscaled(double scale) => Math.Abs(scale - 1.0) < double.Epsilon;

    private static Mat BuildScaledTemplate(Mat template, double scale)
    {
        var width = Math.Max(1, (int)Math.Round(template.Width * scale));
        var height = Math.Max(1, (int)Math.Round(template.Height * scale));
        var scaledTemplate = new Mat();
        Cv2.Resize(template, scaledTemplate, new Size(width, height));
        return scaledTemplate;
    }

    private static IEnumerable<Rect> BuildFallbackSearchBounds(Size imageSize)
    {
        yield return BuildRelativeBounds(imageSize, 0.62, 0.65, 0.35, 0.32);
        yield return BuildRelativeBounds(imageSize, 0.62, 0.52, 0.35, 0.45);
        yield return BuildRelativeBounds(imageSize, 0.58, 0.45, 0.39, 0.50);
    }

    private static Rect BuildRelativeBounds(
        Size imageSize,
        double leftRatio,
        double topRatio,
        double widthRatio,
        double heightRatio)
    {
        var left = (int)Math.Round(imageSize.Width * leftRatio);
        var top = (int)Math.Round(imageSize.Height * topRatio);
        var width = (int)Math.Round(imageSize.Width * widthRatio);
        var height = (int)Math.Round(imageSize.Height * heightRatio);

        left = Math.Clamp(left, 0, Math.Max(0, imageSize.Width - 1));
        top = Math.Clamp(top, 0, Math.Max(0, imageSize.Height - 1));
        width = Math.Clamp(width, 1, imageSize.Width - left);
        height = Math.Clamp(height, 1, imageSize.Height - top);
        return new Rect(left, top, width, height);
    }

    private readonly record struct TemplateLocation(Rect Bounds, double Score);
}
