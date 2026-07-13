using System.Globalization;
using OpenCvSharp;

namespace Automaton.Detectors;

internal sealed class AccuracyDetector
{
    private const int BinaryMaskMaxValue = 255;
    private const int MinimumMaximumChannel = 90;
    private const int MinimumGrayValue = 70;
    private const int MaximumNeutralChannelSpread = 60;
    private const int MaximumHorizontalLineCoveragePercent = 55;
    private const int TextWindowHeight = 26;
    private const int MinimumTextWindowHeight = 14;
    private const int MinimumPercentGlyphWidth = 8;
    private const int MaximumPercentGlyphWidth = 18;
    private const int MinimumDigitGlyphWidth = 3;
    private const int MinimumDigitGlyphHeight = 8;
    private const int MaximumDigitGlyphWidth = 11;
    private const int MaximumDecimalPointWidth = 3;
    private const int MaximumDecimalPointHeight = 4;
    private const int MinimumGlyphCount = 5;
    private const int ExpectedTextHeight = 13;
    private const int HeightPenaltyWeight = 4;
    private const int DecimalPointVerticalTolerance = 3;
    private const double MinimumCandidateScore = 40.0;
    private const double EarlyExitScore = 240.0;
    private static readonly Rect SearchBounds = new(950, 20, 130, 90);

    public AccuracyDetection Detect(Mat screen)
    {
        if (screen.Empty())
        {
            return AccuracyDetection.NotFound(new Rect());
        }

        var searchBounds = BuildSearchBounds(screen.Size());
        using var searchRegion = new Mat(screen, searchBounds);
        using var mask = BuildTextMask(searchRegion);
        RemoveHorizontalLines(mask);

        return TryDetect(mask, searchBounds, out var detection)
            ? detection
            : AccuracyDetection.NotFound(searchBounds);
    }

    private static bool TryDetect(Mat mask, Rect searchBounds, out AccuracyDetection detection)
    {
        TextCandidate? bestCandidate = null;
        for (var top = 0; top <= mask.Height - MinimumTextWindowHeight; top++)
        {
            var height = Math.Min(TextWindowHeight, mask.Height - top);
            if (height < MinimumTextWindowHeight)
            {
                continue;
            }

            using var window = new Mat(mask, new Rect(0, top, mask.Width, height));
            if (!TryReadWindow(mask, window, top, searchBounds.Location, out var candidate))
            {
                continue;
            }

            if (bestCandidate is null || candidate.Score > bestCandidate.Value.Score)
            {
                bestCandidate = candidate;
            }

            if (bestCandidate.Value.Score >= EarlyExitScore)
            {
                detection = BuildDetection(searchBounds, bestCandidate.Value);
                return true;
            }
        }

        if (bestCandidate is null || bestCandidate.Value.Score < MinimumCandidateScore)
        {
            detection = AccuracyDetection.NotFound(searchBounds);
            return false;
        }

        detection = BuildDetection(searchBounds, bestCandidate.Value);
        return true;
    }

    private static AccuracyDetection BuildDetection(Rect searchBounds, TextCandidate candidate)
    {
        return new AccuracyDetection(
            true,
            searchBounds,
            candidate.Bounds,
            candidate.Percentage,
            candidate.Text);
    }

    private static bool TryReadWindow(
        Mat fullMask,
        Mat window,
        int windowTop,
        Point screenOffset,
        out TextCandidate candidate)
    {
        candidate = default;
        var runs = BuildGlyphRuns(window, windowTop);
        if (runs.Count < MinimumGlyphCount)
        {
            return false;
        }

        var textBottom = runs.Max(run => run.Bounds.Bottom);
        var percentIndex = runs.Count - 1;
        if (!IsPercentGlyph(runs[percentIndex]))
        {
            return false;
        }

        var decimalPointIndex = -1;
        for (var index = percentIndex - 1; index >= 0; index--)
        {
            if (IsDecimalPointGlyph(runs[index], textBottom))
            {
                decimalPointIndex = index;
                break;
            }
        }

        if (decimalPointIndex <= 0 || decimalPointIndex > 3 || decimalPointIndex != percentIndex - 2)
        {
            return false;
        }

        var integerDigits = ReadDigits(fullMask, runs, 0, decimalPointIndex);
        var fractionalDigits = ReadDigits(fullMask, runs, decimalPointIndex + 1, percentIndex);
        if (integerDigits is null || fractionalDigits is null)
        {
            return false;
        }

        var text = $"{integerDigits}.{fractionalDigits}%";
        var percentage = double.Parse(
            $"{integerDigits}.{fractionalDigits}",
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture);
        var scoredRuns = runs.Take(percentIndex + 1).ToList();
        var localBounds = UnionBounds(scoredRuns.Select(run => run.Bounds));
        var screenBounds = new Rect(
            screenOffset.X + localBounds.X,
            screenOffset.Y + localBounds.Y,
            localBounds.Width,
            localBounds.Height);
        var score = scoredRuns.Sum(run => run.PixelCount) - Math.Abs(localBounds.Height - ExpectedTextHeight) * HeightPenaltyWeight;
        candidate = new TextCandidate(screenBounds, percentage, text, score);
        return true;
    }

    private static string? ReadDigits(Mat fullMask, IReadOnlyList<GlyphRun> runs, int startIndex, int endIndex)
    {
        if (startIndex >= endIndex)
        {
            return null;
        }

        var digits = new char[endIndex - startIndex];
        for (var index = startIndex; index < endIndex; index++)
        {
            var bounds = runs[index].Bounds;
            if (bounds.Width < MinimumDigitGlyphWidth || bounds.Height < MinimumDigitGlyphHeight || bounds.Width > MaximumDigitGlyphWidth)
            {
                return null;
            }

            using var glyphMask = new Mat(fullMask, bounds);
            if (!TryRecognizeDigit(glyphMask, out var digit))
            {
                return null;
            }

            digits[index - startIndex] = (char)('0' + digit);
        }

        return new string(digits);
    }

    private static bool TryRecognizeDigit(Mat glyphMask, out int digit)
    {
        var features = DigitFeatures.From(glyphMask);

        if (features is { Width: <= 4, Top: < 0.55, Bottom: < 0.55 })
        {
            digit = 1;
            return true;
        }

        if (features is { UpperRight: < 0.15, Top: < 0.55, LowerLeft: > 0.45 })
        {
            digit = 4;
            return true;
        }

        if (features is { UpperLeft: < 0.35, LowerLeft: > 0.45, LowerRight: < 0.45 })
        {
            digit = 2;
            return true;
        }

        if (features is { UpperLeft: < 0.35, LowerLeft: < 0.35 })
        {
            digit = features.Bottom < 0.45 ? 7 : 3;
            return true;
        }

        if (features is { UpperLeft: > 0.55, LowerLeft: > 0.55, UpperRight: < 0.55 })
        {
            digit = 6;
            return true;
        }

        if (features.UpperRight < 0.45)
        {
            digit = features.LowerLeft > 0.55 ? 6 : 5;
            return true;
        }

        if (features.LowerLeft < 0.45)
        {
            digit = 9;
            return true;
        }

        if (features.Top + features.Bottom < 0.30)
        {
            digit = 0;
            return false;
        }

        digit = features.Middle > 0.70 ? 8 : 0;
        return true;
    }

    private static IReadOnlyList<GlyphRun> BuildGlyphRuns(Mat window, int windowTop)
    {
        using var columnProjection = new Mat();
        Cv2.Reduce(window, columnProjection, ReduceDimension.Row, ReduceTypes.Sum, MatType.CV_32S);

        var runs = new List<ColumnRun>();
        var start = -1;
        for (var x = 0; x < window.Width; x++)
        {
            var hasPixels = columnProjection.At<int>(0, x) > 0;
            if (hasPixels && start < 0)
            {
                start = x;
            }
            else if (!hasPixels && start >= 0)
            {
                runs.Add(new ColumnRun(start, x - 1));
                start = -1;
            }
        }

        if (start >= 0)
        {
            runs.Add(new ColumnRun(start, window.Width - 1));
        }

        return runs
            .Select(run => BuildGlyphRun(window, run, windowTop))
            .Where(run => run.PixelCount > 1)
            .ToArray();
    }

    private static GlyphRun BuildGlyphRun(Mat window, ColumnRun run, int windowTop)
    {
        using var glyphRegion = new Mat(window, new Rect(run.Start, 0, run.Width, window.Height));
        using var rowProjection = new Mat();
        Cv2.Reduce(glyphRegion, rowProjection, ReduceDimension.Column, ReduceTypes.Sum, MatType.CV_32S);

        var top = -1;
        var bottom = -1;
        var pixelCount = 0;

        for (var y = 0; y < window.Height; y++)
        {
            var rowPixelCount = rowProjection.At<int>(y, 0) / BinaryMaskMaxValue;
            if (rowPixelCount == 0)
            {
                continue;
            }

            top = top < 0 ? y : top;
            bottom = y;
            pixelCount += rowPixelCount;
        }

        var bounds = top < 0
            ? new Rect(run.Start, windowTop, run.Width, 1)
            : new Rect(run.Start, windowTop + top, run.Width, bottom - top + 1);
        return new GlyphRun(bounds, pixelCount);
    }

    private static bool IsPercentGlyph(GlyphRun run)
    {
        return run.Bounds.Width is >= MinimumPercentGlyphWidth and <= MaximumPercentGlyphWidth &&
               run.Bounds.Height >= MinimumDigitGlyphHeight;
    }

    private static bool IsDecimalPointGlyph(GlyphRun run, int textBottom)
    {
        return run.Bounds is { Width: <= MaximumDecimalPointWidth, Height: <= MaximumDecimalPointHeight } &&
               run.Bounds.Bottom >= textBottom - DecimalPointVerticalTolerance;
    }

    private static Mat BuildTextMask(Mat searchRegion)
    {
        var channels = Cv2.Split(searchRegion);
        try
        {
            using var firstMax = new Mat();
            using var maxChannel = new Mat();
            using var firstMin = new Mat();
            using var minChannel = new Mat();
            using var spread = new Mat();
            using var maxMask = new Mat();
            using var gray = new Mat();
            using var grayMask = new Mat();
            using var spreadMask = new Mat();
            var mask = new Mat();

            Cv2.Max(channels[0], channels[1], firstMax);
            Cv2.Max(firstMax, channels[2], maxChannel);
            Cv2.Min(channels[0], channels[1], firstMin);
            Cv2.Min(firstMin, channels[2], minChannel);
            Cv2.Subtract(maxChannel, minChannel, spread);
            Cv2.Threshold(maxChannel, maxMask, MinimumMaximumChannel, BinaryMaskMaxValue, ThresholdTypes.Binary);
            Cv2.CvtColor(searchRegion, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.Threshold(gray, grayMask, MinimumGrayValue, BinaryMaskMaxValue, ThresholdTypes.Binary);
            Cv2.Threshold(spread, spreadMask, MaximumNeutralChannelSpread, BinaryMaskMaxValue, ThresholdTypes.BinaryInv);
            Cv2.BitwiseAnd(maxMask, grayMask, mask);
            Cv2.BitwiseAnd(mask, spreadMask, mask);
            return mask;
        }
        finally
        {
            foreach (var channel in channels)
            {
                channel.Dispose();
            }
        }
    }

    private static void RemoveHorizontalLines(Mat mask)
    {
        var maximumLinePixels = mask.Width * MaximumHorizontalLineCoveragePercent / 100;
        for (var rowIndex = 0; rowIndex < mask.Height; rowIndex++)
        {
            using var row = mask.Row(rowIndex);
            if (Cv2.CountNonZero(row) > maximumLinePixels)
            {
                row.SetTo(Scalar.Black);
            }
        }
    }

    private static Rect BuildSearchBounds(Size imageSize)
    {
        var x = Math.Clamp(SearchBounds.X, 0, Math.Max(0, imageSize.Width - 1));
        var y = Math.Clamp(SearchBounds.Y, 0, Math.Max(0, imageSize.Height - 1));
        var width = Math.Clamp(SearchBounds.Width, 1, imageSize.Width - x);
        var height = Math.Clamp(SearchBounds.Height, 1, imageSize.Height - y);
        return new Rect(x, y, width, height);
    }

    private static Rect UnionBounds(IEnumerable<Rect> bounds)
    {
        using var enumerator = bounds.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return new Rect();
        }

        var left = enumerator.Current.Left;
        var top = enumerator.Current.Top;
        var right = enumerator.Current.Right;
        var bottom = enumerator.Current.Bottom;
        while (enumerator.MoveNext())
        {
            left = Math.Min(left, enumerator.Current.Left);
            top = Math.Min(top, enumerator.Current.Top);
            right = Math.Max(right, enumerator.Current.Right);
            bottom = Math.Max(bottom, enumerator.Current.Bottom);
        }

        return new Rect(left, top, right - left, bottom - top);
    }

    private readonly record struct ColumnRun(int Start, int End)
    {
        public int Width => End - Start + 1;
    }

    private readonly record struct GlyphRun(Rect Bounds, int PixelCount);

    private readonly record struct TextCandidate(Rect Bounds, double Percentage, string Text, double Score);

    private readonly record struct DigitFeatures(
        int Width,
        double Top,
        double Middle,
        double Bottom,
        double UpperLeft,
        double UpperRight,
        double LowerLeft,
        double LowerRight)
    {
        public static DigitFeatures From(Mat glyphMask)
        {
            return new DigitFeatures(
                glyphMask.Width,
                Density(glyphMask, 0.20, 0.80, 0.00, 0.22),
                Density(glyphMask, 0.20, 0.80, 0.39, 0.61),
                Density(glyphMask, 0.20, 0.80, 0.78, 1.00),
                Density(glyphMask, 0.00, 0.35, 0.12, 0.45),
                Density(glyphMask, 0.65, 1.00, 0.12, 0.45),
                Density(glyphMask, 0.00, 0.35, 0.55, 0.88),
                Density(glyphMask, 0.65, 1.00, 0.55, 0.88));
        }

        private static double Density(Mat glyphMask, double leftRatio, double rightRatio, double topRatio, double bottomRatio)
        {
            var bounds = BuildZone(glyphMask.Size(), leftRatio, rightRatio, topRatio, bottomRatio);
            using var zone = new Mat(glyphMask, bounds);
            return Cv2.CountNonZero(zone) / (double)(bounds.Width * bounds.Height);
        }

        private static Rect BuildZone(Size size, double leftRatio, double rightRatio, double topRatio, double bottomRatio)
        {
            var left = Math.Clamp((int)Math.Floor(size.Width * leftRatio), 0, Math.Max(0, size.Width - 1));
            var top = Math.Clamp((int)Math.Floor(size.Height * topRatio), 0, Math.Max(0, size.Height - 1));
            var right = Math.Clamp((int)Math.Ceiling(size.Width * rightRatio), left + 1, size.Width);
            var bottom = Math.Clamp((int)Math.Ceiling(size.Height * bottomRatio), top + 1, size.Height);
            return new Rect(left, top, right - left, bottom - top);
        }
    }
}

internal sealed record AccuracyDetection(
    bool IsFound,
    Rect SearchBounds,
    Rect? TextBounds,
    double? Percentage,
    string Text)
{
    public static AccuracyDetection NotFound(Rect searchBounds) => new(
        false,
        searchBounds,
        null,
        null,
        string.Empty);
}
