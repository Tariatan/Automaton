using OpenCvSharp;

namespace Automaton.ImageAnalysis;

internal sealed partial class SampleImageProcessor
{
    internal static int? TryFindVerticalSplitRow(IReadOnlyList<Point> candidatePoints, int height)
    {
        if (height < SplitParams.MinimumSegmentHeight * 2)
        {
            return null;
        }

        var rowCounts = new double[height];
        foreach (var point in candidatePoints)
        {
            rowCounts[Math.Clamp(point.Y, 0, height - 1)]++;
        }

        var smoothedCounts = SmoothHistogram(rowCounts, SplitParams.HistogramSmoothingRadius);
        return FindBestValleyIndex(smoothedCounts, SplitParams.MinimumSegmentHeight);
    }

    private static int? TryFindVerticalSplitRow(Mat weightedDensityMask, int height)
    {
        if (height < SplitParams.MinimumSegmentHeight * 2)
        {
            return null;
        }

        var rowSums = new double[height];
        for (var row = 0; row < height; row++)
        {
            rowSums[row] = Cv2.Sum(weightedDensityMask.Row(row)).Val0;
        }

        return TryFindWeightedSplitIndex(rowSums, SplitParams.MinimumSegmentHeight);
    }

    internal static int? TryFindHorizontalSplitColumn(IReadOnlyList<Point> candidatePoints, int width)
    {
        if (width < SplitParams.MinimumSegmentWidth * 2)
        {
            return null;
        }

        var columnCounts = new double[width];
        foreach (var point in candidatePoints)
        {
            columnCounts[Math.Clamp(point.X, 0, width - 1)]++;
        }

        var smoothedCounts = SmoothHistogram(columnCounts, SplitParams.HistogramSmoothingRadius);
        return FindBestValleyIndex(smoothedCounts, SplitParams.MinimumSegmentWidth);
    }

    private static int? TryFindHorizontalSplitColumn(Mat weightedDensityMask, int width)
    {
        if (width < SplitParams.MinimumSegmentWidth * 2)
        {
            return null;
        }

        var columnSums = new double[width];
        for (var column = 0; column < width; column++)
        {
            columnSums[column] = Cv2.Sum(weightedDensityMask.Col(column)).Val0;
        }

        return TryFindWeightedSplitIndex(columnSums, SplitParams.MinimumSegmentWidth);
    }

    private static int? TryFindWeightedSplitIndex(IReadOnlyList<double> values, int minimumSegmentSize)
    {
        var smoothedValues = SmoothHistogram(values, SplitParams.HistogramSmoothingRadius);
        return FindBestValleyIndex(smoothedValues, minimumSegmentSize);
    }

    private static int? FindBestValleyIndex(double[] smoothed, int minimumSegmentSize)
    {
        var length = smoothed.Length;
        if (length < minimumSegmentSize * 2)
        {
            return null;
        }

        var prefixMax = new double[length];
        var suffixMax = new double[length];
        prefixMax[0] = smoothed[0];
        for (var i = 1; i < length; i++)
        {
            prefixMax[i] = Math.Max(prefixMax[i - 1], smoothed[i]);
        }

        suffixMax[length - 1] = smoothed[length - 1];
        for (var i = length - 2; i >= 0; i--)
        {
            suffixMax[i] = Math.Max(suffixMax[i + 1], smoothed[i]);
        }

        var bestIndex = -1;
        var bestValleyRatio = double.MaxValue;

        for (var index = minimumSegmentSize; index < length - minimumSegmentSize; index++)
        {
            var leadingPeak = prefixMax[index - 1];
            var trailingPeak = suffixMax[index + 1];
            if (leadingPeak < SplitParams.MinimumPeakDensity || trailingPeak < SplitParams.MinimumPeakDensity)
            {
                continue;
            }

            var valleyRatio = smoothed[index] / Math.Min(leadingPeak, trailingPeak);
            if (valleyRatio >= bestValleyRatio)
            {
                continue;
            }

            bestValleyRatio = valleyRatio;
            bestIndex = index;
        }

        return bestIndex >= 0 && bestValleyRatio <= SplitParams.MaximumValleyRatio
            ? bestIndex
            : null;
    }

    private static double[] SmoothHistogram(IReadOnlyList<double> values, int radius)
    {
        var smoothed = new double[values.Count];

        for (var index = 0; index < values.Count; index++)
        {
            var start = Math.Max(0, index - radius);
            var end = Math.Min(values.Count - 1, index + radius);
            var sum = 0.0;

            for (var cursor = start; cursor <= end; cursor++)
            {
                sum += values[cursor];
            }

            smoothed[index] = sum / (end - start + 1);
        }

        return smoothed;
    }
}
