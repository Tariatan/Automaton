using OpenCvSharp;

namespace Automaton;

internal sealed class FirstAsteroidDistanceUnitDetector
{
    private const int DistanceUnitMinimumBrightPixelCount = 10;

    public DistanceUnitKind Detect(Mat screen, Rect mineOverviewBounds, Rect firstAsteroidRowBounds)
    {
        if (screen.Empty())
        {
            return DistanceUnitKind.Unknown;
        }

        if (DetectMetersThousandsSeparator(screen, firstAsteroidRowBounds))
        {
            return DistanceUnitKind.Meters;
        }

        var unitBounds = BuildDistanceUnitSearchBounds(screen.Size(), mineOverviewBounds, firstAsteroidRowBounds);
        using var region = new Mat(screen, unitBounds);
        using var gray = new Mat();
        using var mask = new Mat();
        Cv2.CvtColor(region, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.InRange(gray, new Scalar(110), new Scalar(255), mask);
        if (Cv2.CountNonZero(mask) < DistanceUnitMinimumBrightPixelCount)
        {
            return DistanceUnitKind.Unknown;
        }

        var kProbeWidth = Math.Min(4, mask.Width);
        using var kProbe = new Mat(mask, new Rect(0, 0, kProbeWidth, mask.Height));
        var kProbeBrightPixels = Cv2.CountNonZero(kProbe);
        return kProbeBrightPixels >= 2
            ? DistanceUnitKind.Kilometers
            : DistanceUnitKind.Meters;
    }

    private static bool DetectMetersThousandsSeparator(Mat screen, Rect firstAsteroidRowBounds)
    {
        var probeBounds = BuildDistanceThousandsSeparatorProbeBounds(screen.Size(), firstAsteroidRowBounds);
        using var region = new Mat(screen, probeBounds);
        using var gray = new Mat();
        using var mask = new Mat();
        Cv2.CvtColor(region, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.InRange(gray, new Scalar(110), new Scalar(255), mask);

        var stripHeight = Math.Max(1, mask.Height / 4);
        using var bottomStrip = new Mat(mask, new Rect(0, mask.Height - stripHeight, mask.Width, stripHeight));
        return Cv2.CountNonZero(bottomStrip) >= 2;
    }

    private static Rect BuildDistanceUnitSearchBounds(Size imageSize, Rect mineOverviewBounds, Rect firstAsteroidRowBounds)
    {
        var left = Math.Clamp(firstAsteroidRowBounds.Right - 12, 0, Math.Max(0, imageSize.Width - 1));
        var top = Math.Clamp(firstAsteroidRowBounds.Top + 3, 0, Math.Max(0, imageSize.Height - 1));
        var right = Math.Clamp(mineOverviewBounds.Right - 2, left + 1, imageSize.Width);
        var bottom = Math.Clamp(firstAsteroidRowBounds.Bottom - 3, top + 1, imageSize.Height);
        return new Rect(left, top, right - left, bottom - top);
    }

    private static Rect BuildDistanceThousandsSeparatorProbeBounds(Size imageSize, Rect firstAsteroidRowBounds)
    {
        var left = Math.Clamp(firstAsteroidRowBounds.Right - 92, 0, Math.Max(0, imageSize.Width - 1));
        var top = Math.Clamp(firstAsteroidRowBounds.Top + 2, 0, Math.Max(0, imageSize.Height - 1));
        var right = Math.Clamp(firstAsteroidRowBounds.Right - 24, left + 1, imageSize.Width);
        var bottom = Math.Clamp(firstAsteroidRowBounds.Bottom - 2, top + 1, imageSize.Height);
        return new Rect(left, top, right - left, bottom - top);
    }
}
