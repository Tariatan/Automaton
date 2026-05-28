using OpenCvSharp;
using Serilog;

namespace Automaton.Detectors;

internal static class AsteroidBeltLandingDetector
{
    private const int MinimumLabelBrightPixelCount = 700;
    private const int MinimumLabelRowBrightPixelCount = 40;
    private const int MinimumLabelWidth = 400;
    private const int MinimumLabelHeight = 35;
    private const int MaximumLabelHeight = 50;
    private const double DebugOverlayTextScale = 0.8;
    private const int DebugOverlayTextThickness = 2;
    private const int DebugOverlayLeftPadding = 30;
    private const int DebugOverlayTopPadding = 40;
    private static readonly Scalar DebugOverlayColor = new(80, 120, 255);
    private static readonly Scalar SearchBoundsColor = new(255, 200, 120);
    private static readonly Scalar LabelBoundsColor = new(120, 255, 120);
    private static readonly ILogger Logger = Log.ForContext(typeof(AsteroidBeltLandingDetector));

    public static AsteroidBeltLandingAnalysis Detect(Mat screen, bool drawDebugOverlay = true)
    {
        if (screen.Empty())
        {
            return AsteroidBeltLandingAnalysis.NotFound;
        }

        var searchBounds = BuildLabelSearchBounds(screen.Size());
        var labelBounds = LocateAsteroidBeltLabel(screen, searchBounds);
        var analysis = labelBounds is null
            ? new AsteroidBeltLandingAnalysis(false, searchBounds, null)
            : new AsteroidBeltLandingAnalysis(true, searchBounds, labelBounds);

        if (drawDebugOverlay)
        {
            DrawDebugOverlay(screen, analysis);
        }

        return analysis;
    }

    private static void DrawDebugOverlay(Mat image, AsteroidBeltLandingAnalysis analysis)
    {
        if (image.Empty())
        {
            return;
        }

        if (analysis.SearchBounds is not null)
        {
            Cv2.Rectangle(image, analysis.SearchBounds.Value, SearchBoundsColor, 2);
        }

        if (analysis.LabelBounds is not null)
        {
            Cv2.Rectangle(image, analysis.LabelBounds.Value, LabelBoundsColor, 2);
        }

        var overlayText = $"Belt landing: {(analysis.LandedOnAsteroidBelt ? "detected" : "not found")}";
        Cv2.PutText(
            image,
            overlayText,
            new Point(DebugOverlayLeftPadding, DebugOverlayTopPadding),
            HersheyFonts.HersheySimplex,
            DebugOverlayTextScale,
            DebugOverlayColor,
            DebugOverlayTextThickness,
            LineTypes.AntiAlias);

        Logger.Information(
            "Asteroid belt landing: LandedOnAsteroidBelt={LandedOnAsteroidBelt}",
            analysis.LandedOnAsteroidBelt);
    }

    private static Rect? LocateAsteroidBeltLabel(Mat screen, Rect searchBounds)
    {
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

        Cv2.Threshold(gray, brightMask, 180, 255, ThresholdTypes.Binary);

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
        using var rowSums = new Mat();
        Cv2.Reduce(brightMask, rowSums, ReduceDimension.Column, ReduceTypes.Sum, MatType.CV_32S);

        Rect? bestBounds = null;
        var rowStart = -1;
        const int Threshold = MinimumLabelRowBrightPixelCount * 255;

        for (var y = 0; y < brightMask.Height; y++)
        {
            if (rowSums.At<int>(y, 0) >= Threshold)
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
        using var colSums = new Mat();
        Cv2.Reduce(bandMask, colSums, ReduceDimension.Row, ReduceTypes.Sum, MatType.CV_32S);

        var left = -1;
        var right = -1;
        for (var x = 0; x < colSums.Cols; x++)
        {
            if (colSums.At<int>(0, x) <= 0)
            {
                continue;
            }

            if (left < 0)
            {
                left = x;
            }

            right = x;
        }

        if (left < 0)
        {
            return bestBounds;
        }

        var width = right - left + 1;
        var height = rowEnd - rowStart + 1;
        if (width < MinimumLabelWidth || height < MinimumLabelHeight || height > MaximumLabelHeight)
        {
            return bestBounds;
        }

        var bounds = new Rect(left, rowStart, width, height);
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

internal sealed record AsteroidBeltLandingAnalysis(
    bool LandedOnAsteroidBelt,
    Rect? SearchBounds,
    Rect? LabelBounds)
{
    public static AsteroidBeltLandingAnalysis NotFound { get; } = new(false, null, null);
}
