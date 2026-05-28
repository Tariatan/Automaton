using Automaton.Infrastructure;
using OpenCvSharp;

namespace Automaton.Detectors;

internal class FirstAsteroidWithinReachDetector
{
    private const double MinimumMetersTemplateMatchScore = 0.95;
    private static readonly double[] TemplateScales = [1.0, 0.98, 1.02, 0.97, 1.03, 0.95, 1.05, 0.90, 1.10, 0.85, 1.15];
    private static readonly Scalar RowSearchBoundsColor = new(0, 255, 0);
    private static readonly Scalar UnitSearchBoundsColor = new(0, 255, 255);
    private readonly Mat m_DistanceMetersTemplate = EmbeddedResourceLoader.LoadMat("overview.distance_m.png");

    public virtual FirstAsteroidWithinReachAnalysis Detect(Mat screen, Rect mineOverviewBounds, Rect firstAsteroidRowBounds, bool drawDebugOverlay = true)
    {
        if (screen.Empty())
        {
            return FirstAsteroidWithinReachAnalysis.NotFound;
        }

        var rowSearchBounds = BuildDistanceRowSearchBounds(screen.Size(), mineOverviewBounds, firstAsteroidRowBounds);
        var unitBounds = BuildDistanceUnitSearchBounds(screen.Size(), rowSearchBounds);
        using var region = new Mat(screen, unitBounds);
        if (region.Empty())
        {
            return new FirstAsteroidWithinReachAnalysis(
                false,
                rowSearchBounds,
                unitBounds,
                0,
                null);
        }

        var isWithinReach = TryMatchTemplate(
            region,
            m_DistanceMetersTemplate,
            MinimumMetersTemplateMatchScore,
            out var bestScore,
            out var matchedScale);

        if (drawDebugOverlay)
        {
            Cv2.Rectangle(screen, rowSearchBounds, RowSearchBoundsColor, 2);
            Cv2.Rectangle(screen, unitBounds, UnitSearchBoundsColor, 2);
        }

        return new FirstAsteroidWithinReachAnalysis(
            isWithinReach,
            rowSearchBounds,
            unitBounds,
            bestScore,
            matchedScale);
    }

    private static Rect BuildDistanceRowSearchBounds(Size imageSize, Rect mineOverviewBounds, Rect firstAsteroidRowBounds)
    {
        var left = Math.Clamp(firstAsteroidRowBounds.Left, 0, imageSize.Width);
        var top = Math.Clamp(firstAsteroidRowBounds.Top, 0, imageSize.Height);
        var right = Math.Clamp(firstAsteroidRowBounds.Right, left, Math.Min(mineOverviewBounds.Right, imageSize.Width));
        var bottom = Math.Clamp(firstAsteroidRowBounds.Bottom, top, imageSize.Height);
        return new Rect(left, top, right - left, bottom - top);
    }

    private static Rect BuildDistanceUnitSearchBounds(Size imageSize, Rect rowSearchBounds)
    {
        var left = Math.Clamp(rowSearchBounds.Right - 50, 0, imageSize.Width);
        var top = Math.Clamp(rowSearchBounds.Top - 8, 0, imageSize.Height);
        var right = Math.Clamp(rowSearchBounds.Right + 10, left, imageSize.Width);
        var bottom = Math.Clamp(rowSearchBounds.Bottom + 8, top, imageSize.Height);
        return new Rect(left, top, right - left, bottom - top);
    }

    private static bool TryMatchTemplate(
        Mat searchRegion,
        Mat template,
        double minimumScore,
        out double bestScore,
        out double? matchedScale)
    {
        using var searchGray = new Mat();
        Cv2.CvtColor(searchRegion, searchGray, ColorConversionCodes.BGR2GRAY);

        bestScore = 0;
        matchedScale = null;
        var fitScale = Math.Min(
            searchGray.Width / (double)template.Width,
            searchGray.Height / (double)template.Height);

        foreach (var scale in TemplateScales)
        {
            var adjustedScale = Math.Min(scale, fitScale);
            if (adjustedScale <= 0)
            {
                continue;
            }

            using var scaledTemplate = BuildScaledTemplate(template, adjustedScale);
            using var templateGray = new Mat();
            Cv2.CvtColor(scaledTemplate, templateGray, ColorConversionCodes.BGR2GRAY);
            if (templateGray.Width > searchGray.Width || templateGray.Height > searchGray.Height)
            {
                continue;
            }

            using var grayscaleResult = new Mat();
            Cv2.MatchTemplate(searchGray, templateGray, grayscaleResult, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(grayscaleResult, out _, out var grayscaleScore, out _, out _);

            if (grayscaleScore >= minimumScore)
            {
                bestScore = grayscaleScore;
                matchedScale = adjustedScale;
                break;
            }

            if (grayscaleScore > bestScore)
            {
                bestScore = grayscaleScore;
                matchedScale = adjustedScale;
            }
        }

        return bestScore >= minimumScore;
    }

    private static Mat BuildScaledTemplate(Mat template, double scale)
    {
        if (Math.Abs(scale - 1.0) < double.Epsilon)
            return template.Clone();

        var width = Math.Max(1, (int)Math.Round(template.Width * scale));
        var height = Math.Max(1, (int)Math.Round(template.Height * scale));
        var scaledTemplate = new Mat();
        Cv2.Resize(template, scaledTemplate, new Size(width, height));
        return scaledTemplate;
    }
}

internal sealed record FirstAsteroidWithinReachAnalysis(
    bool IsWithinReach,
    Rect? RowSearchBounds,
    Rect? UnitSearchBounds,
    double BestScore,
    double? MatchedScale)
{
    public static FirstAsteroidWithinReachAnalysis NotFound { get; } = new(
        false,
        null,
        null,
        0,
        null);
}
