using Automaton.Core.Helpers;
using OpenCvSharp;

namespace Automaton.ImageAnalysis;

internal sealed partial class SampleImageProcessor
{
    private static IReadOnlyList<Point[]> TryBuildCandidateComponentPolygons(Point[] contour, Mat candidateMask, Size bounds)
    {
        var contourBounds = Cv2.BoundingRect(contour);
        if (!ShouldAttemptSideBySideSplit(contourBounds))
        {
            return [];
        }

        var (maskedCandidates, contourMask) = MaskCandidatesWithinContour(contour, contourBounds, candidateMask);
        using var candidates = maskedCandidates;
        using var mask = contourMask;
        using var refinedCandidates = new Mat();

        using var closeKernel = Cv2.GetStructuringElement(
            MorphShapes.Ellipse,
            new Size(MaskParams.CandidateRefineCloseKernelSize, MaskParams.CandidateRefineCloseKernelSize));
        Cv2.MorphologyEx(maskedCandidates, refinedCandidates, MorphTypes.Close, closeKernel);

        Cv2.FindContours(
            refinedCandidates,
            out var componentContours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        var componentPolygons = componentContours
            .Where(componentContour => Cv2.ContourArea(componentContour) >= SplitParams.MinimumRefinedComponentArea)
            .Where(ComponentHasMinimumFootprint)
            .Select(componentContour => componentContour
                .Select(point => new Point(point.X + contourBounds.X, point.Y + contourBounds.Y))
                .ToArray())
            .Select(componentContour => BuildPolygonFromContour(componentContour, bounds))
            .Where(polygon => polygon.Length >= 3)
            .OrderByDescending(points => Math.Abs(Cv2.ContourArea(points)))
            .ToList();

        return componentPolygons.Count >= 2
            ? componentPolygons
            : Array.Empty<Point[]>();
    }

    private static bool ComponentHasMinimumFootprint(Point[] componentContour)
    {
        var componentBounds = Cv2.BoundingRect(componentContour);
        return (componentBounds.Width * componentBounds.Height) >= SplitParams.MinimumRefinedComponentBoundingArea;
    }

    private static IReadOnlyList<Point[]> TrySplitContourIntoVerticalSegments(Point[] contour, Mat candidateMask, Mat candidateDensityMap, Size bounds)
    {
        var contourBounds = Cv2.BoundingRect(contour);
        if (contourBounds.Height < SplitParams.MinimumSegmentHeight * 2 ||
            contourBounds.Height < contourBounds.Width * SplitParams.MinimumAspectRatio)
        {
            return [];
        }

        var (maskedCandidatesRaw, contourMask) = MaskCandidatesWithinContour(contour, contourBounds, candidateMask);
        using var maskedCandidates = maskedCandidatesRaw;
        using var mask = contourMask;
        using var densityRegion = new Mat(candidateDensityMap, contourBounds);
        using var maskedDensity = new Mat();
        using var candidatePointIndex = new Mat();
        densityRegion.CopyTo(maskedDensity, contourMask);
        Cv2.FindNonZero(maskedCandidates, candidatePointIndex);
        Point[]? candidatePoints = null;
        if (!candidatePointIndex.Empty())
        {
            candidatePointIndex.GetArray(out candidatePoints);
        }
        if (candidatePoints is null || candidatePoints.Length < SplitParams.MinimumPointCount)
        {
            return [];
        }

        var splitRow = TryFindVerticalSplitRow(maskedDensity, contourBounds.Height) ??
                       TryFindVerticalSplitRow(candidatePoints, contourBounds.Height);
        if (splitRow is null)
        {
            return [];
        }

        var topPoints = candidatePoints.Where(point => point.Y <= splitRow.Value).ToArray();
        var bottomPoints = candidatePoints.Where(point => point.Y > splitRow.Value).ToArray();
        if (topPoints.Length < SplitParams.MinimumPointCount || bottomPoints.Length < SplitParams.MinimumPointCount)
        {
            return [];
        }

        var topHeight = topPoints.Max(point => point.Y) - topPoints.Min(point => point.Y) + 1;
        var bottomHeight = bottomPoints.Max(point => point.Y) - bottomPoints.Min(point => point.Y) + 1;
        if (topHeight < SplitParams.MinimumSegmentHeight || bottomHeight < SplitParams.MinimumSegmentHeight)
        {
            return [];
        }

        var splitY = contourBounds.Y + splitRow.Value;
        var topPolygon = ClipPolygonToMaximumY(
            BuildPolygonFromPoints(topPoints, contourBounds.Location, bounds),
            splitY - PolygonParams.SeparationPixels);
        var bottomPolygon = ClipPolygonToMinimumY(
            BuildPolygonFromPoints(bottomPoints, contourBounds.Location, bounds),
            splitY + PolygonParams.SeparationPixels);
        if (topPolygon.Length < 3 || bottomPolygon.Length < 3)
        {
            return [];
        }

        return [topPolygon, bottomPolygon];
    }

    private static IReadOnlyList<Point[]> TrySplitContourByDensitySeeds(Point[] contour, Mat candidateMask, Mat candidateDensityMap, Size bounds)
    {
        var contourBounds = Cv2.BoundingRect(contour);
        var (maskedCandidatesRaw, contourMask) = MaskCandidatesWithinContour(contour, contourBounds, candidateMask);
        using var maskedCandidates = maskedCandidatesRaw;
        using var mask = contourMask;
        using var densityRegion = new Mat(candidateDensityMap, contourBounds);
        using var maskedDensity = new Mat();
        using var blurred = new Mat();
        using var thresholded = new Mat();
        using var candidatePointIndex = new Mat();
        densityRegion.CopyTo(maskedDensity, contourMask);
        Cv2.FindNonZero(maskedCandidates, candidatePointIndex);

        Point[]? candidatePoints = null;
        if (!candidatePointIndex.Empty())
        {
            candidatePointIndex.GetArray(out candidatePoints);
        }

        if (candidatePoints is null || candidatePoints.Length < SplitParams.MinimumPointCount * 2)
        {
            return [];
        }

        Cv2.GaussianBlur(maskedDensity, blurred, new Size(0, 0), SplitParams.DensitySeedBlurSigma, SplitParams.DensitySeedBlurSigma);
        Cv2.MinMaxLoc(blurred, out double _, out var maxValue);
        if (maxValue <= double.Epsilon)
        {
            return [];
        }

        var thresholdValue = Math.Max(1.0, maxValue * SplitParams.DensitySeedThresholdRatio);
        Cv2.Threshold(blurred, thresholded, thresholdValue, MaskParams.BinaryMaskMaxValue, ThresholdTypes.Binary);
        Cv2.FindContours(
            thresholded,
            out var seedContours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        var seedCenters = seedContours
            .Where(seedContour => Cv2.ContourArea(seedContour) >= SplitParams.DensitySeedMinimumContourArea)
            .Select(GeometryHelper.GetContourCentroid)
            .Distinct()
            .OrderBy(point => point.X)
            .ThenBy(point => point.Y)
            .ToList();

        if (seedCenters.Count < 2)
        {
            return [];
        }

        seedCenters = ReduceSeedCenters(seedCenters);
        if (seedCenters.Count is < 2 or > SplitParams.MaximumDensitySeedCount)
        {
            return [];
        }

        var groupedPoints = new List<Point>[seedCenters.Count];
        for (var index = 0; index < groupedPoints.Length; index++)
        {
            groupedPoints[index] = [];
        }

        foreach (var point in candidatePoints)
        {
            var closestIndex = 0;
            var closestDistance = double.MaxValue;

            for (var index = 0; index < seedCenters.Count; index++)
            {
                var distance = GeometryHelper.Distance(point, seedCenters[index]);
                if (distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = distance;
                closestIndex = index;
            }

            groupedPoints[closestIndex].Add(point);
        }

        var polygons = groupedPoints
            .Where(points => points.Count >= SplitParams.MinimumPointCount)
            .Select(points => BuildPolygonFromPoints(points.ToArray(), contourBounds.Location, bounds))
            .Where(points => points.Length >= 3)
            .ToList();

        return polygons.Count >= 2
            ? polygons
            : Array.Empty<Point[]>();
    }

    private static IReadOnlyList<Point[]> TrySplitContourByPointClusters(Point[] contour, Mat candidateMask, Size bounds)
    {
        var contourBounds = Cv2.BoundingRect(contour);
        var candidatePoints = TryGetContourCandidatePoints(contour, contourBounds, candidateMask);
        if (candidatePoints.Length < SplitParams.MinimumPointCount * 2)
        {
            return [];
        }

        List<Point[]> bestPolygons = [];
        var bestScore = 0.0;
        var maxClusterCount = Math.Min(SplitParams.MaximumPointClusterCount, candidatePoints.Length / SplitParams.MinimumPointCount);

        for (var clusterCount = 2; clusterCount <= maxClusterCount; clusterCount++)
        {
            var evaluation = TryBuildPointClusterPolygons(candidatePoints, clusterCount, contourBounds.Location, bounds);
            if (!evaluation.HasValue || evaluation.Value.Score <= bestScore)
            {
                continue;
            }

            bestScore = evaluation.Value.Score;
            bestPolygons = evaluation.Value.Polygons;
        }

        return bestPolygons.Count >= 2
            ? bestPolygons
            : Array.Empty<Point[]>();
    }

    private static IReadOnlyList<Point[]> TrySplitContourIntoHorizontalSegments(Point[] contour, Mat candidateMask, Mat candidateDensityMap, Size bounds)
    {
        var contourBounds = Cv2.BoundingRect(contour);
        if (contourBounds.Width < SplitParams.MinimumSegmentWidth * 2 ||
            !ShouldAttemptSideBySideSplit(contourBounds))
        {
            return [];
        }

        var (maskedCandidatesRaw, contourMask) = MaskCandidatesWithinContour(contour, contourBounds, candidateMask);
        using var maskedCandidates = maskedCandidatesRaw;
        using var mask = contourMask;
        using var densityRegion = new Mat(candidateDensityMap, contourBounds);
        using var maskedDensity = new Mat();
        using var candidatePointIndex = new Mat();
        densityRegion.CopyTo(maskedDensity, contourMask);
        Cv2.FindNonZero(maskedCandidates, candidatePointIndex);
        Point[]? candidatePoints = null;
        if (!candidatePointIndex.Empty())
        {
            candidatePointIndex.GetArray(out candidatePoints);
        }

        if (candidatePoints is null || candidatePoints.Length < SplitParams.MinimumPointCount)
        {
            return [];
        }

        var splitColumn = TryFindHorizontalSplitColumn(maskedDensity, contourBounds.Width) ??
                          TryFindHorizontalSplitColumn(candidatePoints, contourBounds.Width);
        if (splitColumn is null)
        {
            return [];
        }

        var leftPoints = candidatePoints.Where(point => point.X <= splitColumn.Value).ToArray();
        var rightPoints = candidatePoints.Where(point => point.X > splitColumn.Value).ToArray();
        if (leftPoints.Length < SplitParams.MinimumPointCount || rightPoints.Length < SplitParams.MinimumPointCount)
        {
            return [];
        }

        var leftWidth = leftPoints.Max(point => point.X) - leftPoints.Min(point => point.X) + 1;
        var rightWidth = rightPoints.Max(point => point.X) - rightPoints.Min(point => point.X) + 1;
        if (leftWidth < SplitParams.MinimumSegmentWidth || rightWidth < SplitParams.MinimumSegmentWidth)
        {
            return [];
        }

        var splitX = contourBounds.X + splitColumn.Value;
        var leftPolygon = ClipPolygonToMaximumX(
            BuildPolygonFromPoints(leftPoints, contourBounds.Location, bounds),
            splitX - PolygonParams.SeparationPixels);
        var rightPolygon = ClipPolygonToMinimumX(
            BuildPolygonFromPoints(rightPoints, contourBounds.Location, bounds),
            splitX + PolygonParams.SeparationPixels);
        if (leftPolygon.Length < 3 || rightPolygon.Length < 3)
        {
            return [];
        }

        return [leftPolygon, rightPolygon];
    }

    private static PointClusterEvaluation? TryBuildPointClusterPolygons(Point[] candidatePoints, int clusterCount, Point contourOffset, Size bounds)
    {
        using var samples = new Mat(candidatePoints.Length, 2, MatType.CV_32FC1);
        for (var index = 0; index < candidatePoints.Length; index++)
        {
            samples.Set(index, 0, candidatePoints[index].X);
            samples.Set(index, 1, candidatePoints[index].Y);
        }

        using var labels = new Mat();
        using var centers = new Mat();
        var criteria = new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 20, 1.0);
        Cv2.Kmeans(samples, clusterCount, labels, criteria, SplitParams.PointClusterAttempts, KMeansFlags.PpCenters, centers);
        labels.GetArray(out int[] labelValues);

        var groupedPoints = new List<Point>[clusterCount];
        var centerPoints = new Point2d[clusterCount];
        for (var index = 0; index < clusterCount; index++)
        {
            groupedPoints[index] = [];
            centerPoints[index] = new Point2d(centers.At<float>(index, 0), centers.At<float>(index, 1));
        }

        for (var index = 0; index < candidatePoints.Length; index++)
        {
            groupedPoints[labelValues[index]].Add(candidatePoints[index]);
        }

        var polygons = new List<Point[]>(clusterCount);
        var maxAverageRadius = 0.0;
        for (var index = 0; index < groupedPoints.Length; index++)
        {
            if (groupedPoints[index].Count < SplitParams.MinimumPointCount)
            {
                return null;
            }

            var clusterPoints = groupedPoints[index].ToArray();
            var clusterBounds = Cv2.BoundingRect(clusterPoints);
            if ((clusterBounds.Width * clusterBounds.Height) < SplitParams.MinimumRefinedComponentBoundingArea)
            {
                return null;
            }

            var averageRadius = clusterPoints
                .Average(point => GeometryHelper.Distance(new Point2d(point.X, point.Y), centerPoints[index]));
            maxAverageRadius = Math.Max(maxAverageRadius, averageRadius);

            var polygon = BuildPolygonFromPoints(clusterPoints, contourOffset, bounds);
            if (polygon.Length < 3)
            {
                return null;
            }

            polygons.Add(polygon);
        }

        var minimumCenterDistance = double.MaxValue;
        for (var firstIndex = 0; firstIndex < centerPoints.Length; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < centerPoints.Length; secondIndex++)
            {
                minimumCenterDistance = Math.Min(
                    minimumCenterDistance,
                    GeometryHelper.Distance(centerPoints[firstIndex], centerPoints[secondIndex]));
            }
        }

        if (minimumCenterDistance < SplitParams.PointClusterMinimumCentroidDistance)
        {
            return null;
        }

        var separationRatio = maxAverageRadius <= double.Epsilon
            ? minimumCenterDistance
            : minimumCenterDistance / maxAverageRadius;
        if (separationRatio < SplitParams.PointClusterMinimumSeparationRatio)
        {
            return null;
        }

        return new PointClusterEvaluation(polygons, separationRatio);
    }

    private static List<Point> ReduceSeedCenters(List<Point> seedCenters)
    {
        var reducedCenters = new List<Point>(seedCenters.Count);

        foreach (var seedCenter in
                 seedCenters
                     .Where(seedCenter => !reducedCenters
                         .Any(existingCenter => GeometryHelper.Distance(existingCenter, seedCenter) < SplitParams.DensitySeedMinimumCentroidDistance)))
        {
            reducedCenters.Add(seedCenter);
        }

        return reducedCenters;
    }

    private readonly record struct PointClusterEvaluation(List<Point[]> Polygons, double Score);
}
