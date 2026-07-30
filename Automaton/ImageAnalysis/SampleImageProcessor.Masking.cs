using OpenCvSharp;

namespace Automaton.ImageAnalysis;

internal sealed partial class SampleImageProcessor
{
    private static Mat CropAnalysisRegion(Mat image)
    {
        const int AnalysisWidth = 1000;
        const int AnalysisHeight = 1000;

        var width = Math.Min(AnalysisWidth, image.Width);
        var height = Math.Min(AnalysisHeight, image.Height);

        if (width == image.Width && height == image.Height)
        {
            return image.Clone();
        }

        return new Mat(image, new Rect(0, 0, width, height)).Clone();
    }

    private static (Mat CandidateMask, Mat CandidateDensityMap) BuildCandidateMaskAndDensityMap(Mat playfieldImage)
    {
        using var hsv = new Mat();
        Cv2.CvtColor(playfieldImage, hsv, ColorConversionCodes.BGR2HSV);

        var channels = Cv2.Split(hsv);
        try
        {
            using var openKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(MaskParams.CandidateOpenKernelSize, MaskParams.CandidateOpenKernelSize));

            using var saturationMask = new Mat();
            using var brightnessMask = new Mat();
            using var combinedMask = new Mat();
            var openedMask = new Mat();
            Cv2.Threshold(channels[1], saturationMask, MaskParams.SaturationThreshold, MaskParams.BinaryMaskMaxValue, ThresholdTypes.Binary);
            Cv2.Threshold(channels[2], brightnessMask, MaskParams.BrightnessThreshold, MaskParams.BinaryMaskMaxValue, ThresholdTypes.Binary);
            Cv2.BitwiseAnd(saturationMask, brightnessMask, combinedMask);
            Cv2.MorphologyEx(combinedMask, openedMask, MorphTypes.Open, openKernel);

            using var density = new Mat();
            using var filteredDensity = new Mat();
            var openedDensity = new Mat();
            Cv2.Min(channels[1], channels[2], density);
            Cv2.BitwiseAnd(density, saturationMask, filteredDensity);
            Cv2.MorphologyEx(filteredDensity, openedDensity, MorphTypes.Open, openKernel);

            return (openedMask, openedDensity);
        }
        finally
        {
            foreach (var channel in channels)
            {
                channel.Dispose();
            }
        }
    }

    private static Mat BuildClusterMask(Mat candidateMask)
    {
        using var blurred = new Mat();
        using var dilated = new Mat();
        using var thresholded = new Mat();
        using var opened = new Mat();

        Cv2.GaussianBlur(candidateMask, blurred, new Size(0, 0), MaskParams.ClusterBlurSigma, MaskParams.ClusterBlurSigma);

        using var dilateKernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(MaskParams.ClusterDilateKernelSize, MaskParams.ClusterDilateKernelSize));
        Cv2.Dilate(blurred, dilated, dilateKernel);
        Cv2.Threshold(dilated, thresholded, MaskParams.ClusterThreshold, MaskParams.BinaryMaskMaxValue, ThresholdTypes.Binary);

        using var closeKernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(MaskParams.ClusterCloseKernelSize, MaskParams.ClusterCloseKernelSize));
        Cv2.MorphologyEx(thresholded, thresholded, MorphTypes.Close, closeKernel);
        using var openKernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(MaskParams.ClusterOpenKernelSize, MaskParams.ClusterOpenKernelSize));
        Cv2.MorphologyEx(thresholded, opened, MorphTypes.Open, openKernel);

        return opened.Clone();
    }

    private static (Mat MaskedCandidates, Mat ContourMask) MaskCandidatesWithinContour(Point[] contour, Rect contourBounds, Mat candidateMask)
    {
        var contourMask = new Mat(contourBounds.Height, contourBounds.Width, MatType.CV_8UC1, Scalar.All(0));
        var contourInRoi = contour
            .Select(point => new Point(point.X - contourBounds.X, point.Y - contourBounds.Y))
            .ToArray();
        Cv2.FillPoly(contourMask, [contourInRoi], Scalar.All(MaskParams.BinaryMaskMaxValue));

        using var candidateRegion = new Mat(candidateMask, contourBounds);
        var maskedCandidates = new Mat();
        Cv2.BitwiseAnd(candidateRegion, contourMask, maskedCandidates);
        return (maskedCandidates, contourMask);
    }

    private static Point[] TryGetContourCandidatePoints(Point[] contour, Rect contourBounds, Mat candidateMask)
    {
        var (maskedCandidates, contourMask) = MaskCandidatesWithinContour(contour, contourBounds, candidateMask);
        using var candidates = maskedCandidates;
        using var mask = contourMask;
        using var candidatePointIndex = new Mat();
        Cv2.FindNonZero(maskedCandidates, candidatePointIndex);
        if (candidatePointIndex.Empty())
        {
            return [];
        }

        candidatePointIndex.GetArray(out Point[] candidatePoints);
        return candidatePoints;
    }
}
