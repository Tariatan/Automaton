using Automaton.Core.Helpers;
using OpenCvSharp;

namespace Automaton.ImageAnalysis;

internal sealed partial class SampleImageProcessor
{
    internal static void ResolvePolygonCollisions(IList<Point[]> polygons)
    {
        for (var pass = 0; pass < PolygonParams.MaximumCollisionResolutionPasses; pass++)
        {
            var changed = false;

            for (var firstIndex = 0; firstIndex < polygons.Count; firstIndex++)
            {
                for (var secondIndex = firstIndex + 1; secondIndex < polygons.Count; secondIndex++)
                {
                    if (!PolygonsOverlap(polygons[firstIndex], polygons[secondIndex]))
                    {
                        continue;
                    }

                    if (TrySeparateAxisAlignedPolygons(polygons[firstIndex], polygons[secondIndex], out var firstSeparated, out var secondSeparated))
                    {
                        polygons[firstIndex] = firstSeparated;
                        polygons[secondIndex] = secondSeparated;
                        changed = true;
                        continue;
                    }

                    var firstArea = Math.Abs(Cv2.ContourArea(polygons[firstIndex]));
                    var secondArea = Math.Abs(Cv2.ContourArea(polygons[secondIndex]));
                    var largerIndex = firstArea >= secondArea ? firstIndex : secondIndex;
                    var smallerIndex = largerIndex == firstIndex ? secondIndex : firstIndex;
                    var clipped = ClipPolygonAwayFromOther(polygons[largerIndex], polygons[smallerIndex]);
                    if (clipped.Length < 3)
                    {
                        continue;
                    }

                    polygons[largerIndex] = clipped.Length > PolygonParams.MaximumPoints
                        ? GeometryHelper.SimplifyContour(clipped, PolygonParams.MaximumPoints)
                        : clipped;
                    changed = true;
                }
            }

            if (!changed)
            {
                break;
            }
        }
    }

    internal static void EnsureMinimumPointSpacing(IList<Point[]> polygons)
    {
        for (var pass = 0; pass < PolygonParams.MaximumPointSpacingResolutionPasses; pass++)
        {
            var changed = false;

            for (var firstIndex = 0; firstIndex < polygons.Count; firstIndex++)
            {
                for (var secondIndex = firstIndex + 1; secondIndex < polygons.Count; secondIndex++)
                {
                    if (!TryFindClosestPolygonSpacingViolation(
                            polygons[firstIndex],
                            polygons[secondIndex],
                            out var violation))
                    {
                        continue;
                    }

                    if (violation.Distance >= PolygonParams.MinimumInterPolygonPointSpacing)
                    {
                        continue;
                    }

                    var firstPoints = polygons[firstIndex].ToArray();
                    var secondPoints = polygons[secondIndex].ToArray();
                    var firstPoint = firstPoints[violation.FirstPointIndex];
                    var secondPoint = secondPoints[violation.SecondPointIndex];
                    var dx = violation.FirstReferencePoint.X - violation.SecondReferencePoint.X;
                    var dy = violation.FirstReferencePoint.Y - violation.SecondReferencePoint.Y;

                    if (dx == 0 && dy == 0)
                    {
                        var firstCentroid = GeometryHelper.GetCentroid(firstPoints);
                        var secondCentroid = GeometryHelper.GetCentroid(secondPoints);
                        dx = firstCentroid.X >= secondCentroid.X ? 1 : -1;
                        dy = firstCentroid.Y >= secondCentroid.Y ? 1 : -1;
                    }

                    var length = Math.Sqrt((dx * dx) + (dy * dy));
                    var missingDistance = PolygonParams.MinimumInterPolygonPointSpacing - violation.Distance;
                    var offsetScale = (missingDistance / 2.0) / length;
                    var offsetX = (int)Math.Ceiling(Math.Abs(dx * offsetScale)) * Math.Sign(dx);
                    var offsetY = (int)Math.Ceiling(Math.Abs(dy * offsetScale)) * Math.Sign(dy);

                    if (offsetX == 0 && offsetY == 0)
                    {
                        offsetX = Math.Sign(dx);
                        offsetY = Math.Sign(dy);
                    }

                    firstPoints[violation.FirstPointIndex] = new Point(firstPoint.X + offsetX, firstPoint.Y + offsetY);
                    secondPoints[violation.SecondPointIndex] = new Point(secondPoint.X - offsetX, secondPoint.Y - offsetY);
                    polygons[firstIndex] = firstPoints;
                    polygons[secondIndex] = secondPoints;
                    changed = true;
                }
            }

            if (!changed)
            {
                break;
            }
        }
    }

    private static bool TryFindClosestPolygonSpacingViolation(
        Point[] firstPolygon,
        Point[] secondPolygon,
        out PolygonSpacingViolation violation)
    {
        violation = default;
        var foundViolation = false;
        var minimumDistance = double.MaxValue;

        for (var firstIndex = 0; firstIndex < firstPolygon.Length; firstIndex++)
        {
            for (var secondIndex = 0; secondIndex < secondPolygon.Length; secondIndex++)
            {
                var dx = firstPolygon[firstIndex].X - secondPolygon[secondIndex].X;
                var dy = firstPolygon[firstIndex].Y - secondPolygon[secondIndex].Y;
                var currentDistance = Math.Sqrt((dx * dx) + (dy * dy));
                if (currentDistance < minimumDistance)
                {
                    minimumDistance = currentDistance;
                    violation = new PolygonSpacingViolation(
                        firstIndex,
                        secondIndex,
                        new Point2d(firstPolygon[firstIndex].X, firstPolygon[firstIndex].Y),
                        new Point2d(secondPolygon[secondIndex].X, secondPolygon[secondIndex].Y),
                        currentDistance);
                    foundViolation = true;
                }
            }
        }

        for (var firstIndex = 0; firstIndex < firstPolygon.Length; firstIndex++)
        {
            for (var secondIndex = 0; secondIndex < secondPolygon.Length; secondIndex++)
            {
                var segmentStart = secondPolygon[secondIndex];
                var segmentEnd = secondPolygon[(secondIndex + 1) % secondPolygon.Length];
                var closestPoint = GeometryHelper.FindClosestPointOnSegment(firstPolygon[firstIndex], segmentStart, segmentEnd);
                var currentDistance = GeometryHelper.Distance(firstPolygon[firstIndex], closestPoint);
                if (currentDistance < minimumDistance)
                {
                    minimumDistance = currentDistance;
                    violation = new PolygonSpacingViolation(
                        firstIndex,
                        secondIndex,
                        new Point2d(firstPolygon[firstIndex].X, firstPolygon[firstIndex].Y),
                        closestPoint,
                        currentDistance);
                    foundViolation = true;
                }
            }
        }

        for (var secondIndex = 0; secondIndex < secondPolygon.Length; secondIndex++)
        {
            for (var firstIndex = 0; firstIndex < firstPolygon.Length; firstIndex++)
            {
                var segmentStart = firstPolygon[firstIndex];
                var segmentEnd = firstPolygon[(firstIndex + 1) % firstPolygon.Length];
                var closestPoint = GeometryHelper.FindClosestPointOnSegment(secondPolygon[secondIndex], segmentStart, segmentEnd);
                var currentDistance = GeometryHelper.Distance(secondPolygon[secondIndex], closestPoint);
                if (currentDistance < minimumDistance)
                {
                    minimumDistance = currentDistance;
                    violation = new PolygonSpacingViolation(
                        firstIndex,
                        secondIndex,
                        closestPoint,
                        new Point2d(secondPolygon[secondIndex].X, secondPolygon[secondIndex].Y),
                        currentDistance);
                    foundViolation = true;
                }
            }
        }

        return foundViolation;
    }

    private static bool PolygonsOverlap(Point[] firstPolygon, Point[] secondPolygon)
    {
        var firstBounds = Cv2.BoundingRect(firstPolygon);
        var secondBounds = Cv2.BoundingRect(secondPolygon);
        var overlapBounds = new Rect(
            Math.Max(firstBounds.X, secondBounds.X),
            Math.Max(firstBounds.Y, secondBounds.Y),
            Math.Min(firstBounds.Right, secondBounds.Right) - Math.Max(firstBounds.X, secondBounds.X),
            Math.Min(firstBounds.Bottom, secondBounds.Bottom) - Math.Max(firstBounds.Y, secondBounds.Y));

        if (overlapBounds.Width <= 0 || overlapBounds.Height <= 0)
        {
            return false;
        }

        using var firstMask = new Mat(overlapBounds.Height, overlapBounds.Width, MatType.CV_8UC1, Scalar.All(0));
        using var secondMask = new Mat(overlapBounds.Height, overlapBounds.Width, MatType.CV_8UC1, Scalar.All(0));
        using var overlapMask = new Mat();

        var translatedFirstPolygon = firstPolygon
            .Select(point => new Point(point.X - overlapBounds.X, point.Y - overlapBounds.Y))
            .ToArray();
        var translatedSecondPolygon = secondPolygon
            .Select(point => new Point(point.X - overlapBounds.X, point.Y - overlapBounds.Y))
            .ToArray();

        Cv2.FillPoly(firstMask, [translatedFirstPolygon], Scalar.All(MaskParams.BinaryMaskMaxValue));
        Cv2.FillPoly(secondMask, [translatedSecondPolygon], Scalar.All(MaskParams.BinaryMaskMaxValue));
        Cv2.BitwiseAnd(firstMask, secondMask, overlapMask);

        return Cv2.CountNonZero(overlapMask) > PolygonParams.MinimumOverlapArea;
    }

    private readonly record struct PolygonSpacingViolation(
        int FirstPointIndex,
        int SecondPointIndex,
        Point2d FirstReferencePoint,
        Point2d SecondReferencePoint,
        double Distance);
}
