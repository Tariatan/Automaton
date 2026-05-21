using OpenCvSharp;

namespace Automaton.Detectors;

internal static class AsteroidBeltLandingDetector
{
    private const int BinaryMaskMaxValue = 255;
    private const int MinimumLabelBrightPixelCount = 700;
    private const int MinimumLabelRowBrightPixelCount = 40;
    private const int MinimumLabelWidth = 400;
    private const int MinimumLabelHeight = 35;
    private const int MaximumLabelHeight = 50;

    public static AsteroidBeltLandingAnalysis Analyze(Mat screen)
    {
        if (screen.Empty())
        {
            return AsteroidBeltLandingAnalysis.NotFound;
        }

        var labelBounds = LocateAsteroidBeltLabel(screen);
        return labelBounds is null ? AsteroidBeltLandingAnalysis.NotFound : new AsteroidBeltLandingAnalysis(true);
    }

    private static Rect? LocateAsteroidBeltLabel(Mat screen)
    {
        var searchBounds = BuildLabelSearchBounds(screen.Size());
        using var searchRegion = new Mat(screen, searchBounds);
        using var gray = new Mat();
        using var brightMask = new Mat();

        if (searchRegion.Channels() == 1)
        {
            searchRegion.CopyTo(gray);
        }
        else
        {
            Cv2.CvtColor(searchRegion, gray, ColorConversionCodes.BGR2GRAY);
        }

        Cv2.Threshold(gray, brightMask, 180, BinaryMaskMaxValue, ThresholdTypes.Binary);

        var brightPixelCount = Cv2.CountNonZero(brightMask);
        if (brightPixelCount < MinimumLabelBrightPixelCount)
        {
            return null;
        }

        var localBounds = FindAsteroidBeltLabelBand(brightMask);
        if (localBounds is null)
        {
            return null;
        }

        return new Rect(
            searchBounds.X + localBounds.Value.X,
            searchBounds.Y + localBounds.Value.Y,
            localBounds.Value.Width,
            localBounds.Value.Height);
    }

    private static Rect? FindAsteroidBeltLabelBand(Mat brightMask)
    {
        Rect? bestBounds = null;
        var rowStart = -1;

        for (var y = 0; y < brightMask.Height; y++)
        {
            using var row = brightMask.Row(y);
            var rowBrightPixelCount = Cv2.CountNonZero(row);
            if (rowBrightPixelCount >= MinimumLabelRowBrightPixelCount)
            {
                if (rowStart < 0)
                {
                    rowStart = y;
                }

                continue;
            }

            bestBounds = ConsiderLabelBand(brightMask, rowStart, y - 1, bestBounds);
            rowStart = -1;
        }

        return ConsiderLabelBand(brightMask, rowStart, brightMask.Height - 1, bestBounds);
    }

    private static Rect? ConsiderLabelBand(Mat brightMask, int rowStart, int rowEnd, Rect? bestBounds)
    {
        if (rowStart < 0 || rowEnd < rowStart)
        {
            return bestBounds;
        }

        using var bandMask = new Mat(brightMask, new Rect(0, rowStart, brightMask.Width, rowEnd - rowStart + 1));
        using var brightPixels = new Mat();
        Cv2.FindNonZero(bandMask, brightPixels);
        if (brightPixels.Empty())
        {
            return bestBounds;
        }

        var bounds = Cv2.BoundingRect(brightPixels);
        bounds.Y += rowStart;
        if (bounds.Width < MinimumLabelWidth ||
            bounds.Height < MinimumLabelHeight ||
            bounds.Height > MaximumLabelHeight)
        {
            return bestBounds;
        }

        return bestBounds is null || bounds.Width > bestBounds.Value.Width
            ? bounds
            : bestBounds;
    }

    private static Rect BuildLabelSearchBounds(Size imageSize)
    {
        var left = (int)Math.Round(imageSize.Width * 0.36);
        var top = (int)Math.Round(imageSize.Height * 0.72);
        var width = (int)Math.Round(imageSize.Width * 0.32);
        var height = (int)Math.Round(imageSize.Height * 0.12);
        return new Rect(left, top, width, height);
    }
}

internal sealed record AsteroidBeltLandingAnalysis(bool LandedOnAsteroidBelt)
{
    public static AsteroidBeltLandingAnalysis NotFound { get; } = new(false);
}
