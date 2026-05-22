using Automaton.Infrastructure;
using OpenCvSharp;

namespace Automaton.Detectors;

internal sealed class FirstAsteroidWithinReachDetector
{
    private const double MinimumMetersTemplateMatchScore = 0.82;
    private static readonly double[] TemplateScales = [1.0, 0.95, 1.05, 0.90, 1.10, 0.85, 1.15];
    private readonly Mat m_DistanceMetersTemplate = EmbeddedResourceLoader.LoadMat("overview.distance_m.png");

    public bool Detect(Mat screen, Rect mineOverviewBounds, Rect firstAsteroidRowBounds)
    {
        return Detect(screen, mineOverviewBounds, firstAsteroidRowBounds, out _);
    }

    public bool Detect(Mat screen, Rect mineOverviewBounds, Rect firstAsteroidRowBounds, out DistanceUnitDetectionTelemetry telemetry)
    {
        if (screen.Empty())
        {
            telemetry = new DistanceUnitDetectionTelemetry(
                null,
                null,
                0,
                0,
                false);
            return false;
        }

        var rowSearchBounds = BuildDistanceRowSearchBounds(screen.Size(), mineOverviewBounds, firstAsteroidRowBounds);
        var unitBounds = BuildDistanceUnitSearchBounds(screen.Size(), rowSearchBounds);
        using var region = new Mat(screen, unitBounds);
        if (region.Empty())
        {
            telemetry = new DistanceUnitDetectionTelemetry(
                rowSearchBounds,
                unitBounds,
                0,
                0,
                false);
            return false;
        }

        var isMetersByTemplate = TryMatchMeters(region, out var bestScore, out var matchedScale);

        telemetry = new DistanceUnitDetectionTelemetry(
            rowSearchBounds,
            unitBounds,
            bestScore,
            matchedScale,
            isMetersByTemplate);
        return isMetersByTemplate;
    }

    private static Rect BuildDistanceRowSearchBounds(Size imageSize, Rect mineOverviewBounds, Rect firstAsteroidRowBounds)
    {
        var left = Math.Clamp(firstAsteroidRowBounds.Left, 0, Math.Max(0, imageSize.Width));
        var top = Math.Clamp(firstAsteroidRowBounds.Top, 0, Math.Max(0, imageSize.Height));
        var right = Math.Clamp(firstAsteroidRowBounds.Right, left, Math.Min(mineOverviewBounds.Right, imageSize.Width));
        var bottom = Math.Clamp(firstAsteroidRowBounds.Bottom, top, imageSize.Height);
        return new Rect(left, top, right - left, bottom - top);
    }

    private static Rect BuildDistanceUnitSearchBounds(Size imageSize, Rect rowSearchBounds)
    {
        var left = Math.Clamp(rowSearchBounds.Right - 35, 0, Math.Max(0, imageSize.Width));
        var top = Math.Clamp(rowSearchBounds.Top - 5, 0, Math.Max(0, imageSize.Height));
        var right = Math.Clamp(rowSearchBounds.Right + 5, left, imageSize.Width);
        var bottom = Math.Clamp(rowSearchBounds.Bottom + 5, top, imageSize.Height);
        return new Rect(left, top, right - left, bottom - top);
    }

    private bool TryMatchMeters(Mat searchRegion, out double bestScore, out double? matchedScale)
    {
        return TryMatchTemplate(
            searchRegion,
            m_DistanceMetersTemplate,
            MinimumMetersTemplateMatchScore,
            out bestScore,
            out matchedScale);
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
        using var searchEdges = new Mat();
        Cv2.Canny(searchGray, searchEdges, 45, 120);

        bestScore = 0;
        matchedScale = null;
        var fitScale = Math.Min(
            searchEdges.Width / (double)template.Width,
            searchEdges.Height / (double)template.Height);

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
            using var templateEdges = new Mat();
            Cv2.Canny(templateGray, templateEdges, 45, 120);
            if (templateEdges.Width > searchEdges.Width || templateEdges.Height > searchEdges.Height)
            {
                continue;
            }

            using var result = new Mat();
            Cv2.MatchTemplate(searchEdges, templateEdges, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out var score, out _, out _);
            if (score > bestScore)
            {
                bestScore = score;
                matchedScale = adjustedScale;
            }
        }

        return bestScore >= minimumScore;
    }

    private static Mat BuildScaledTemplate(Mat template, double scale)
    {
        if (Math.Abs(scale - 1.0) < double.Epsilon)
        {
            return template.Clone();
        }

        var width = Math.Max(1, (int)Math.Round(template.Width * scale));
        var height = Math.Max(1, (int)Math.Round(template.Height * scale));
        var scaledTemplate = new Mat();
        Cv2.Resize(template, scaledTemplate, new Size(width, height));
        return scaledTemplate;
    }

}

internal readonly record struct DistanceUnitDetectionTelemetry(
    Rect? RowSearchBounds,
    Rect? SearchBounds,
    double BestMetersScore,
    double? MatchedMetersScale,
    bool IsMetersTemplateMatch);
