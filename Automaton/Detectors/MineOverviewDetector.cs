using System.Drawing.Imaging;
using System.IO;
using OpenCvSharp;

namespace Automaton.Detectors;

internal sealed class MineOverviewDetector
{
    private const double MinimumHeaderMatchScore = 0.90;
    private static readonly double[] TemplateScales = [1.0, 0.95, 1.05];

    private readonly Mat m_OverviewMineTemplate = LoadTemplate(Properties.Resources.overview_mine, "overview_mine");

    public bool TryLocate(Mat screen, Rect asteroidBeltLabelBounds, out Rect mineOverviewBounds)
    {
        mineOverviewBounds = default;
        if (screen.Empty())
        {
            return false;
        }

        var searchBounds = BuildLabelAnchoredSearchBounds(screen.Size(), asteroidBeltLabelBounds);
        return TryLocateByTemplate(screen, searchBounds, out mineOverviewBounds);
    }

    public bool TryLocate(Mat screen, out Rect mineOverviewBounds)
    {
        mineOverviewBounds = default;
        if (screen.Empty())
        {
            return false;
        }

        var searchBounds = BuildFallbackSearchBounds(screen.Size());
        return TryLocateByTemplate(screen, searchBounds, out mineOverviewBounds);
    }

    private bool TryLocateByTemplate(Mat screen, Rect searchBounds, out Rect mineOverviewBounds)
    {
        mineOverviewBounds = default;
        if (!TryMatchTemplate(screen, m_OverviewMineTemplate, searchBounds, out var headerLocation))
        {
            return false;
        }

        mineOverviewBounds = BuildMineOverviewBounds(screen.Size(), headerLocation.Bounds);
        return true;
    }

    private static bool TryMatchTemplate(Mat screen, Mat template, Rect searchBounds, out TemplateLocation location)
    {
        location = default;
        using var searchRegion = new Mat(screen, searchBounds);
        TemplateLocation? bestLocation = null;
        foreach (var scale in TemplateScales)
        {
            using var scaledTemplate = BuildScaledTemplate(template, scale);
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
        var left = Math.Clamp(overviewMineHeaderBounds.X - 10, 0, Math.Max(0, imageSize.Width - 1));
        var top = Math.Clamp(overviewMineHeaderBounds.Y - 40, 0, Math.Max(0, imageSize.Height - 1));
        var right = Math.Clamp(left + 180, left + 1, imageSize.Width);
        var bottom = Math.Clamp(top + 420, top + 1, imageSize.Height);
        return new Rect(left, top, right - left, bottom - top);
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

    private static Rect BuildLabelAnchoredSearchBounds(Size imageSize, Rect labelBounds)
    {
        var left = Math.Clamp(labelBounds.Right + 20, 0, Math.Max(0, imageSize.Width - 1));
        var top = Math.Clamp(labelBounds.Top - 520, 0, Math.Max(0, imageSize.Height - 1));
        var right = Math.Clamp(imageSize.Width, left + 1, imageSize.Width);
        var bottom = Math.Clamp(labelBounds.Top + 80, top + 1, imageSize.Height);
        return new Rect(left, top, right - left, bottom - top);
    }

    private static Rect BuildFallbackSearchBounds(Size imageSize)
    {
        return BuildRelativeBounds(imageSize, 0.62, 0.52, 0.35, 0.45);
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
