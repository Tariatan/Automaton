using Automaton.Core.Helpers;
using OpenCvSharp;

namespace Automaton.ImageAnalysis;

internal sealed partial class SampleImageProcessor
{
    internal static void FinalizeDetectedPolygons(IList<Point[]> polygons, IReadOnlyList<Rect> markerBounds)
    {
        // Three passes ensure that collision resolution and spacing enforcement don't
        // reintroduce violations fixed in the previous pass.
        foreach (var _ in new int[1, 2])
        {
            NormalizePolygons(polygons, mergeCloseNeighboringPoints: false);
            OverwritePolygons(polygons, ApplyMarkerBoundaryConstraints([.. polygons], markerBounds));
            ResolvePolygonCollisions(polygons);
            EnsureMinimumPointSpacing(polygons);
        }

        NormalizePolygons(polygons);
        OverwritePolygons(polygons, ApplyMarkerBoundaryConstraints([.. polygons], markerBounds));
    }

    internal static IReadOnlyList<Point[]> ApplyMarkerBoundaryConstraints(IReadOnlyList<Point[]> polygons, IReadOnlyList<Rect> markerBounds)
    {
        if (polygons.Count == 0 || markerBounds.Count < 4)
        {
            return polygons;
        }

        var topMarkers = markerBounds
            .OrderBy(marker => marker.Y + (marker.Height / 2.0))
            .Take(2)
            .ToArray();
        var leftBoundary = markerBounds.Min(marker => marker.X);
        var topBoundary = markerBounds.Min(marker => marker.Y);
        var rightBoundary = markerBounds.Max(marker => marker.Right);
        var bottomBoundary = markerBounds.Max(marker => marker.Bottom);
        var topMarkerCeiling = topMarkers.Min(marker => marker.Y);
        var averageTopMarkerHeight = topMarkers.Average(marker => marker.Height);
        var topBandCentroidThreshold = topMarkerCeiling + (averageTopMarkerHeight * PolygonParams.TopMarkerBandCentroidScale);
        var adjustedPolygons = new List<Point[]>(polygons.Count);

        foreach (var polygon in polygons)
        {
            var clippedPolygon = polygon;
            clippedPolygon = ClipPolygonToMinimumX(clippedPolygon, leftBoundary);
            if (clippedPolygon.Length < 3)
            {
                adjustedPolygons.Add(polygon);
                continue;
            }

            clippedPolygon = ClipPolygonToMaximumX(clippedPolygon, rightBoundary);
            if (clippedPolygon.Length < 3)
            {
                adjustedPolygons.Add(polygon);
                continue;
            }

            var centroid = GeometryHelper.GetCentroid(clippedPolygon);
            var clippedBounds = Cv2.BoundingRect(clippedPolygon);
            if (clippedBounds.Top < topMarkerCeiling && centroid.Y <= topBandCentroidThreshold)
            {
                clippedPolygon = ClipPolygonToMinimumY(clippedPolygon, topMarkerCeiling);
                if (clippedPolygon.Length < 3)
                {
                    adjustedPolygons.Add(polygon);
                    continue;
                }
            }

            clippedPolygon = ClipPolygonToMinimumY(clippedPolygon, topBoundary);
            if (clippedPolygon.Length < 3)
            {
                adjustedPolygons.Add(polygon);
                continue;
            }

            clippedPolygon = ClipPolygonToMaximumY(clippedPolygon, bottomBoundary);
            adjustedPolygons.Add(clippedPolygon.Length >= 3 ? clippedPolygon : polygon);
        }

        return adjustedPolygons;
    }

    internal static void RandomizePolygons(IList<Point[]> polygons, Random? random = null)
    {
        random ??= Random.Shared;

        for (var polygonIndex = 0; polygonIndex < polygons.Count; polygonIndex++)
        {
            var polygon = polygons[polygonIndex];
            if (polygon.Length < 3)
            {
                continue;
            }

            var randomizedPointCount = Math.Max(
                1,
                (int)Math.Round(polygon.Length * PolygonParams.RandomizedPointRatio, MidpointRounding.AwayFromZero));
            randomizedPointCount = Math.Min(randomizedPointCount, polygon.Length);

            var randomizedIndices = Enumerable.Range(0, polygon.Length)
                .OrderBy(_ => random.Next())
                .Take(randomizedPointCount)
                .ToArray();
            var randomizedPolygon = polygon.ToArray();

            foreach (var pointIndex in randomizedIndices)
            {
                var angle = random.NextDouble() * Math.PI * 2.0;
                var distance = random.Next(PolygonParams.MinimumRandomizedPointDistance, PolygonParams.MaximumRandomizedPointDistance + 1);
                var offsetX = (int)Math.Round(Math.Cos(angle) * distance);
                var offsetY = (int)Math.Round(Math.Sin(angle) * distance);

                if (offsetX == 0 && offsetY == 0)
                {
                    offsetX = distance;
                }

                randomizedPolygon[pointIndex] = new Point(
                    randomizedPolygon[pointIndex].X + offsetX,
                    randomizedPolygon[pointIndex].Y + offsetY);
            }

            polygons[polygonIndex] = randomizedPolygon;
        }
    }

    internal static void NormalizePolygons(IList<Point[]> polygons, bool mergeCloseNeighboringPoints = true)
    {
        for (var polygonIndex = 0; polygonIndex < polygons.Count; polygonIndex++)
        {
            polygons[polygonIndex] = NormalizePolygon(polygons[polygonIndex], mergeCloseNeighboringPoints);
        }
    }

    internal static Point[] NormalizePolygon(Point[] polygon, bool mergeCloseNeighboringPoints = true)
    {
        if (polygon.Length < 3)
        {
            return polygon;
        }

        var normalizedPolygon = Cv2.IsContourConvex(polygon)
            ? polygon
            : Cv2.ConvexHull(polygon);
        return mergeCloseNeighboringPoints
            ? MergeCloseNeighboringPoints(normalizedPolygon)
            : normalizedPolygon;
    }

    private static Point[] MergeCloseNeighboringPoints(Point[] polygon)
    {
        if (polygon.Length <= 3)
        {
            return polygon;
        }

        var mergedPoints = new List<Point> { polygon[0] };

        for (var pointIndex = 1; pointIndex < polygon.Length; pointIndex++)
        {
            var candidate = polygon[pointIndex];
            var remainingPointsAfterCandidate = polygon.Length - pointIndex - 1;
            var canSkipCandidate = mergedPoints.Count + remainingPointsAfterCandidate >= 3;
            if (canSkipCandidate && GeometryHelper.Distance(candidate, mergedPoints[^1]) < PolygonParams.MinimumNeighboringPointSpacing)
            {
                continue;
            }

            mergedPoints.Add(candidate);
        }

        while (mergedPoints.Count > 3 &&
               GeometryHelper.Distance(mergedPoints[0], mergedPoints[^1]) < PolygonParams.MinimumNeighboringPointSpacing)
        {
            mergedPoints.RemoveAt(mergedPoints.Count - 1);
        }

        return [.. mergedPoints];
    }

    private static void OverwritePolygons(IList<Point[]> target, IReadOnlyList<Point[]> source)
    {
        target.Clear();

        foreach (var polygon in source)
        {
            target.Add(polygon);
        }
    }
}
