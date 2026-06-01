using Automaton.Helpers;
using Automaton.Infrastructure;
using Automaton.Primitives;
using OpenCvSharp;

namespace Automaton.Detectors;

internal sealed class MineOverviewDetector : IDisposable
{
    private const double MinimumHeaderMatchScore = 0.90;
    private const double EarlyExitScore = 0.95;
    private static readonly double[] TemplateScales = [1.0, 0.95, 1.05];

    private readonly Mat m_OverviewMineTemplate = EmbeddedResourceLoader.LoadMat("overview.overview_mine.png");

    public void Dispose()
    {
        m_OverviewMineTemplate.Dispose();
    }

    public MineOverviewAnalysis Detect(Mat screen)
    {
        if (screen.Empty())
        {
            return MineOverviewAnalysis.NotFound;
        }

        var searchBounds = BuildFallbackSearchBounds(screen.Size());
        if (!TryMatchTemplate(screen, m_OverviewMineTemplate, searchBounds, out var headerLocation))
        {
            return new MineOverviewAnalysis(false, searchBounds, null, null);
        }

        var mineOverviewBounds = BuildMineOverviewBounds(screen.Size(), headerLocation.Bounds);
        return new MineOverviewAnalysis(true, searchBounds, headerLocation.Bounds, mineOverviewBounds);
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

    private static Rect BuildMineOverviewBounds(Size imageSize, Rect overviewMineHeaderBounds)
    {
        var left = Math.Clamp(overviewMineHeaderBounds.X - 10, 0, imageSize.Width);
        var top = Math.Clamp(overviewMineHeaderBounds.Y - 10, 0, imageSize.Height);
        var right = Math.Clamp(left + Settings.MineOverviewWidth, left, imageSize.Width);
        var bottom = Math.Clamp(top + Settings.MineOverviewHeight, top, imageSize.Height);
        return new Rect(left, top, right - left, bottom - top);
    }

    private static Mat BuildScaledTemplate(Mat template, double scale)
    {
        var width = Math.Max(1, (int)Math.Round(template.Width * scale));
        var height = Math.Max(1, (int)Math.Round(template.Height * scale));
        var scaledTemplate = new Mat();
        Cv2.Resize(template, scaledTemplate, new Size(width, height));
        return scaledTemplate;
    }

    private static Rect BuildFallbackSearchBounds(Size imageSize)
    {
        return GeometryHelper.BuildRelativeBounds(imageSize, 0.62, 0.52, 0.35, 0.45);
    }

    private readonly record struct TemplateLocation(Rect Bounds, double Score);
}

internal sealed record MineOverviewAnalysis(
    bool MineOverviewLocated,
    Rect SearchBounds,
    Rect? HeaderBounds,
    Rect? MineOverviewBounds)
{
    public static MineOverviewAnalysis NotFound { get; } = new(false, new Rect(0, 0, 1, 1), null, null);
}
