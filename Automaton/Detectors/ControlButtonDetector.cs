using Automaton.Helpers;
using Automaton.Infrastructure;
using OpenCvSharp;

namespace Automaton.Detectors;

internal enum ControlButtonState
{
    Disabled,
    Enabled
}

internal sealed class ControlButtonDetector
{
    private const double MinimumTemplateMatchScore = 0.88;
    private const double EarlyExitScore = 0.90;
    private const double MinimumDisabledHsvDistanceForEnabled = 80.0;
    private const double MaximumDisabledHsvDistanceForDisabled = 80.0;
    private static readonly double[] TemplateScales = [1.0, 0.95, 1.05, 0.90, 1.10];
    private static readonly Mat EnabledTemplate = EmbeddedResourceLoader.LoadMat("submit_enabled.png");
    private static readonly Mat DisabledTemplate = EmbeddedResourceLoader.LoadMat("submit_disabled.png");

    public ControlButtonDetection Detect(Mat screen, Rect playfieldBounds, ControlButtonState state)
    {
        if (screen.Empty())
        {
            return ControlButtonDetection.NotFound;
        }

        var searchBounds = BuildSearchBounds(screen.Size(), playfieldBounds);
        using var searchRegion = new Mat(screen, searchBounds);

        var requestedTemplate = state == ControlButtonState.Enabled ? EnabledTemplate : DisabledTemplate;

        var requestedMatch = MatchTemplateAcrossScales(searchRegion, requestedTemplate, searchBounds.Location);
        if (requestedMatch is null)
        {
            return new ControlButtonDetection(
                false,
                searchBounds,
                null,
                0.0,
                state,
                0.0);
        }

        var requestedScore = requestedMatch.Value.Score;
        var colorState = MeasureHsvDistances(screen, requestedMatch.Value.Bounds);
        var hsvStatePass = state == ControlButtonState.Enabled
            ? colorState.DisabledDistance >= MinimumDisabledHsvDistanceForEnabled
            : colorState.DisabledDistance <= MaximumDisabledHsvDistanceForDisabled;
        var isFound = requestedScore >= MinimumTemplateMatchScore && hsvStatePass;

        return new ControlButtonDetection(
            isFound,
            searchBounds,
            requestedMatch.Value.Bounds,
            requestedScore,
            state,
            colorState.DisabledDistance);
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
        // Derive full Project Discovery panel from chart-local playfield bounds,
        // then search in right 30% and lower 25% of that full panel.
        var discoveryPanelBounds = BuildDiscoveryPanelBounds(playfieldBounds, imageSize);
        var left = discoveryPanelBounds.X + (int)Math.Round(discoveryPanelBounds.Width * 0.66);
        var top = discoveryPanelBounds.Y + (int)Math.Round(discoveryPanelBounds.Height * 0.75);
        var right = discoveryPanelBounds.Right;
        var bottom = discoveryPanelBounds.Bottom;

        // Keep the same center position and make the ROI 20% wider in total.
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
        // The full Discovery panel extends well to the right of the chart area
        // (cards + instructions + submit region), with similar top alignment.
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

    private static ControlButtonColorMetrics MeasureHsvDistances(Mat screen, Rect buttonBounds)
    {
        var sampleBounds = BuildColorSampleBounds(buttonBounds, screen.Size());
        if (sampleBounds.Width <= 0 || sampleBounds.Height <= 0)
        {
            return new ControlButtonColorMetrics(0.0, 0.0);
        }

        using var buttonRegion = new Mat(screen, sampleBounds);
        var enabledDistance = ComputeHsvDistance(buttonRegion, EnabledTemplate);
        var disabledDistance = ComputeHsvDistance(buttonRegion, DisabledTemplate);
        return new ControlButtonColorMetrics(enabledDistance, disabledDistance);
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

    private static double ComputeHsvDistance(Mat sourceBgr, Mat templateBgr)
    {
        using var resizedSource = new Mat();
        Cv2.Resize(sourceBgr, resizedSource, templateBgr.Size(), 0, 0, InterpolationFlags.Area);
        using var sourceHsv = new Mat();
        using var templateHsv = new Mat();
        Cv2.CvtColor(resizedSource, sourceHsv, ColorConversionCodes.BGR2HSV);
        Cv2.CvtColor(templateBgr, templateHsv, ColorConversionCodes.BGR2HSV);
        using var diff = new Mat();
        Cv2.Absdiff(sourceHsv, templateHsv, diff);
        var mean = Cv2.Mean(diff);

        // Hue has wrap-around behavior and is less stable near dark values, so
        // prioritize saturation/value deltas for enabled-vs-disabled distinction.
        return mean.Val1 * 1.8 + mean.Val2 * 1.2 + mean.Val0 * 0.3;
    }

    private readonly record struct TemplateMatch(Rect Bounds, double Score);
    private readonly record struct ControlButtonColorMetrics(double EnabledDistance, double DisabledDistance);
}

internal sealed record ControlButtonDetection(
    bool IsFound,
    Rect SearchBounds,
    Rect? ButtonBounds,
    double Score,
    ControlButtonState TargetState,
    double DisabledHsvDistance)
{
    public static ControlButtonDetection NotFound { get; } = new(
        false,
        new Rect(0, 0, 1, 1),
        null,
        0.0,
        ControlButtonState.Enabled,
        0.0);
}
