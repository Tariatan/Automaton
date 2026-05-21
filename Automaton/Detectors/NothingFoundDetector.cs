using OpenCvSharp;

namespace Automaton.Detectors;

internal static class NothingFoundDetector
{
    private const int MinimumBrightPixelCount = 180;
    private const int MinimumGlyphArea = 80;
    private const int MinimumLineWidth = 48;
    private const int MinimumLineHeight = 10;
    private const int MinimumLineCount = 2;
    private const int WideLineWidth = 80;

    public static bool Detect(Mat screen, Rect mineOverviewBounds)
    {
        if (screen.Empty())
        {
            return false;
        }

        var nothingFoundBounds = BuildNothingFoundSearchBounds(screen.Size(), mineOverviewBounds);
        using var region = new Mat(screen, nothingFoundBounds);
        using var gray = new Mat();
        using var mask = new Mat();
        Cv2.CvtColor(region, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.InRange(gray, new Scalar(110), new Scalar(255), mask);
        if (Cv2.CountNonZero(mask) < MinimumBrightPixelCount)
        {
            return false;
        }

        using var mergedMask = new Mat();
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(7, 3));
        Cv2.MorphologyEx(mask, mergedMask, MorphTypes.Close, kernel);
        Cv2.FindContours(
            mergedMask,
            out var contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        var lineBounds = contours
            .Select(Cv2.BoundingRect)
            .Where(bounds =>
                bounds is { Width: >= MinimumLineWidth, Height: >= MinimumLineHeight } &&
                bounds.Width * bounds.Height >= MinimumGlyphArea)
            .ToArray();

        return lineBounds.Length >= MinimumLineCount || lineBounds.Any(bounds => bounds.Width >= WideLineWidth);
    }

    private static Rect BuildNothingFoundSearchBounds(Size imageSize, Rect mineOverviewBounds)
    {
        var left = Math.Clamp(mineOverviewBounds.X + 40, 0, Math.Max(0, imageSize.Width - 1));
        var top = Math.Clamp(mineOverviewBounds.Y + 185, 0, Math.Max(0, imageSize.Height - 1));
        var right = Math.Clamp(mineOverviewBounds.Right - 28, left + 1, imageSize.Width);
        var bottom = Math.Clamp(mineOverviewBounds.Bottom - 30, top + 1, imageSize.Height);
        return new Rect(left, top, right - left, bottom - top);
    }
}
