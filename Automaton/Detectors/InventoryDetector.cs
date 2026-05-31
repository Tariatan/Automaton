using Automaton.Infrastructure;
using OpenCvSharp;

namespace Automaton.Detectors;

internal sealed class InventoryDetector : IDisposable
{
    private const double MinimumTitleMatchScore = 0.82;
    private const int FirstRowTopOffsetFromTemplateBottom = 110;
    private const int FirstRowWidth = 300;
    private const int FirstRowHeight = 30;
    private static readonly double[] TemplateScales = [1.0, 0.95, 1.05];

    private readonly Mat m_ItemHangarTemplate = EmbeddedResourceLoader.LoadMat("mining.item_hangar.png");
    private readonly Mat m_MiningHoldTemplate = EmbeddedResourceLoader.LoadMat("mining.mining_hold.png");

    public void Dispose()
    {
        m_ItemHangarTemplate.Dispose();
        m_MiningHoldTemplate.Dispose();
    }

    public InventoryAnalysis Detect(Mat screen)
    {
        if (screen.Empty())
        {
            return InventoryAnalysis.NotFound;
        }

        var itemHangarFound = TryLocateTitle(screen, m_ItemHangarTemplate, out var itemHangarTitleBounds);
        var miningHoldFound = TryLocateTitle(screen, m_MiningHoldTemplate, out var miningHoldTitleBounds);

        return new InventoryAnalysis(
            miningHoldTitleBounds,
            itemHangarTitleBounds,
            miningHoldFound ? BuildFirstRowBounds(miningHoldTitleBounds!.Value, screen.Size()) : null,
            itemHangarFound ? BuildFirstRowBounds(itemHangarTitleBounds!.Value, screen.Size()) : null);
    }

    private static bool TryLocateTitle(Mat screen, Mat template, out Rect? titleBounds)
    {
        const double EarlyExitScore = 0.95;

        titleBounds = null;
        TemplateMatch? bestMatch = null;
        foreach (var scale in TemplateScales)
        {
            var ownsTemplate = !IsUnscaled(scale);
            var effectiveTemplate = ownsTemplate ? BuildScaledTemplate(template, scale) : template;
            try
            {
                if (effectiveTemplate.Width > screen.Width || effectiveTemplate.Height > screen.Height)
                {
                    continue;
                }

                using var result = new Mat();
                Cv2.MatchTemplate(screen, effectiveTemplate, result, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(result, out _, out var maxScore, out _, out var maxLocation);
                var candidate = new TemplateMatch(
                    new Rect(
                        maxLocation.X,
                        maxLocation.Y,
                        effectiveTemplate.Width,
                        effectiveTemplate.Height),
                    maxScore);
                if (bestMatch is null || candidate.Score > bestMatch.Value.Score)
                {
                    bestMatch = candidate;
                }

                if (bestMatch.Value.Score >= EarlyExitScore)
                {
                    break;
                }
            }
            finally
            {
                if (ownsTemplate)
                {
                    effectiveTemplate.Dispose();
                }
            }
        }

        if (bestMatch is null || bestMatch.Value.Score < MinimumTitleMatchScore)
        {
            return false;
        }

        titleBounds = bestMatch.Value.Bounds;
        return true;
    }

    private static Rect BuildFirstRowBounds(Rect titleBounds, Size screenSize)
    {
        var firstRowBounds = new Rect(
            titleBounds.Left,
            titleBounds.Bottom + FirstRowTopOffsetFromTemplateBottom,
            FirstRowWidth,
            FirstRowHeight);
        return ClampToScreen(firstRowBounds, screenSize);
    }

    private static bool IsUnscaled(double scale) => Math.Abs(scale - 1.0) < double.Epsilon;

    private static Rect ClampToScreen(Rect bounds, Size imageSize)
    {
        var x = Math.Clamp(bounds.X, 0, Math.Max(0, imageSize.Width - 1));
        var y = Math.Clamp(bounds.Y, 0, Math.Max(0, imageSize.Height - 1));
        var width = Math.Clamp(bounds.Width, 1, Math.Max(1, imageSize.Width - x));
        var height = Math.Clamp(bounds.Height, 1, Math.Max(1, imageSize.Height - y));
        return new Rect(x, y, width, height);
    }

    private static Mat BuildScaledTemplate(Mat template, double scale)
    {
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
