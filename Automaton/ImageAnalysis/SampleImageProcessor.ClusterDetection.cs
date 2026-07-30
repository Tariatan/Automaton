using Automaton.Core.Helpers;
using OpenCvSharp;

namespace Automaton.ImageAnalysis;

internal sealed partial class SampleImageProcessor
{
    private static List<Point[]> BuildClusterPolygons(Mat candidateMask, Mat candidateDensityMap, Mat clusterMask, Rect playfieldBounds)
    {
        Cv2.FindContours(
            clusterMask,
            out var contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        var polygons = new List<Point[]>();

        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area < MaskParams.MinimumClusterArea)
            {
                continue;
            }

            IReadOnlyList<Point[]> localPolygons = [];
            if (ShouldAttemptMultiPolygonSplit(area))
            {
                localPolygons = ImageAnalysis.SampleImageProcessor.TryBuildCandidateComponentPolygons(contour, candidateMask, clusterMask.Size());
                if (localPolygons.Count == 0)
                {
                    localPolygons = ImageAnalysis.SampleImageProcessor.TrySplitContourIntoHorizontalSegments(contour, candidateMask, candidateDensityMap, clusterMask.Size());
                }

                if (localPolygons.Count == 0)
                {
                    localPolygons = ImageAnalysis.SampleImageProcessor.TrySplitContourIntoVerticalSegments(contour, candidateMask, candidateDensityMap, clusterMask.Size());
                }

                if (localPolygons.Count == 0)
                {
                    localPolygons = ImageAnalysis.SampleImageProcessor.TrySplitContourByDensitySeeds(contour, candidateMask, candidateDensityMap, clusterMask.Size());
                }

                if (localPolygons.Count == 0)
                {
                    localPolygons = ImageAnalysis.SampleImageProcessor.TrySplitContourByPointClusters(contour, candidateMask, clusterMask.Size());
                }
            }

            if (localPolygons.Count == 0)
            {
                var polygon = ImageAnalysis.SampleImageProcessor.BuildPolygonFromContour(contour, clusterMask.Size());
                if (polygon.Length >= 3)
                {
                    polygons.Add(polygon);
                }

                continue;
            }

            localPolygons = MergeSiblingPolygons(contour, localPolygons, clusterMask.Size());

            polygons.AddRange(localPolygons);
        }

        polygons = [.. polygons.OrderByDescending(points => Math.Abs(Cv2.ContourArea(points)))];

        TryRecoverSparseLowerCluster(candidateMask, clusterMask.Size(), polygons);
        return
        [
            .. polygons
                .Take(PolygonParams.MaximumPerSession)
                .Select(points => ImageAnalysis.SampleImageProcessor.TranslatePolygon(points, playfieldBounds))
        ];
    }

    private static void TryRecoverSparseLowerCluster(Mat candidateMask, Size bounds, List<Point[]> polygons)
    {
        if (polygons.Count != 1)
        {
            return;
        }

        var primaryBounds = Cv2.BoundingRect(polygons[0]);
        var recoveryStartY = primaryBounds.Bottom + RecoveryParams.MinimumGapBelowPrimaryCluster;
        if (recoveryStartY >= bounds.Height)
        {
            return;
        }

        using var pointIndex = new Mat();
        Cv2.FindNonZero(candidateMask, pointIndex);
        if (pointIndex.Empty())
        {
            return;
        }

        pointIndex.GetArray(out Point[] candidatePoints);
        var lowerPoints = candidatePoints
            .Where(point => point.Y >= recoveryStartY)
            .ToArray();
        if (lowerPoints.Length < RecoveryParams.MinimumCandidatePoints)
        {
            return;
        }

        using var sparseMask = new Mat(bounds.Height, bounds.Width, MatType.CV_8UC1, Scalar.All(0));
        foreach (var point in lowerPoints)
        {
            sparseMask.Set(point.Y, point.X, MaskParams.BinaryMaskMaxValue);
        }

        using var blurred = new Mat();
        using var dilated = new Mat();
        using var thresholded = new Mat();
        using var dilateKernel = Cv2.GetStructuringElement(
            MorphShapes.Ellipse,
            new Size(RecoveryParams.DilateKernelSize, RecoveryParams.DilateKernelSize));
        Cv2.GaussianBlur(sparseMask, blurred, new Size(0, 0), RecoveryParams.BlurSigma, RecoveryParams.BlurSigma);
        Cv2.Dilate(blurred, dilated, dilateKernel);
        Cv2.Threshold(dilated, thresholded, RecoveryParams.Threshold, MaskParams.BinaryMaskMaxValue, ThresholdTypes.Binary);

        Cv2.FindContours(
            thresholded,
            out var contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        var contour = contours
            .OrderByDescending(points => Math.Abs(Cv2.ContourArea(points)))
            .FirstOrDefault(points => Cv2.ContourArea(points) >= RecoveryParams.MinimumContourArea);
        if (contour is null)
        {
            return;
        }

        var polygon = ImageAnalysis.SampleImageProcessor.BuildPolygonFromContour(contour, bounds);
        if (polygon.Length < 3)
        {
            return;
        }

        polygons.Add(polygon);
    }

    internal static IReadOnlyList<Point[]> MergeSiblingPolygons(Point[] sourceContour, IReadOnlyList<Point[]> polygons, Size bounds)
    {
        if (polygons.Count < 2)
        {
            return polygons;
        }

        var merged = polygons.ToList();
        var changed = true;

        while (changed)
        {
            changed = false;

            for (var firstIndex = 0; firstIndex < merged.Count; firstIndex++)
            {
                for (var secondIndex = firstIndex + 1; secondIndex < merged.Count; secondIndex++)
                {
                    if (!ShouldMergeSiblingPolygons(merged[firstIndex], merged[secondIndex]))
                    {
                        continue;
                    }

                    var mergedPolygon = ImageAnalysis.SampleImageProcessor.BuildPolygonFromContour(sourceContour, bounds);
                    if (mergedPolygon.Length < 3)
                    {
                        continue;
                    }

                    merged[firstIndex] = mergedPolygon;
                    merged.RemoveAt(secondIndex);
                    changed = true;
                    break;
                }

                if (changed)
                {
                    break;
                }
            }
        }

        return merged;
    }

    internal static bool ShouldMergeSiblingPolygons(Point[] firstPolygon, Point[] secondPolygon)
    {
        var firstBounds = Cv2.BoundingRect(firstPolygon);
        var secondBounds = Cv2.BoundingRect(secondPolygon);

        var horizontalGap = GeometryHelper.GetAxisGap(firstBounds.X, firstBounds.Right, secondBounds.X, secondBounds.Right);
        var verticalGap = GeometryHelper.GetAxisGap(firstBounds.Y, firstBounds.Bottom, secondBounds.Y, secondBounds.Bottom);
        var horizontalOverlapRatio = GeometryHelper.GetAxisOverlapRatio(firstBounds.X, firstBounds.Right, secondBounds.X, secondBounds.Right);
        var verticalOverlapRatio = GeometryHelper.GetAxisOverlapRatio(firstBounds.Y, firstBounds.Bottom, secondBounds.Y, secondBounds.Bottom);

        var firstArea = Math.Abs(Cv2.ContourArea(firstPolygon));
        var secondArea = Math.Abs(Cv2.ContourArea(secondPolygon));
        var smallerArea = Math.Min(firstArea, secondArea);
        var largerArea = Math.Max(firstArea, secondArea);
        var areaRatio = largerArea <= double.Epsilon
            ? 0.0
            : smallerArea / largerArea;

        if (areaRatio > PolygonParams.MaximumSiblingAreaRatio)
        {
            return false;
        }

        var sideBySideSiblings = horizontalGap <= PolygonParams.MaximumSiblingMergeGap &&
                                 verticalOverlapRatio >= PolygonParams.MinimumSiblingAxisOverlapRatio;
        var stackedSiblings = verticalGap <= PolygonParams.MaximumSiblingMergeGap &&
                              horizontalOverlapRatio >= PolygonParams.MinimumSiblingAxisOverlapRatio;

        return sideBySideSiblings || stackedSiblings;
    }

    internal static bool ShouldAttemptMultiPolygonSplit(double contourArea)
    {
        return contourArea >= SplitParams.MinimumContourArea;
    }

    internal static bool ShouldAttemptSideBySideSplit(Rect contourBounds)
    {
        return contourBounds.Height >= SplitParams.MinimumContourHeightForSideBySideSplit;
    }
}
