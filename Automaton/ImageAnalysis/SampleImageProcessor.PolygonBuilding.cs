using Automaton.Core.Helpers;
using OpenCvSharp;

namespace Automaton.ImageAnalysis;

internal sealed partial class SampleImageProcessor
{
    private static Point[] BuildPolygonFromContour(Point[] contour, Size bounds)
    {
        var contourBounds = Cv2.BoundingRect(contour);
        using var mask = new Mat(contourBounds.Height, contourBounds.Width, MatType.CV_8UC1, Scalar.All(0));
        var contourInBounds = contour
            .Select(point => new Point(point.X - contourBounds.X, point.Y - contourBounds.Y))
            .ToArray();
        Cv2.FillPoly(mask, [contourInBounds], Scalar.All(MaskParams.BinaryMaskMaxValue));
        return BuildPolygonFromMask(mask, contourBounds.Location, bounds);
    }

    private static Point[] BuildPolygonFromPoints(Point[] points, Point offset, Size bounds)
    {
        var translatedPoints = points
            .Select(point => new Point(point.X + offset.X, point.Y + offset.Y))
            .ToArray();

        var pointBounds = Cv2.BoundingRect(translatedPoints);
        pointBounds = ExpandRect(pointBounds, PolygonParams.PointCloudMargin, bounds);

        using var pointMask = new Mat(pointBounds.Height, pointBounds.Width, MatType.CV_8UC1, Scalar.All(0));

        foreach (var point in translatedPoints)
        {
            Cv2.Circle(
                pointMask,
                new Point(point.X - pointBounds.X, point.Y - pointBounds.Y),
                PolygonParams.PointCloudSeedRadius,
                Scalar.All(MaskParams.BinaryMaskMaxValue),
                -1,
                LineTypes.AntiAlias);
        }

        return BuildPolygonFromMask(pointMask, pointBounds.Location, bounds);
    }

    private static Point[] BuildPolygonFromMask(Mat mask, Point offset, Size bounds)
    {
        if (Cv2.CountNonZero(mask) == 0)
        {
            return [];
        }

        using var paddedMask = new Mat();
        using var closeKernel = Cv2.GetStructuringElement(
            MorphShapes.Ellipse,
            new Size(PolygonParams.MaskCloseKernelSize, PolygonParams.MaskCloseKernelSize));
        Cv2.MorphologyEx(mask, paddedMask, MorphTypes.Close, closeKernel);

        var padding = CalculateMaskPadding(Cv2.CountNonZero(mask));
        var kernelSize = (padding * 2) + 1;
        using var dilateKernel = Cv2.GetStructuringElement(
            MorphShapes.Ellipse,
            new Size(kernelSize, kernelSize));
        Cv2.Dilate(paddedMask, paddedMask, dilateKernel);

        Cv2.FindContours(
            paddedMask,
            out var contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        var expandedContour = contours.MaxBy(points => Math.Abs(Cv2.ContourArea(points)));
        if (expandedContour is null || expandedContour.Length < 3)
        {
            return [];
        }

        var balloonContour = BalloonizePolygon(expandedContour, bounds);
        var simplified = GeometryHelper.SimplifyContour(balloonContour, PolygonParams.MaximumPoints);
        var translatedPolygon = simplified
            .Select(point => new Point(
                Math.Clamp(point.X + offset.X, 0, bounds.Width - 1),
                Math.Clamp(point.Y + offset.Y, 0, bounds.Height - 1)))
            .ToArray();
        return EnforceMinimumPolygonFootprint(translatedPolygon, bounds);
    }

    private static Point[] TranslatePolygon(Point[] polygon, Rect playfieldBounds)
    {
        return [.. polygon.Select(point => new Point(point.X + playfieldBounds.X, point.Y + playfieldBounds.Y))];
    }

    internal static Point[] BalloonizePolygon(Point[] polygon, Size bounds)
    {
        if (polygon.Length < 3)
        {
            return polygon;
        }

        var hull = Cv2.ConvexHull(polygon);
        var expandedHull = ExpandBalloonHull(hull);
        var simplifiedHull = expandedHull.Length <= PolygonParams.MaximumPoints
            ? expandedHull
            : GeometryHelper.SimplifyContour(expandedHull, PolygonParams.MaximumPoints);
        return ClampPolygonToBounds(simplifiedHull, bounds);
    }

    private static Point[] ClampPolygonToBounds(Point[] polygon, Size bounds)
    {
        return
        [
            .. polygon
                .Select(point => new Point(
                    Math.Clamp(point.X, 0, bounds.Width - 1),
                    Math.Clamp(point.Y, 0, bounds.Height - 1)))
        ];
    }

    private static Point[] ExpandBalloonHull(Point[] hull)
    {
        var centroid = GeometryHelper.GetCentroid(hull);
        var expansion = Math.Clamp(
            (int)Math.Round(Math.Sqrt(Math.Abs(Cv2.ContourArea(hull))) * PolygonParams.BalloonExpansionScale),
            PolygonParams.MinimumBalloonExpansion,
            PolygonParams.MaximumBalloonExpansion);

        return
        [
            .. hull
                .Select(point =>
                {
                    var dx = point.X - centroid.X;
                    var dy = point.Y - centroid.Y;
                    var length = Math.Sqrt((dx * dx) + (dy * dy));
                    if (length < double.Epsilon)
                    {
                        return point;
                    }

                    var scale = (length + expansion) / length;
                    return new Point(
                        (int)Math.Round(centroid.X + (dx * scale)),
                        (int)Math.Round(centroid.Y + (dy * scale)));
                })
        ];
    }

    private static int CalculateMaskPadding(int area)
    {
        var scaledPadding = (int)Math.Round(Math.Sqrt(area) * PolygonParams.MaskPaddingScale);
        return Math.Clamp(scaledPadding, PolygonParams.MinimumMaskPadding, PolygonParams.MaximumMaskPadding);
    }

    private static Rect ExpandRect(Rect rect, int margin, Size bounds)
    {
        var left = Math.Max(0, rect.X - margin);
        var top = Math.Max(0, rect.Y - margin);
        var right = Math.Min(bounds.Width, rect.Right + margin);
        var bottom = Math.Min(bounds.Height, rect.Bottom + margin);
        return new Rect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    internal static Point[] EnforceMinimumPolygonFootprint(Point[] polygon, Size bounds)
    {
        if (polygon.Length < 3)
        {
            return polygon;
        }

        var polygonBounds = Cv2.BoundingRect(polygon);
        var currentBoundingArea = polygonBounds.Width * polygonBounds.Height;
        if (currentBoundingArea >= PolygonParams.MinimumBoundingArea)
        {
            return polygon;
        }

        var scale = Math.Sqrt(PolygonParams.MinimumBoundingArea / (double)Math.Max(1, currentBoundingArea));

        var centroid = GeometryHelper.GetCentroid(polygon);
        var expandedPolygon = polygon
            .Select(point =>
            {
                var scaledX = centroid.X + ((point.X - centroid.X) * scale);
                var scaledY = centroid.Y + ((point.Y - centroid.Y) * scale);
                return new Point(
                    Math.Clamp((int)Math.Round(scaledX), 0, bounds.Width - 1),
                    Math.Clamp((int)Math.Round(scaledY), 0, bounds.Height - 1));
            })
            .ToArray();
        return ForceMinimumBoundingArea(expandedPolygon, bounds);
    }

    private static Point[] ForceMinimumBoundingArea(Point[] polygon, Size bounds)
    {
        var adjustedPolygon = polygon.ToArray();
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var adjustedBounds = Cv2.BoundingRect(adjustedPolygon);
            var adjustedBoundingArea = adjustedBounds.Width * adjustedBounds.Height;
            if (adjustedBoundingArea >= PolygonParams.MinimumBoundingArea)
            {
                return adjustedPolygon;
            }

            var areaScale = Math.Sqrt(PolygonParams.MinimumBoundingArea / (double)Math.Max(1, adjustedBoundingArea));
            areaScale = Math.Max(areaScale, 1.05);

            var centerX = adjustedBounds.X + ((adjustedBounds.Width - 1) / 2.0);
            var centerY = adjustedBounds.Y + ((adjustedBounds.Height - 1) / 2.0);
            for (var index = 0; index < adjustedPolygon.Length; index++)
            {
                adjustedPolygon[index].X = Math.Clamp(
                    (int)Math.Round(centerX + ((adjustedPolygon[index].X - centerX) * areaScale)),
                    0,
                    bounds.Width - 1);
                adjustedPolygon[index].Y = Math.Clamp(
                    (int)Math.Round(centerY + ((adjustedPolygon[index].Y - centerY) * areaScale)),
                    0,
                    bounds.Height - 1);
            }
        }

        var finalBounds = Cv2.BoundingRect(adjustedPolygon);
        if ((finalBounds.Width * finalBounds.Height) >= PolygonParams.MinimumBoundingArea)
        {
            return adjustedPolygon;
        }

        var width = Math.Max(1, finalBounds.Width);
        var height = Math.Max(1, finalBounds.Height);
        var finalScale = Math.Sqrt(PolygonParams.MinimumBoundingArea / (double)(width * height));
        var fallbackCenterX = finalBounds.X + ((finalBounds.Width - 1) / 2.0);
        var fallbackCenterY = finalBounds.Y + ((finalBounds.Height - 1) / 2.0);
        return
        [
            .. adjustedPolygon
                .Select(point => new Point(
                    Math.Clamp((int)Math.Round(fallbackCenterX + ((point.X - fallbackCenterX) * finalScale)), 0,
                        bounds.Width - 1),
                    Math.Clamp((int)Math.Round(fallbackCenterY + ((point.Y - fallbackCenterY) * finalScale)), 0,
                        bounds.Height - 1)))
        ];
    }
}
