using OpenCvSharp;

namespace Automaton;

internal sealed class AsteroidBeltLandingDetector
{
    private const int BinaryMaskMaxValue = 255;
    private const int MinimumLabelBrightPixelCount = 700;
    private const int MinimumLabelRowBrightPixelCount = 40;
    private const int MinimumLabelWidth = 400;
    private const int MinimumLabelHeight = 35;
    private const int MaximumLabelHeight = 110;
    private const int MinimumMinePanelBorderPixelCount = 120;
    private const int MinimumAsteroidIconArea = 4;
    private const int MaximumAsteroidIconWidth = 18;
    private const int MaximumAsteroidIconHeight = 18;
    private const int AsteroidIconGroupMaximumDistance = 18;

    public AsteroidBeltLandingAnalysis Analyze(Mat screen)
    {
        if (screen.Empty())
        {
            return AsteroidBeltLandingAnalysis.NotFound;
        }

        using var searchableScreen = BuildSearchableScreen(screen);
        var labelBounds = LocateAsteroidBeltLabel(searchableScreen);
        if (labelBounds is null)
        {
            return AsteroidBeltLandingAnalysis.NotFound;
        }

        var mineOverviewBounds = LocateMineOverview(searchableScreen, labelBounds.Value);
        var asteroidRows = mineOverviewBounds is null
            ? []
            : LocateAsteroidRows(searchableScreen, mineOverviewBounds.Value);

        return new AsteroidBeltLandingAnalysis(
            true,
            labelBounds,
            mineOverviewBounds,
            asteroidRows);
    }

    private static Rect? LocateAsteroidBeltLabel(Mat screen)
    {
        var searchBounds = BuildLabelSearchBounds(screen.Size());
        using var searchRegion = new Mat(screen, searchBounds);
        using var gray = new Mat();
        using var brightMask = new Mat();

        Cv2.CvtColor(searchRegion, gray, ColorConversionCodes.BGR2GRAY);
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

        if (localBounds.Value.Width < MinimumLabelWidth ||
            localBounds.Value.Height < MinimumLabelHeight ||
            localBounds.Value.Height > MaximumLabelHeight)
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

    private static Rect? LocateMineOverview(Mat screen, Rect labelBounds)
    {
        var searchBounds = BuildMineOverviewSearchBounds(screen.Size(), labelBounds);
        using var searchRegion = new Mat(screen, searchBounds);
        var bestBorder = FindBestMinePanelTopBorder(searchRegion);
        if (bestBorder is null)
        {
            return null;
        }

        var left = searchBounds.X + bestBorder.Value.Left;
        var top = searchBounds.Y + bestBorder.Value.Y;
        var right = searchBounds.X + bestBorder.Value.Right;
        var bottom = Math.Min(screen.Height, labelBounds.Top);
        return new Rect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
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

    private static IReadOnlyList<AsteroidOverviewEntry> LocateAsteroidRows(Mat screen, Rect mineOverviewBounds)
    {
        var iconColumnBounds = BuildMineAsteroidIconColumnBounds(screen.Size(), mineOverviewBounds);
        using var iconColumn = new Mat(screen, iconColumnBounds);
        using var gray = new Mat();
        using var mask = new Mat();
        Cv2.CvtColor(iconColumn, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.InRange(gray, new Scalar(80), new Scalar(180), mask);
        Cv2.FindContours(
            mask,
            out var contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        var iconCenters = new List<int>();
        foreach (var contour in contours)
        {
            var bounds = Cv2.BoundingRect(contour);
            if (Cv2.ContourArea(contour) < MinimumAsteroidIconArea ||
                bounds.Width > MaximumAsteroidIconWidth ||
                bounds.Height > MaximumAsteroidIconHeight)
            {
                continue;
            }

            iconCenters.Add(iconColumnBounds.Y + bounds.Y + bounds.Height / 2);
        }

        var rowLeft = Math.Clamp(mineOverviewBounds.X + 28, 0, Math.Max(0, screen.Width - 1));
        var rowWidth = Math.Clamp(mineOverviewBounds.Width - 55, 1, screen.Width - rowLeft);
        return GroupIconCenters(iconCenters)
            .Select(group => (int)Math.Round(group.Average()))
            .Select(centerY =>
            {
                var rowTop = Math.Clamp(centerY - 17, 0, Math.Max(0, screen.Height - 1));
                var rowHeight = Math.Clamp(34, 1, screen.Height - rowTop);
                return new AsteroidOverviewEntry(new Rect(rowLeft, rowTop, rowWidth, rowHeight));
            })
            .OrderBy(row => row.Bounds.Y)
            .ToArray();
    }

    private static IReadOnlyList<IReadOnlyList<int>> GroupIconCenters(IReadOnlyList<int> iconCenters)
    {
        var groups = new List<List<int>>();
        foreach (var center in iconCenters.Order())
        {
            var currentGroup = groups.LastOrDefault();
            if (currentGroup is null ||
                Math.Abs(center - currentGroup.Average()) > AsteroidIconGroupMaximumDistance)
            {
                groups.Add([center]);
                continue;
            }

            currentGroup.Add(center);
        }

        return groups;
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

    private static Rect BuildLabelSearchBounds(Size imageSize)
    {
        return BuildRelativeBounds(imageSize, 0.36, 0.72, 0.32, 0.12);
    }

    private static Rect BuildMineOverviewSearchBounds(Size imageSize, Rect labelBounds)
    {
        var left = Math.Clamp(labelBounds.Right + 80, 0, Math.Max(0, imageSize.Width - 1));
        var top = Math.Clamp(labelBounds.Top - 420, 0, Math.Max(0, imageSize.Height - 1));
        var right = Math.Clamp(labelBounds.Right + 520, left + 1, imageSize.Width);
        var bottom = Math.Clamp(labelBounds.Top, top + 1, imageSize.Height);
        return new Rect(left, top, right - left, bottom - top);
    }

    private static Rect BuildMineAsteroidIconColumnBounds(Size imageSize, Rect mineOverviewBounds)
    {
        var left = Math.Clamp(mineOverviewBounds.X + 30, 0, Math.Max(0, imageSize.Width - 1));
        var top = Math.Clamp(mineOverviewBounds.Y + 115, 0, Math.Max(0, imageSize.Height - 1));
        var bottom = Math.Min(imageSize.Height, mineOverviewBounds.Bottom);
        var width = Math.Clamp(70, 1, imageSize.Width - left);
        var height = Math.Clamp(bottom - top, 1, imageSize.Height - top);
        return new Rect(left, top, width, height);
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

    private static Mat BuildSearchableScreen(Mat screen)
    {
        if (screen.Channels() == 3)
        {
            return screen.Clone();
        }

        var colorScreen = new Mat();
        Cv2.CvtColor(screen, colorScreen, ColorConversionCodes.GRAY2BGR);
        return colorScreen;
    }

    private readonly record struct BorderLocation(int Y, int Left, int Right, int PixelCount);
}

internal sealed record AsteroidBeltLandingAnalysis(
    bool LandedOnAsteroidBelt,
    Rect? AsteroidBeltLabelBounds,
    Rect? MineOverviewBounds,
    IReadOnlyList<AsteroidOverviewEntry> Asteroids)
{
    public static AsteroidBeltLandingAnalysis NotFound { get; } = new(
        false,
        null,
        null,
        []);
}

internal sealed record AsteroidOverviewEntry(Rect Bounds);
