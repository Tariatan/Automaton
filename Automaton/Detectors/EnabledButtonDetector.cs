using Automaton.Helpers;
using Automaton.Infrastructure;
using OpenCvSharp;

namespace Automaton.Detectors;

internal static class EnabledButtonDetector
{
    private const double MinimumTemplateMatchScore = 0.88;
    private const double EarlyExitScore = 0.90;
    private const double MaximumHsvDistance = 130.0;
    private static readonly double[] TemplateScales = [1.0, 0.95, 1.05, 0.90, 1.10];
    private static readonly Mat EnabledTemplate = EmbeddedResourceLoader.LoadMat("submit_enabled.png");
    private static readonly Mat EnabledTemplateHsv = ConvertToHsv(EnabledTemplate);

    public static EnabledButtonDetection Detect(Mat screen, Rect playfieldBounds)
    {
        if (screen.Empty())
        {
            return EnabledButtonDetection.NotFound;
        }

        var searchBounds = BuildSearchBounds(screen.Size(), playfieldBounds);
        using var searchRegion = new Mat(screen, searchBounds);

        var requestedMatch = MatchTemplateAcrossScales(searchRegion, EnabledTemplate, searchBounds.Location);
        if (requestedMatch is null)
        {
            return new EnabledButtonDetection(
                false,
                searchBounds,
                null,
                0.0,
                0.0);
        }

        var score = requestedMatch.Value.Score;
        var hsv = MeasureHsv(screen, requestedMatch.Value.Bounds);
        var isFound = score >= MinimumTemplateMatchScore && hsv <= MaximumHsvDistance;

        return new EnabledButtonDetection(
            isFound,
            searchBounds,
            requestedMatch.Value.Bounds,
            score,
            hsv);
    }

    private static TemplateMatch? MatchTemplateAcrossScales(Mat searchRegion, Mat template, Point searchOffset)
    {
        TemplateMatch? best = null;
        foreach (var scale in TemplateScales)
        {
            var ownsScaled = !GeometryHelper.IsUnscaled(scale);
            var candidateTemplate = ownsScaled ? BuildScaledTemplate(template, scale) : template;
            try
            {
                if (candidateTemplate.Width > searchRegion.Width || candidateTemplate.Height > searchRegion.Height)
                {
                    continue;
                }

                using var result = new Mat();
                Cv2.MatchTemplate(searchRegion, candidateTemplate, result, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(result, out _, out var score, out _, out var location);

                var bounds = new Rect(
                    searchOffset.X + location.X,
                    searchOffset.Y + location.Y,
                    candidateTemplate.Width,
                    candidateTemplate.Height);
                if (best is null || score > best.Value.Score)
                {
                    best = new TemplateMatch(bounds, score);
                }

                if (best.Value.Score >= EarlyExitScore)
                {
                    break;
                }
            }
            finally
            {
                if (ownsScaled)
                {
                    candidateTemplate.Dispose();
                }
            }
        }

        return best;
    }

    private static Mat BuildScaledTemplate(Mat template, double scale)
    {
        var width = Math.Max(1, (int)Math.Round(template.Width * scale));
        var height = Math.Max(1, (int)Math.Round(template.Height * scale));
        var scaled = new Mat();
        Cv2.Resize(template, scaled, new Size(width, height));
        return scaled;
    }

    private static Rect BuildSearchBounds(Size imageSize, Rect playfieldBounds)
    {
        var discoveryPanelBounds = BuildDiscoveryPanelBounds(playfieldBounds, imageSize);
        var left = discoveryPanelBounds.X + (int)Math.Round(discoveryPanelBounds.Width * 0.66);
        var top = discoveryPanelBounds.Y + (int)Math.Round(discoveryPanelBounds.Height * 0.75);
        var right = discoveryPanelBounds.Right;
        var bottom = discoveryPanelBounds.Bottom;

        var width = right - left;
        var horizontalPadding = (int)Math.Round(width * 0.10);
        left -= horizontalPadding;
        right += horizontalPadding;

        left = Math.Clamp(left, 0, Math.Max(0, imageSize.Width - 1));
        top = Math.Clamp(top, 0, Math.Max(0, imageSize.Height - 1));
        right = Math.Clamp(right, left + 1, imageSize.Width);
        bottom = Math.Clamp(bottom, top + 1, imageSize.Height);
        return new Rect(left, top, right - left, bottom - top);
    }

    private static Rect BuildDiscoveryPanelBounds(Rect playfieldBounds, Size imageSize)
    {
        var panelLeft = playfieldBounds.X;
        var panelTop = playfieldBounds.Y - (int)Math.Round(playfieldBounds.Height * 0.08);
        var panelRight = playfieldBounds.X + (int)Math.Round(playfieldBounds.Width * 2.45);
        var panelBottom = playfieldBounds.Y + (int)Math.Round(playfieldBounds.Height * 1.15);

        panelLeft = Math.Clamp(panelLeft, 0, Math.Max(0, imageSize.Width - 1));
        panelTop = Math.Clamp(panelTop, 0, Math.Max(0, imageSize.Height - 1));
        panelRight = Math.Clamp(panelRight, panelLeft + 1, imageSize.Width);
        panelBottom = Math.Clamp(panelBottom, panelTop + 1, imageSize.Height);
        return new Rect(panelLeft, panelTop, panelRight - panelLeft, panelBottom - panelTop);
    }

    private static double MeasureHsv(Mat screen, Rect buttonBounds)
    {
        var sampleBounds = BuildColorSampleBounds(buttonBounds, screen.Size());
        if (sampleBounds.Width <= 0 || sampleBounds.Height <= 0)
        {
            return 0.0;
        }

        using var buttonRegion = new Mat(screen, sampleBounds);
        return ComputeHsv(buttonRegion, EnabledTemplateHsv);
    }

    private static Rect BuildColorSampleBounds(Rect bounds, Size imageSize)
    {
        var insetX = Math.Max(2, (int)Math.Round(bounds.Width * 0.12));
        var insetY = Math.Max(2, (int)Math.Round(bounds.Height * 0.20));
        var left = bounds.X + insetX;
        var top = bounds.Y + insetY;
        var right = bounds.Right - insetX;
        var bottom = bounds.Bottom - insetY;

        left = Math.Clamp(left, 0, Math.Max(0, imageSize.Width - 1));
        top = Math.Clamp(top, 0, Math.Max(0, imageSize.Height - 1));
        right = Math.Clamp(right, left + 1, imageSize.Width);
        bottom = Math.Clamp(bottom, top + 1, imageSize.Height);
        return new Rect(left, top, right - left, bottom - top);
    }

    private static double ComputeHsv(Mat sourceBgr, Mat templateHsv)
    {
        using var resizedSource = new Mat();
        Cv2.Resize(sourceBgr, resizedSource, templateHsv.Size(), 0, 0, InterpolationFlags.Area);
        using var sourceHsv = new Mat();
        Cv2.CvtColor(resizedSource, sourceHsv, ColorConversionCodes.BGR2HSV);
        using var diff = new Mat();
        Cv2.Absdiff(sourceHsv, templateHsv, diff);
        var mean = Cv2.Mean(diff);

        // Prioritize saturation/value over hue (hue wraps and is noisy in dark regions).
        return mean.Val1 * 1.8 + mean.Val2 * 1.2 + mean.Val0 * 0.3;
    }

    private static Mat ConvertToHsv(Mat bgr)
    {
        var hsv = new Mat();
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);
        return hsv;
    }

    private readonly record struct TemplateMatch(Rect Bounds, double Score);
}

internal sealed record EnabledButtonDetection(
    bool IsFound,
    Rect SearchBounds,
    Rect? ButtonBounds,
    double Score,
    double HsvDistance)
{
    public static EnabledButtonDetection NotFound { get; } = new(
        false,
        new Rect(0, 0, 1, 1),
        null,
        0.0,
        0.0);
}
