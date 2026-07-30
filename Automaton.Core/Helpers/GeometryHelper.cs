using OpenCvSharp;

namespace Automaton.Core.Helpers;

internal static class GeometryHelper
{
    public static Point Center(Rect bounds) => new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);

    public static double CenterX(Rect bounds) => bounds.X + bounds.Width / 2.0;

    public static double CenterY(Rect bounds) => bounds.Y + bounds.Height / 2.0;

    public static bool IsUnscaled(double scale) => Math.Abs(scale - 1.0) < double.Epsilon;

    public static Rect BuildRelativeBounds(
        Size imageSize,
        double leftRatio,
        double topRatio,
        double widthRatio,
        double heightRatio)
    {
        return BuildRelativeBounds(
            new Rect(0, 0, imageSize.Width, imageSize.Height),
            leftRatio, topRatio, widthRatio, heightRatio);
    }

    public static Rect BuildClampedBounds(int x, int y, int width, int height, Size containingSize)
    {
        var clampedX = Math.Clamp(x, 0, Math.Max(0, containingSize.Width - 1));
        var clampedY = Math.Clamp(y, 0, Math.Max(0, containingSize.Height - 1));
        var clampedWidth = Math.Clamp(width, 1, containingSize.Width - clampedX);
        var clampedHeight = Math.Clamp(height, 1, containingSize.Height - clampedY);
        return new Rect(clampedX, clampedY, clampedWidth, clampedHeight);
    }

    public static Rect BuildRelativeBounds(
        Rect bounds,
        double leftRatio,
        double topRatio,
        double widthRatio,
        double heightRatio)
    {
        var x = bounds.X + (int)Math.Round(bounds.Width * leftRatio);
        var y = bounds.Y + (int)Math.Round(bounds.Height * topRatio);
        var width = Math.Max(1, (int)Math.Round(bounds.Width * widthRatio));
        var height = Math.Max(1, (int)Math.Round(bounds.Height * heightRatio));

        var maxX = bounds.X + bounds.Width;
        var maxY = bounds.Y + bounds.Height;

        x = Math.Clamp(x, 0, Math.Max(0, maxX - 1));
        y = Math.Clamp(y, 0, Math.Max(0, maxY - 1));
        width = Math.Clamp(width, 1, maxX - x);
        height = Math.Clamp(height, 1, maxY - y);
        return new Rect(x, y, width, height);
    }

    public static Point[] SimplifyContour(
        Point[] contour,
        int maxPoints = 10,
        double minimumEpsilon = 3.0,
        double epsilonScale = 0.01,
        double growthFactor = 1.35,
        int maxAttempts = 12)
    {
        var perimeter = Cv2.ArcLength(contour, true);
        var epsilon = Math.Max(minimumEpsilon, perimeter * epsilonScale);
        Point[]? bestApproximation = null;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var simplified = Cv2.ApproxPolyDP(contour, epsilon, true);
            if (simplified.Length >= 3)
            {
                bestApproximation = simplified;
            }

            if (simplified.Length <= maxPoints)
            {
                return simplified.Length >= 3 ? simplified : (bestApproximation ?? contour.Take(maxPoints).ToArray());
            }

            epsilon *= growthFactor;
        }

        return bestApproximation ?? contour.Take(maxPoints).ToArray();
    }

    public static double Distance(Point2d a, Point2d b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    public static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    public static double Distance(Point a, Point2d b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    public static Point2d GetCentroid(Point[] polygon) =>
        new(polygon.Average(p => p.X), polygon.Average(p => p.Y));

    public static Point GetContourCentroid(Point[] contour)
    {
        var moments = Cv2.Moments(contour);
        if (Math.Abs(moments.M00) <= double.Epsilon)
        {
            var bounds = Cv2.BoundingRect(contour);
            return new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        }

        return new Point(
            (int)Math.Round(moments.M10 / moments.M00),
            (int)Math.Round(moments.M01 / moments.M00));
    }

    public static int GetAxisGap(int firstStart, int firstEnd, int secondStart, int secondEnd)
    {
        if (firstEnd < secondStart)
        {
            return secondStart - firstEnd;
        }

        if (secondEnd < firstStart)
        {
            return firstStart - secondEnd;
        }

        return 0;
    }

    public static double GetAxisOverlapRatio(int firstStart, int firstEnd, int secondStart, int secondEnd)
    {
        var overlap = Math.Min(firstEnd, secondEnd) - Math.Max(firstStart, secondStart);
        if (overlap <= 0)
        {
            return 0.0;
        }

        var shorterLength = Math.Min(firstEnd - firstStart, secondEnd - secondStart);
        return shorterLength <= 0
            ? 0.0
            : overlap / (double)shorterLength;
    }

    public static Point2d FindClosestPointOnSegment(Point point, Point segmentStart, Point segmentEnd)
    {
        var dx = segmentEnd.X - segmentStart.X;
        var dy = segmentEnd.Y - segmentStart.Y;
        if (dx == 0 && dy == 0)
        {
            return new Point2d(segmentStart.X, segmentStart.Y);
        }

        var tNumerator = ((point.X - segmentStart.X) * dx) + ((point.Y - segmentStart.Y) * dy);
        var tDenominator = (dx * dx) + (dy * dy);
        var t = Math.Clamp(tNumerator / (double)tDenominator, 0.0, 1.0);
        return new Point2d(
            segmentStart.X + (dx * t),
            segmentStart.Y + (dy * t));
    }
}
