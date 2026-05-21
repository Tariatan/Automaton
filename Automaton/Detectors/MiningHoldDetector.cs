using Automaton.Infrastructure;
using Automaton.Primitives;
using OpenCvSharp;

namespace Automaton.Detectors;

internal sealed class MiningHoldDetector
{
    private const double MinimumTitleMatchScore = 0.82;
    private const int FirstRowMinimumBrightPixelCount = 220;
    private static readonly double[] TemplateScales = [1.0, 0.95, 1.05];

    private readonly Mat m_ItemHangarTemplate = EmbeddedResourceLoader.LoadMat("mining.item_hangar.png");
    private readonly Mat m_MiningHoldTemplate = EmbeddedResourceLoader.LoadMat("mining.mining_hold.png");

    public DockedScreenAnalysis Analyze(Mat screen)
    {
        if (screen.Empty())
        {
            return DockedScreenAnalysis.NotFound;
        }

        var searchBounds = BuildTopLeftSearchBounds(screen.Size());
        Rect? itemHangarTitleBounds = TryLocateTitle(screen, m_ItemHangarTemplate, searchBounds, out var locatedItemHangarTitleBounds)
            ? locatedItemHangarTitleBounds
            : BuildFallbackItemHangarTitleBounds(screen.Size());
        Rect? miningHoldTitleBounds = TryLocateTitle(screen, m_MiningHoldTemplate, searchBounds, out var locatedMiningHoldTitleBounds)
            ? locatedMiningHoldTitleBounds
            : BuildFallbackMiningHoldTitleBounds(screen.Size());
        Rect? itemHangarFirstRowBounds = Settings.ItemHangarFirstRowBounds;
        Rect? miningHoldFirstRowBounds = Settings.MiningHoldFirstRowBounds;
        var itemHangarFocused = itemHangarFirstRowBounds is not null && RowLooksFocused(screen, itemHangarFirstRowBounds.Value);
        var miningHoldFocused = miningHoldFirstRowBounds is not null && RowLooksFocused(screen, miningHoldFirstRowBounds.Value);
        var miningHoldContent = miningHoldFirstRowBounds is null
            ? MiningHoldContentState.Unknown
            : (RowLooksPopulated(screen, miningHoldFirstRowBounds.Value)
                ? MiningHoldContentState.ContainsOre
                : MiningHoldContentState.Empty);

        return new DockedScreenAnalysis(
            miningHoldTitleBounds,
            itemHangarTitleBounds,
            miningHoldFirstRowBounds,
            itemHangarFirstRowBounds,
            miningHoldFocused,
            itemHangarFocused,
            miningHoldContent);
    }

    private static bool TryLocateTitle(Mat screen, Mat template, Rect searchBounds, out Rect titleBounds)
    {
        titleBounds = default;
        using var searchRegion = new Mat(screen, searchBounds);
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
                    searchBounds.X + maxLocation.X,
                    searchBounds.Y + maxLocation.Y,
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

    private static Rect BuildTopLeftSearchBounds(Size imageSize)
    {
        return BuildRelativeBounds(imageSize, 0.01, 0.02, 0.30, 0.45);
    }

    private static Rect BuildFallbackItemHangarTitleBounds(Size imageSize)
    {
        return BuildRelativeBounds(imageSize, 0.025, 0.025, 0.155, 0.030);
    }

    private static Rect BuildFallbackMiningHoldTitleBounds(Size imageSize)
    {
        return BuildRelativeBounds(imageSize, 0.025, 0.142, 0.155, 0.030);
    }

    private static bool RowLooksFocused(Mat screen, Rect rowBounds)
    {
        using var row = new Mat(screen, rowBounds);
        using var hsv = new Mat();
        using var mask = new Mat();
        Cv2.CvtColor(row, hsv, ColorConversionCodes.BGR2HSV);
        Cv2.InRange(hsv, new Scalar(85, 35, 25), new Scalar(110, 220, 140), mask);
        return Cv2.CountNonZero(mask) >= 120;
    }

    private static bool RowLooksPopulated(Mat screen, Rect rowBounds)
    {
        using var row = new Mat(screen, rowBounds);
        using var gray = new Mat();
        using var mask = new Mat();
        Cv2.CvtColor(row, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.InRange(gray, new Scalar(110), new Scalar(255), mask);
        return Cv2.CountNonZero(mask) >= FirstRowMinimumBrightPixelCount;
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

internal sealed record DockedScreenAnalysis(
    Rect? MiningHoldTitleBounds,
    Rect? ItemHangarTitleBounds,
    Rect? MiningHoldFirstRowBounds,
    Rect? ItemHangarFirstRowBounds,
    bool MiningHoldFocused,
    bool ItemHangarFocused,
    MiningHoldContentState MiningHoldContent)
{
    public Rect? MiningHoldEntryBounds => MiningHoldTitleBounds;
    public Rect? ItemHangarEntryBounds => ItemHangarTitleBounds;

    public static DockedScreenAnalysis NotFound { get; } = new(
        null,
        null,
        null,
        null,
        false,
        false,
        MiningHoldContentState.Unknown);
}

internal enum MiningHoldContentState
{
    Unknown,
    Empty,
    ContainsOre
}
