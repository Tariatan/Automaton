using Automaton.Infrastructure;
using OpenCvSharp;

namespace Automaton.Detectors;

internal sealed class LocationChangeTimerDetector : IDisposable
{
    private const double MinimumMatchScore = 0.90;
    private const double EarlyExitScore = 0.95;
    private static readonly Rect SearchBounds = new(130, 50, 50, 50);
    private static readonly double[] TemplateScales = [1.0, 0.95, 1.05];

    private readonly Mat m_Template = EmbeddedResourceLoader.LoadMat("location_change_timer.png");

    public void Dispose() => m_Template.Dispose();

    public bool Detect(Mat screen, out LocationChangeTimerLocation location)
    {
        location = default;
        if (screen.Empty())
        {
            return false;
        }

        var searchBounds = BuildSearchBounds(screen.Size());
        using var searchRegion = new Mat(screen, searchBounds);
        LocationChangeTimerLocation? bestLocation = null;
        foreach (var scale in TemplateScales)
        {
            var ownsScaled = scale != 1.0;
            var scaledTemplate = ownsScaled ? BuildScaledTemplate(scale) : m_Template;
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
                    bestLocation = new LocationChangeTimerLocation(bounds, score);
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

    private static Rect BuildSearchBounds(Size imageSize)
    {
        var x = Math.Clamp(SearchBounds.X, 0, Math.Max(0, imageSize.Width - 1));
        var y = Math.Clamp(SearchBounds.Y, 0, Math.Max(0, imageSize.Height - 1));
        var width = Math.Clamp(SearchBounds.Width, 1, imageSize.Width - x);
        var height = Math.Clamp(SearchBounds.Height, 1, imageSize.Height - y);
        return new Rect(x, y, width, height);
    }

    private Mat BuildScaledTemplate(double scale)
    {
        var width = Math.Max(1, (int)Math.Round(m_Template.Width * scale));
        var height = Math.Max(1, (int)Math.Round(m_Template.Height * scale));
        var scaledTemplate = new Mat();
        Cv2.Resize(m_Template, scaledTemplate, new Size(width, height));
        return scaledTemplate;
    }
}

internal readonly record struct LocationChangeTimerLocation(Rect Bounds, double Score);
