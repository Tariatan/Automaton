using Automaton.Helpers;
using OpenCvSharp;

namespace Automaton.Detectors;

internal static class UndockButtonDetector
{
    private const int MinimumUndockButtonWidth = 300;
    private const int MinimumUndockButtonHeight = 50;
    private const double MinimumUndockButtonBlueRatio = 0.12;
    private static readonly Scalar BlueUiMinimum = new(85, 35, 25);
    private static readonly Scalar BlueUiMaximum = new(110, 220, 140);

    public static bool Detect(Mat screen, out Rect undockButtonBounds)
    {
        undockButtonBounds = default;
        if (screen.Empty())
        {
            return false;
        }

        var searchBounds = GeometryHelper.BuildRelativeBounds(screen.Size(), 0.75, 0.13, 0.24, 0.18);
        using var searchRegion = new Mat(screen, searchBounds);
        using var blueMask = BuildBlueUiMask(searchRegion);
        Cv2.FindContours(
            blueMask,
            out var contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        Rect? bestBounds = null;
        var bestArea = 0;
        foreach (var contour in contours)
        {
            var bounds = Cv2.BoundingRect(contour);
            if (bounds.Width < MinimumUndockButtonWidth ||
                bounds.Height < MinimumUndockButtonHeight)
            {
                continue;
            }

            using var candidateMask = new Mat(blueMask, bounds);
            var bluePixels = Cv2.CountNonZero(candidateMask);
            var area = bounds.Width * bounds.Height;
            if (bluePixels < area * MinimumUndockButtonBlueRatio || area <= bestArea)
            {
                continue;
            }

            bestArea = area;
            bestBounds = new Rect(
                searchBounds.X + bounds.X,
                searchBounds.Y + bounds.Y,
                bounds.Width,
                bounds.Height);
        }

        if (bestBounds is null)
        {
            return false;
        }

        undockButtonBounds = bestBounds.Value;
        return true;
    }

    private static Mat BuildBlueUiMask(Mat image)
    {
        using var hsv = new Mat();
        var mask = new Mat();
        Cv2.CvtColor(image, hsv, ColorConversionCodes.BGR2HSV);
        Cv2.InRange(hsv, BlueUiMinimum, BlueUiMaximum, mask);
        return mask;
    }
}
