using OpenCvSharp;

namespace Automaton.Detectors;

internal sealed class MiningHoldDetector
{
    private const int BinaryMaskMaxValue = 255;
    private const double MinimumFocusedEntryBlueRatio = 0.18;
    private const int MinimumOreContourArea = 450;
    private const int MinimumOreContourWidth = 24;
    private const int MinimumOreContourHeight = 24;
    private static readonly Scalar BlueUiMinimum = new(85, 35, 25);
    private static readonly Scalar BlueUiMaximum = new(110, 220, 140);

    public DockedScreenAnalysis Analyze(Mat screen)
    {
        if (screen.Empty())
        {
            return DockedScreenAnalysis.NotFound;
        }

        var imageSize = screen.Size();
        var miningHoldEntryBounds = BuildMiningHoldEntryBounds(imageSize);
        var itemHangarEntryBounds = BuildItemHangarEntryBounds(imageSize);
        var miningHoldFocused = HasFocusedEntryHighlight(screen, miningHoldEntryBounds);
        var itemHangarFocused = HasFocusedEntryHighlight(screen, itemHangarEntryBounds);
        var miningHoldContent = miningHoldFocused
            ? DetectMiningHoldContent(screen, BuildMiningHoldContentBounds(imageSize))
            : MiningHoldContentState.Unknown;

        return new DockedScreenAnalysis(
            miningHoldEntryBounds,
            itemHangarEntryBounds,
            miningHoldFocused,
            itemHangarFocused,
            miningHoldContent);
    }

    private static bool HasFocusedEntryHighlight(Mat screen, Rect entryBounds)
    {
        using var entry = new Mat(screen, entryBounds);
        using var blueMask = BuildBlueUiMask(entry);
        var bluePixels = Cv2.CountNonZero(blueMask);
        return bluePixels >= entryBounds.Width * entryBounds.Height * MinimumFocusedEntryBlueRatio;
    }

    private static MiningHoldContentState DetectMiningHoldContent(Mat screen, Rect contentBounds)
    {
        using var content = new Mat(screen, contentBounds);
        using var gray = new Mat();
        using var brightMask = new Mat();
        using var closed = new Mat();
        using var closeKernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));

        Cv2.CvtColor(content, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.Threshold(gray, brightMask, 115, BinaryMaskMaxValue, ThresholdTypes.Binary);
        Cv2.MorphologyEx(brightMask, closed, MorphTypes.Close, closeKernel);
        Cv2.FindContours(
            closed,
            out var contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area < MinimumOreContourArea)
            {
                continue;
            }

            var bounds = Cv2.BoundingRect(contour);
            if (bounds is { Width: >= MinimumOreContourWidth, Height: >= MinimumOreContourHeight })
            {
                return MiningHoldContentState.ContainsOre;
            }
        }

        return MiningHoldContentState.Empty;
    }

    private static Mat BuildBlueUiMask(Mat image)
    {
        using var hsv = new Mat();
        var mask = new Mat();
        Cv2.CvtColor(image, hsv, ColorConversionCodes.BGR2HSV);
        Cv2.InRange(hsv, BlueUiMinimum, BlueUiMaximum, mask);
        return mask;
    }

    private static Rect BuildMiningHoldEntryBounds(Size imageSize)
    {
        return BuildRelativeBounds(imageSize, 0.025, 0.834, 0.095, 0.026);
    }

    private static Rect BuildItemHangarEntryBounds(Size imageSize)
    {
        return BuildRelativeBounds(imageSize, 0.025, 0.877, 0.095, 0.026);
    }

    private static Rect BuildMiningHoldContentBounds(Size imageSize)
    {
        return BuildRelativeBounds(imageSize, 0.125, 0.805, 0.070, 0.115);
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
}

internal sealed record DockedScreenAnalysis(
    Rect? MiningHoldEntryBounds,
    Rect? ItemHangarEntryBounds,
    bool MiningHoldFocused,
    bool ItemHangarFocused,
    MiningHoldContentState MiningHoldContent)
{
    public static DockedScreenAnalysis NotFound { get; } = new(
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
