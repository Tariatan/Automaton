using System.Drawing.Imaging;
using System.IO;
using OpenCvSharp;

namespace Automaton.Detectors;

internal sealed class HomeStationDetector
{
    private const double MinimumMatchScore = 0.86;
    private static readonly double[] TemplateScales = [1.0, 0.95, 1.05];

    private readonly AsteroidBeltOverviewDetector m_AsteroidBeltOverviewDetector;
    private readonly Mat m_HomeStationTemplate = LoadTemplate(Properties.Resources.home_station, "home_station");

    public HomeStationDetector()
        : this(new AsteroidBeltOverviewDetector())
    {
    }

    internal HomeStationDetector(AsteroidBeltOverviewDetector asteroidBeltOverviewDetector)
    {
        m_AsteroidBeltOverviewDetector = asteroidBeltOverviewDetector;
    }

    public HomeStationAnalysis Analyze(Mat screen)
    {
        var overviewAnalysis = m_AsteroidBeltOverviewDetector.Analyze(screen);
        if (!overviewAnalysis.OverviewLocated || overviewAnalysis.OverviewBounds is null)
        {
            return new HomeStationAnalysis(false, null, overviewAnalysis);
        }

        if (!TryMatchHomeStation(screen, overviewAnalysis.OverviewBounds.Value, out var homeStationBounds))
        {
            return new HomeStationAnalysis(false, null, overviewAnalysis);
        }

        return new HomeStationAnalysis(true, homeStationBounds, overviewAnalysis);
    }

    private bool TryMatchHomeStation(Mat screen, Rect overviewBounds, out Rect homeStationBounds)
    {
        homeStationBounds = default;
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
        var left = Math.Clamp(overviewBounds.X + 4, 0, Math.Max(0, imageSize.Width - 1));
        var top = Math.Clamp(overviewBounds.Y + 98, 0, Math.Max(0, imageSize.Height - 1));
        var right = Math.Clamp(overviewBounds.Right - 8, left + 1, imageSize.Width);
        var bottom = Math.Clamp(overviewBounds.Y + 198, top + 1, imageSize.Height);
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

    private static Mat LoadTemplate(System.Drawing.Bitmap bitmap, string resourceName)
    {
        using var memoryStream = new MemoryStream();
        bitmap.Save(memoryStream, ImageFormat.Png);
        var template = Cv2.ImDecode(memoryStream.ToArray(), ImreadModes.Color);
        if (template.Empty())
        {
            throw new InvalidOperationException($"Could not load {resourceName} template from resources.");
        }

        return template;
    }

    private readonly record struct TemplateLocation(Rect Bounds, double Score);
}

internal sealed record HomeStationAnalysis(
    bool HomeStationLocated,
    Rect? HomeStationBounds,
    AsteroidBeltOverviewAnalysis OverviewAnalysis);
