using Automaton.Helpers;
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

    public WarOverviewAnalysis Detect(Mat screen)
    {
        if (screen.Empty())
        {
            return WarOverviewAnalysis.NotFound;
        }

        TemplateLocation? bestHeaderLocation = null;
        foreach (var searchBounds in BuildSearchBounds(screen.Size()))
        {
            if (!TryMatchTemplate(screen, m_OverviewWarTemplate, searchBounds, out var headerLocation))
            {
                continue;
            }

            if (bestHeaderLocation is null || headerLocation.Score > bestHeaderLocation.Value.Score)
            {
                bestHeaderLocation = headerLocation;
            }

            if (bestHeaderLocation.Value.Score >= EarlyExitScore)
            {
                break;
            }
        }

        if (bestHeaderLocation is null)
        {
            return WarOverviewAnalysis.NotFound;
        }

        var warOverviewBounds = BuildWarOverviewBounds(screen.Size(), bestHeaderLocation.Value.Bounds);
        return new WarOverviewAnalysis(true, bestHeaderLocation.Value.Bounds, warOverviewBounds);
    }

    private static bool TryMatchTemplate(Mat screen, Mat template, Rect searchBounds, out TemplateLocation location)
    {
        location = default;
        using var searchRegion = new Mat(screen, searchBounds);
        TemplateLocation? bestLocation = null;
        foreach (var scale in TemplateScales)
        {
            var ownsScaled = !GeometryHelper.IsUnscaled(scale);
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

    private static Rect BuildWarOverviewBounds(Size imageSize, Rect headerBounds)
    {
        return GeometryHelper.BuildClampedBounds(
            headerBounds.X - 10,
            headerBounds.Y - 40,
            180,
            420,
            imageSize);
    }

    private static Mat BuildScaledTemplate(Mat template, double scale)
    {
        var width = Math.Max(1, (int)Math.Round(template.Width * scale));
        var height = Math.Max(1, (int)Math.Round(template.Height * scale));
        var scaledTemplate = new Mat();
        Cv2.Resize(template, scaledTemplate, new Size(width, height));
        return scaledTemplate;
    }

    private static IEnumerable<Rect> BuildSearchBounds(Size imageSize)
    {
        yield return GeometryHelper.BuildRelativeBounds(imageSize, 0.62, 0.65, 0.35, 0.32);
        yield return GeometryHelper.BuildRelativeBounds(imageSize, 0.62, 0.52, 0.35, 0.45);
        yield return GeometryHelper.BuildRelativeBounds(imageSize, 0.58, 0.45, 0.39, 0.50);
    }

    private readonly record struct TemplateLocation(Rect Bounds, double Score);
}

internal sealed record WarOverviewAnalysis(
    bool WarOverviewLocated,
    Rect? HeaderBounds,
    Rect? WarOverviewBounds)
{
    public static WarOverviewAnalysis NotFound { get; } = new(false, null, null);
}
