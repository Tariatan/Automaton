using Automaton.Core.Helpers;
using OpenCvSharp;

namespace Automaton.ImageAnalysis;

internal sealed partial class SampleImageProcessor
{
    private static Point[] ClipPolygonToMaximumY(Point[] polygon, int maximumY)
    {
        return ClipPolygonWithBoundary(
            polygon,
            point => point.Y <= maximumY,
            (start, end) => IntersectSegmentWithHorizontalBoundary(start, end, maximumY));
    }

    private static Point[] ClipPolygonToMinimumY(Point[] polygon, int minimumY)
    {
        return ClipPolygonWithBoundary(
            polygon,
            point => point.Y >= minimumY,
            (start, end) => IntersectSegmentWithHorizontalBoundary(start, end, minimumY));
    }

    private static Point[] ClipPolygonToMaximumX(Point[] polygon, int maximumX)
    {
        return ClipPolygonWithBoundary(
            polygon,
            point => point.X <= maximumX,
            (start, end) => IntersectSegmentWithVerticalBoundary(start, end, maximumX));
    }

    private static Point[] ClipPolygonToMinimumX(Point[] polygon, int minimumX)
    {
        return ClipPolygonWithBoundary(
            polygon,
            point => point.X >= minimumX,
            (start, end) => IntersectSegmentWithVerticalBoundary(start, end, minimumX));
    }

    private static Point[] ClipPolygonWithBoundary(
        Point[] polygon,
        Func<Point, bool> isInside,
        Func<Point, Point, Point?> intersect)
    {
        if (polygon.Length < 3)
        {
            return [];
        }

        var clipped = new List<Point>();

        for (var index = 0; index < polygon.Length; index++)
        {
            var current = polygon[index];
            var next = polygon[(index + 1) % polygon.Length];
            var currentInside = isInside(current);
            var nextInside = isInside(next);

            if (currentInside && nextInside)
            {
                clipped.Add(next);
                continue;
            }

            if (currentInside && !nextInside)
            {
                var intersection = intersect(current, next);
                if (intersection is not null)
                {
                    clipped.Add(intersection.Value);
                }

                continue;
            }

            if (!currentInside && nextInside)
            {
                var intersection = intersect(current, next);
                if (intersection is not null)
                {
                    clipped.Add(intersection.Value);
                }

                clipped.Add(next);
            }
        }

        var distinctPoints = clipped
            .Distinct()
            .ToArray();

        return distinctPoints.Length <= PolygonParams.MaximumPoints
            ? distinctPoints
            : GeometryHelper.SimplifyContour(distinctPoints, PolygonParams.MaximumPoints);
    }

    private static Point? IntersectSegmentWithHorizontalBoundary(Point start, Point end, int boundaryY)
    {
        var dy = end.Y - start.Y;
        if (dy == 0)
        {
            return null;
        }

        var t = (boundaryY - start.Y) / (double)dy;
        if (t is < 0.0 or > 1.0)
        {
            return null;
        }

        return new Point(
            (int)Math.Round(start.X + ((end.X - start.X) * t)),
            boundaryY);
    }

    private static Point? IntersectSegmentWithVerticalBoundary(Point start, Point end, int boundaryX)
    {
        var dx = end.X - start.X;
        if (dx == 0)
        {
            return null;
        }

        var t = (boundaryX - start.X) / (double)dx;
        if (t is < 0.0 or > 1.0)
        {
            return null;
        }

        return new Point(
            boundaryX,
            (int)Math.Round(start.Y + ((end.Y - start.Y) * t)));
    }

    private static bool TrySeparateAxisAlignedPolygons(
        Point[] firstPolygon,
        Point[] secondPolygon,
        out Point[] firstSeparated,
        out Point[] secondSeparated)
    {
        firstSeparated = firstPolygon;
        secondSeparated = secondPolygon;

        var firstCentroid = GeometryHelper.GetCentroid(firstPolygon);
        var secondCentroid = GeometryHelper.GetCentroid(secondPolygon);
        var deltaX = Math.Abs(firstCentroid.X - secondCentroid.X);
        var deltaY = Math.Abs(firstCentroid.Y - secondCentroid.Y);

        if (deltaY >= deltaX)
        {
            var boundaryY = (int)Math.Round((firstCentroid.Y + secondCentroid.Y) / 2.0);
            var firstIsTop = firstCentroid.Y <= secondCentroid.Y;
            var topPolygon = firstIsTop ? firstPolygon : secondPolygon;
            var bottomPolygon = firstIsTop ? secondPolygon : firstPolygon;
            var separatedTop = ClipPolygonToMaximumY(topPolygon, boundaryY - PolygonParams.SeparationPixels);
            var separatedBottom = ClipPolygonToMinimumY(bottomPolygon, boundaryY + PolygonParams.SeparationPixels);

            if (separatedTop.Length < 3 || separatedBottom.Length < 3)
            {
                return false;
            }

            if (firstIsTop)
            {
                firstSeparated = separatedTop;
                secondSeparated = separatedBottom;
            }
            else
            {
                firstSeparated = separatedBottom;
                secondSeparated = separatedTop;
            }

            return !ImageAnalysis.SampleImageProcessor.PolygonsOverlap(firstSeparated, secondSeparated);
        }

        var boundaryX = (int)Math.Round((firstCentroid.X + secondCentroid.X) / 2.0);
        var firstIsLeft = firstCentroid.X <= secondCentroid.X;
        var leftPolygon = firstIsLeft ? firstPolygon : secondPolygon;
        var rightPolygon = firstIsLeft ? secondPolygon : firstPolygon;
        var separatedLeft = ClipPolygonToMaximumX(leftPolygon, boundaryX - PolygonParams.SeparationPixels);
        var separatedRight = ClipPolygonToMinimumX(rightPolygon, boundaryX + PolygonParams.SeparationPixels);

        if (separatedLeft.Length < 3 || separatedRight.Length < 3)
        {
            return false;
        }

        if (firstIsLeft)
        {
            firstSeparated = separatedLeft;
            secondSeparated = separatedRight;
        }
        else
        {
            firstSeparated = separatedRight;
            secondSeparated = separatedLeft;
        }

        return !ImageAnalysis.SampleImageProcessor.PolygonsOverlap(firstSeparated, secondSeparated);
    }

    private static Point[] ClipPolygonAwayFromOther(Point[] polygon, Point[] otherPolygon)
    {
        var polygonCentroid = GeometryHelper.GetCentroid(polygon);
        var otherCentroid = GeometryHelper.GetCentroid(otherPolygon);
        var midpoint = new Point2d(
            (polygonCentroid.X + otherCentroid.X) / 2.0,
            (polygonCentroid.Y + otherCentroid.Y) / 2.0);
        var normal = new Point2d(
            polygonCentroid.X - otherCentroid.X,
            polygonCentroid.Y - otherCentroid.Y);

        var clipped = new List<Point>();

        for (var index = 0; index < polygon.Length; index++)
        {
            var current = polygon[index];
            var next = polygon[(index + 1) % polygon.Length];
            var currentInside = IsInsideHalfPlane(current, midpoint, normal);
            var nextInside = IsInsideHalfPlane(next, midpoint, normal);

            if (currentInside && nextInside)
            {
                clipped.Add(next);
                continue;
            }

            if (currentInside && !nextInside)
            {
                var intersection = IntersectSegmentWithHalfPlane(current, next, midpoint, normal);
                if (intersection is not null)
                {
                    clipped.Add(intersection.Value);
                }

                continue;
            }

            if (!currentInside && nextInside)
            {
                var intersection = IntersectSegmentWithHalfPlane(current, next, midpoint, normal);
                if (intersection is not null)
                {
                    clipped.Add(intersection.Value);
                }

                clipped.Add(next);
            }
        }

        return [.. clipped.Distinct()];
    }

    private static bool IsInsideHalfPlane(Point point, Point2d midpoint, Point2d normal)
    {
        var dot = ((point.X - midpoint.X) * normal.X) + ((point.Y - midpoint.Y) * normal.Y);
        return dot >= 0.0;
    }

    private static Point? IntersectSegmentWithHalfPlane(Point start, Point end, Point2d midpoint, Point2d normal)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var denominator = (dx * normal.X) + (dy * normal.Y);
        if (Math.Abs(denominator) < double.Epsilon)
        {
            return null;
        }

        var t = -(((start.X - midpoint.X) * normal.X) + ((start.Y - midpoint.Y) * normal.Y)) / denominator;
        if (t is < 0.0 or > 1.0)
        {
            return null;
        }

        return new Point(
            (int)Math.Round(start.X + (dx * t)),
            (int)Math.Round(start.Y + (dy * t)));
    }
}
