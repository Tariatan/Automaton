using Automaton.Infrastructure;
using OpenCvSharp;

namespace Automaton.Detectors;

internal sealed class HomeStationDetector(AsteroidBeltOverviewDetector asteroidBeltOverviewDetector)
{
    private const double MinimumMatchScore = 0.76;
    private static readonly double[] TemplateScales = [0.80, 0.90, 1.0, 1.10, 1.20, 1.30];

    private readonly Mat m_HomeStationTemplate = EmbeddedResourceLoader.LoadMat("overview.home_station.png");

    public HomeStationAnalysis Detect(Mat screen, bool drawDebugOverlay = true)
    {
        var overviewAnalysis = asteroidBeltOverviewDetector.Detect(screen, false);
        if (!overviewAnalysis.OverviewLocated || overviewAnalysis.OverviewBounds is null)
        {
            return new HomeStationAnalysis(false, null, 0, overviewAnalysis);
        }

        if (!TryMatchHomeStation(screen, overviewAnalysis.OverviewBounds.Value, out var homeStationBounds, out var bestScore))
        {
            return new HomeStationAnalysis(false, null, bestScore, overviewAnalysis);
        }

        return new HomeStationAnalysis(true, homeStationBounds, bestScore, overviewAnalysis);
    }

    private bool TryMatchHomeStation(Mat screen, Rect overviewBounds, out Rect homeStationBounds, out double bestScore)
    {
        homeStationBounds = default;
        bestScore = 0;
        var searchBounds = BuildHomeStationSearchBounds(screen.Size(), overviewBounds);
        if (!TryCreateRegion(screen, searchBounds, out var searchRegion))
        {
            return false;
        }

        using (searchRegion)
        {
            TemplateLocation? bestLocation = null;
            foreach (var scale in TemplateScales)
            {
                using var scaledTemplate = BuildScaledTemplate(m_HomeStationTemplate, scale);
                if (scaledTemplate.Width > searchRegion.Width || scaledTemplate.Height > searchRegion.Height)
                {
                    continue;
                }

                using var result = new Mat();
                Cv2.MatchTemplate(searchRegion, scaledTemplate, result, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(result, out _, out var score, out _, out var locationPoint);
                var candidateBounds = new Rect(
                    searchBounds.X + locationPoint.X,
                    searchBounds.Y + locationPoint.Y,
                    scaledTemplate.Width,
                    scaledTemplate.Height);
                if (bestLocation is null || score > bestLocation.Value.Score)
                {
                    bestLocation = new TemplateLocation(candidateBounds, score);
                }
            }

            bestScore = bestLocation?.Score ?? 0;
            if (bestLocation is null || bestLocation.Value.Score < MinimumMatchScore)
            {
                return false;
            }

            homeStationBounds = bestLocation.Value.Bounds;
            return true;
        }
    }

    private static Rect BuildHomeStationSearchBounds(Size imageSize, Rect overviewBounds)
    {
        var left = Math.Clamp(overviewBounds.X + 2, 0, Math.Max(0, imageSize.Width - 1));
        var top = Math.Clamp(overviewBounds.Y + 70, 0, Math.Max(0, imageSize.Height - 1));
        var right = Math.Clamp(overviewBounds.Right - 4, left + 1, imageSize.Width);
        var bottom = Math.Clamp(overviewBounds.Y + 250, top + 1, imageSize.Height);
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

    private readonly record struct TemplateLocation(Rect Bounds, double Score);
}

internal sealed record HomeStationAnalysis(
    bool HomeStationLocated,
    Rect? HomeStationBounds,
    double BestMatchScore,
    AsteroidBeltOverviewAnalysis OverviewAnalysis);
