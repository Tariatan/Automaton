using OpenCvSharp;

namespace Automaton.Detectors;

internal sealed class MineOverviewDetector
{
    private const int MinimumMinePanelBorderPixelCount = 120;
    private const int NothingFoundMinimumBrightPixelCount = 180;

    public bool TryLocate(Mat screen, Rect asteroidBeltLabelBounds, out Rect mineOverviewBounds)
    {
        mineOverviewBounds = default;
        if (screen.Empty())
        {
            return false;
        }

        var searchBounds = BuildLabelAnchoredSearchBounds(screen.Size(), asteroidBeltLabelBounds);
        return TryLocateInSearchBounds(screen, searchBounds, out mineOverviewBounds, bottomLimit: asteroidBeltLabelBounds.Top);
    }

    public bool TryLocate(Mat screen, out Rect mineOverviewBounds)
    {
        mineOverviewBounds = default;
        if (screen.Empty())
        {
            return false;
        }

        var searchBounds = BuildFallbackSearchBounds(screen.Size());
        return TryLocateInSearchBounds(screen, searchBounds, out mineOverviewBounds, bottomLimit: null);
    }

    public bool DetectNothingFound(Mat screen, Rect mineOverviewBounds)
    {
        if (screen.Empty())
        {
            return false;
        }

        var nothingFoundBounds = BuildNothingFoundSearchBounds(screen.Size(), mineOverviewBounds);
        using var region = new Mat(screen, nothingFoundBounds);
        using var gray = new Mat();
        using var mask = new Mat();
        Cv2.CvtColor(region, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.InRange(gray, new Scalar(110), new Scalar(255), mask);
        return Cv2.CountNonZero(mask) >= NothingFoundMinimumBrightPixelCount;
    }

    private static bool TryLocateInSearchBounds(Mat screen, Rect searchBounds, out Rect mineOverviewBounds, int? bottomLimit)
    {
        mineOverviewBounds = default;
        using var searchRegion = new Mat(screen, searchBounds);
        var bestBorder = FindBestMinePanelTopBorder(searchRegion);
        if (bestBorder is null)
        {
            return false;
        }

        var left = searchBounds.X + bestBorder.Value.Left;
        var top = searchBounds.Y + bestBorder.Value.Y;
        var right = searchBounds.X + bestBorder.Value.Right;
        var bottom = bottomLimit ?? Math.Min(screen.Height, top + 380);
        bottom = Math.Clamp(bottom, top + 1, screen.Height);
        mineOverviewBounds = new Rect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
        return true;
    }

    private static BorderLocation? FindBestMinePanelTopBorder(Mat searchRegion)
    {
        BorderLocation? bestBorder = null;
        for (var y = 0; y < searchRegion.Height; y++)
        {
            var left = int.MaxValue;
            var right = int.MinValue;
            var borderPixelCount = 0;

            for (var x = 0; x < searchRegion.Width; x++)
            {
                var pixel = searchRegion.At<Vec3b>(y, x);
                if (!IsMinePanelBorderPixel(pixel))
                {
                    continue;
                }

                left = Math.Min(left, x);
                right = Math.Max(right, x + 1);
                borderPixelCount++;
            }

            if (borderPixelCount < MinimumMinePanelBorderPixelCount)
            {
                continue;
            }

            if (bestBorder is null)
            {
                bestBorder = new BorderLocation(y, left, right, borderPixelCount);
            }
        }

        return bestBorder;
    }

    private static bool IsMinePanelBorderPixel(Vec3b pixel)
    {
        return IsMinePanelAccentBorderPixel(pixel) ||
               IsMinePanelNeutralBorderPixel(pixel);
    }

    private static bool IsMinePanelAccentBorderPixel(Vec3b pixel)
    {
        return pixel.Item0 >= 70 &&
               pixel.Item0 <= 150 &&
               pixel.Item1 >= 70 &&
               pixel.Item1 <= 150 &&
               pixel.Item2 <= 80;
    }

    private static bool IsMinePanelNeutralBorderPixel(Vec3b pixel)
    {
        var minChannel = Math.Min(pixel.Item0, Math.Min(pixel.Item1, pixel.Item2));
        var maxChannel = Math.Max(pixel.Item0, Math.Max(pixel.Item1, pixel.Item2));
        return minChannel >= 25 &&
               maxChannel <= 60 &&
               maxChannel - minChannel <= 6;
    }

    private static Rect BuildLabelAnchoredSearchBounds(Size imageSize, Rect labelBounds)
    {
        var left = Math.Clamp(labelBounds.Right + 80, 0, Math.Max(0, imageSize.Width - 1));
        var top = Math.Clamp(labelBounds.Top - 420, 0, Math.Max(0, imageSize.Height - 1));
        var right = Math.Clamp(labelBounds.Right + 520, left + 1, imageSize.Width);
        var bottom = Math.Clamp(labelBounds.Top, top + 1, imageSize.Height);
        return new Rect(left, top, right - left, bottom - top);
    }

    private static Rect BuildFallbackSearchBounds(Size imageSize)
    {
        return BuildRelativeBounds(imageSize, 0.58, 0.55, 0.40, 0.43);
    }

    private static Rect BuildNothingFoundSearchBounds(Size imageSize, Rect mineOverviewBounds)
    {
        var left = Math.Clamp(mineOverviewBounds.X + 82, 0, Math.Max(0, imageSize.Width - 1));
        var top = Math.Clamp(mineOverviewBounds.Y + 205, 0, Math.Max(0, imageSize.Height - 1));
        var right = Math.Clamp(mineOverviewBounds.Right - 72, left + 1, imageSize.Width);
        var bottom = Math.Clamp(mineOverviewBounds.Bottom - 24, top + 1, imageSize.Height);
        return new Rect(left, top, right - left, bottom - top);
    }

    private static Rect BuildRelativeBounds(
        Size imageSize,
        double leftRatio,
        double topRatio,
        double widthRatio,
        double heightRatio)
    {
        var left = (int)Math.Round(imageSize.Width * leftRatio);
        var top = (int)Math.Round(imageSize.Height * topRatio);
        var width = (int)Math.Round(imageSize.Width * widthRatio);
        var height = (int)Math.Round(imageSize.Height * heightRatio);

        left = Math.Clamp(left, 0, Math.Max(0, imageSize.Width - 1));
        top = Math.Clamp(top, 0, Math.Max(0, imageSize.Height - 1));
        width = Math.Clamp(width, 1, imageSize.Width - left);
        height = Math.Clamp(height, 1, imageSize.Height - top);
        return new Rect(left, top, width, height);
    }

    private readonly record struct BorderLocation(int Y, int Left, int Right, int PixelCount);
}
