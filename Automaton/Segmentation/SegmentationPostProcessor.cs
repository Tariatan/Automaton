using OpenCvSharp;

namespace Automaton.Segmentation;

internal static class SegmentationPostProcessor
{
    private const int MinimumContourArea = 800;
    private const int MaximumPolygonPoints = 10;
    private const int MaximumPolygons = 8;
    private const double MinimumSimplificationEpsilon = 3.0;
    private const double SimplificationEpsilonScale = 0.01;
    private const double SimplificationGrowthFactor = 1.35;
    private const int MaxSimplificationAttempts = 12;
    private const double BinaryThreshold = 127.0;
    private const int MorphCloseKernelSize = 5;
    private const int MorphOpenKernelSize = 3;

    public static IReadOnlyList<Point[]> ExtractPolygons(Mat mask)
    {
        if (mask.Empty())
        {
            return [];
        }

        using var binaryMask = EnsureBinaryMask(mask);
        using var cleaned = CleanMask(binaryMask);

        Cv2.FindContours(
            cleaned,
            out var contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        return contours
            .Where(contour => Cv2.ContourArea(contour) >= MinimumContourArea)
            .OrderByDescending(contour => Cv2.ContourArea(contour))
            .Take(MaximumPolygons)
            .Select(SimplifyContour)
            .Where(polygon => polygon.Length >= 3)
            .ToArray();
    }

    private static Mat EnsureBinaryMask(Mat mask)
    {
        var binary = new Mat();
        if (mask.Channels() > 1)
        {
            using var gray = new Mat();
            Cv2.CvtColor(mask, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.Threshold(gray, binary, BinaryThreshold, 255, ThresholdTypes.Binary);
        }
        else
        {
            Cv2.Threshold(mask, binary, BinaryThreshold, 255, ThresholdTypes.Binary);
        }

        return binary;
    }

    private static Mat CleanMask(Mat binaryMask)
    {
        var cleaned = new Mat();
        using var closeKernel = Cv2.GetStructuringElement(
            MorphShapes.Ellipse, new Size(MorphCloseKernelSize, MorphCloseKernelSize));
        using var openKernel = Cv2.GetStructuringElement(
            MorphShapes.Ellipse, new Size(MorphOpenKernelSize, MorphOpenKernelSize));

        Cv2.MorphologyEx(binaryMask, cleaned, MorphTypes.Close, closeKernel);
        Cv2.MorphologyEx(cleaned, cleaned, MorphTypes.Open, openKernel);
        return cleaned;
    }

    private static Point[] SimplifyContour(Point[] contour)
    {
        var perimeter = Cv2.ArcLength(contour, true);
        var epsilon = Math.Max(MinimumSimplificationEpsilon, perimeter * SimplificationEpsilonScale);

        for (var attempt = 0; attempt < MaxSimplificationAttempts; attempt++)
        {
            var simplified = Cv2.ApproxPolyDP(contour, epsilon, true);
            if (simplified.Length is <= MaximumPolygonPoints and >= 3)
            {
                return simplified;
            }

            epsilon *= SimplificationGrowthFactor;
        }

        return contour.Take(MaximumPolygonPoints).ToArray();
    }
}
