using Automaton.Infrastructure;
using Automaton.Primitives;
using OpenCvSharp;

namespace Automaton.Detectors;

internal sealed class InventoryDetector : IDisposable
{
    private const double MinimumTitleMatchScore = 0.82;
    private static readonly double[] TemplateScales = [1.0, 0.95, 1.05];

    private readonly Mat m_ItemHangarTemplate = EmbeddedResourceLoader.LoadMat("mining.item_hangar.png");
    private readonly Mat m_MiningHoldTemplate = EmbeddedResourceLoader.LoadMat("mining.mining_hold.png");

    public void Dispose()
    {
        m_ItemHangarTemplate.Dispose();
        m_MiningHoldTemplate.Dispose();
    }

    public InventoryAnalysis Detect(Mat screen, bool drawDebugOverlay = true)
    {
        if (screen.Empty())
        {
            return InventoryAnalysis.NotFound;
        }

        var itemHangarFound = TryLocateTitle(screen, m_ItemHangarTemplate, Settings.ItemHangarBounds, out var itemHangarTitleBounds);
        var miningHoldFound = TryLocateTitle(screen, m_MiningHoldTemplate, Settings.MiningHoldBounds, out var miningHoldTitleBounds);

        return new InventoryAnalysis(
            miningHoldTitleBounds,
            itemHangarTitleBounds,
            miningHoldFound ? Settings.MiningHoldFirstRowBounds : null,
            itemHangarFound ? Settings.ItemHangarFirstRowBounds : null);
    }

    private static bool TryLocateTitle(Mat screen, Mat template, Rect searchBounds, out Rect? titleBounds)
    {
        titleBounds = null;
        var clampedBounds = ClampBounds(searchBounds, screen.Size());
        using var searchRegion = new Mat(screen, clampedBounds);
        TemplateMatch? bestMatch = null;
        foreach (var scale in TemplateScales)
        {
            using var scaledTemplate = BuildScaledTemplate(template, scale);
            if (scaledTemplate.Width > searchRegion.Width || scaledTemplate.Height > searchRegion.Height)
            {
                continue;
            }

            using var result = new Mat();
            Cv2.MatchTemplate(searchRegion, scaledTemplate, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out var maxScore, out _, out var maxLocation);
            var candidate = new TemplateMatch(
                new Rect(
                    clampedBounds.X + maxLocation.X,
                    clampedBounds.Y + maxLocation.Y,
                    scaledTemplate.Width,
                    scaledTemplate.Height),
                maxScore);
            if (bestMatch is null || candidate.Score > bestMatch.Value.Score)
            {
                bestMatch = candidate;
            }
        }

        if (bestMatch is null || bestMatch.Value.Score < MinimumTitleMatchScore)
        {
            return false;
        }

        titleBounds = bestMatch.Value.Bounds;
        return true;
    }

    private static Rect ClampBounds(Rect bounds, Size imageSize)
    {
        var x = Math.Clamp(bounds.X, 0, Math.Max(0, imageSize.Width - 1));
        var y = Math.Clamp(bounds.Y, 0, Math.Max(0, imageSize.Height - 1));
        var width = Math.Clamp(bounds.Width, 1, imageSize.Width - x);
        var height = Math.Clamp(bounds.Height, 1, imageSize.Height - y);
        return new Rect(x, y, width, height);
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

    private readonly record struct TemplateMatch(Rect Bounds, double Score);
}

internal sealed record InventoryAnalysis(
    Rect? MiningHoldTitleBounds,
    Rect? ItemHangarTitleBounds,
    Rect? MiningHoldFirstRowBounds,
    Rect? ItemHangarFirstRowBounds)
{
    public static InventoryAnalysis NotFound { get; } = new(null, null, null, null);
}
