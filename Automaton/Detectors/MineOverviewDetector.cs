using Automaton.Infrastructure;
using Automaton.Primitives;
using OpenCvSharp;
using Serilog;

namespace Automaton.Detectors;

internal sealed class MineOverviewDetector
{
    private const double MinimumHeaderMatchScore = 0.90;
    private const double DebugOverlayTextScale = 0.8;
    private const int DebugOverlayTextThickness = 2;
    private const int DebugOverlayLeftPadding = 30;
    private const int DebugOverlayTopPadding = 40;
    private static readonly double[] TemplateScales = [1.0, 0.95, 1.05];
    private static readonly Scalar DebugOverlayColor = new(80, 120, 255);
    private static readonly Scalar SearchBoundsColor = new(255, 200, 120);
    private static readonly Scalar HeaderBoundsColor = new(120, 255, 120);
    private static readonly Scalar MineOverviewBoundsColor = new(120, 220, 255);
    private static readonly ILogger Logger = Log.ForContext<MineOverviewDetector>();

    private readonly Mat m_OverviewMineTemplate = EmbeddedResourceLoader.LoadMat("overview.overview_mine.png");

    public MineOverviewAnalysis AnalyzeAndDrawDebugOverlay(string imagePath)
    {
        using var screen = Cv2.ImRead(imagePath);
        if (screen.Empty())
        {
            return MineOverviewAnalysis.NotFound;
        }

        var analysis = AnalyzeCore(screen);
        DrawDebugOverlay(screen, analysis);
        Cv2.ImWrite(imagePath, screen);
        return analysis;
    }

    private MineOverviewAnalysis AnalyzeCore(Mat screen)
    {
        var searchBounds = BuildFallbackSearchBounds(screen.Size());
        if (!TryMatchTemplate(screen, m_OverviewMineTemplate, searchBounds, out var headerLocation))
        {
            return new MineOverviewAnalysis(false, searchBounds, null, null, 0);
        }

        var mineOverviewBounds = BuildMineOverviewBounds(screen.Size(), headerLocation.Bounds);
        return new MineOverviewAnalysis(true, searchBounds, headerLocation.Bounds, mineOverviewBounds, headerLocation.Score);
    }

    private static void DrawDebugOverlay(Mat image, MineOverviewAnalysis analysis)
    {
        if (image.Empty())
        {
            return;
        }

        Cv2.Rectangle(image, analysis.SearchBounds, SearchBoundsColor, 2);
        if (analysis.HeaderBounds is not null)
        {
            Cv2.Rectangle(image, analysis.HeaderBounds.Value, HeaderBoundsColor, 2);
        }

        if (analysis.MineOverviewBounds is not null)
        {
            Cv2.Rectangle(image, analysis.MineOverviewBounds.Value, MineOverviewBoundsColor, 2);
        }

        Cv2.PutText(
            image,
            analysis.MineOverviewLocated ? "MINE overview found" : "MINE overview not found",
            new Point(DebugOverlayLeftPadding, DebugOverlayTopPadding),
            HersheyFonts.HersheySimplex,
            DebugOverlayTextScale,
            DebugOverlayColor,
            DebugOverlayTextThickness,
            LineTypes.AntiAlias);

        if (analysis.MineOverviewLocated)
        {
            Logger.Information("Mine overview located" );
        }
        else
        {
            Logger.Error("Failed to detect Mine overview");
        }
    }

    private static bool TryMatchTemplate(Mat screen, Mat template, Rect searchBounds, out TemplateLocation location)
    {
        location = default;
        using var searchRegion = new Mat(screen, searchBounds);
        TemplateLocation? bestLocation = null;
        foreach (var scale in TemplateScales)
        {
            using var scaledTemplate = BuildScaledTemplate(template, scale);
            if (scaledTemplate.Width > searchRegion.Width ||
                scaledTemplate.Height > searchRegion.Height)
            {
                continue;
            }

            using var result = new Mat();
            Cv2.MatchTemplate(searchRegion, scaledTemplate, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out var score, out _, out var locationPoint);
            var bounds = new Rect(
                searchBounds.X + locationPoint.X,
                searchBounds.Y + locationPoint.Y,
                scaledTemplate.Width,
                scaledTemplate.Height);
            if (bestLocation is null || score > bestLocation.Value.Score)
            {
                bestLocation = new TemplateLocation(bounds, score);
            }
        }

        if (bestLocation is null || bestLocation.Value.Score < MinimumHeaderMatchScore)
        {
            return false;
        }

        location = bestLocation.Value;
        return true;
    }

    private static Rect BuildMineOverviewBounds(Size imageSize, Rect overviewMineHeaderBounds)
    {
        var left = Math.Clamp(overviewMineHeaderBounds.X - 10, 0, imageSize.Width);
        var top = Math.Clamp(overviewMineHeaderBounds.Y - 10, 0, imageSize.Height);
        var right = Math.Clamp(left + Settings.MineOverviewWidth, left, imageSize.Width);
        var bottom = Math.Clamp(top + Settings.MineOverviewHeight, top, imageSize.Height);
        return new Rect(left, top, right - left, bottom - top);
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

    private static Rect BuildFallbackSearchBounds(Size imageSize)
    {
        return BuildRelativeBounds(imageSize, 0.62, 0.52, 0.35, 0.45);
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

    private readonly record struct TemplateLocation(Rect Bounds, double Score);
}

internal sealed record MineOverviewAnalysis(
    bool MineOverviewLocated,
    Rect SearchBounds,
    Rect? HeaderBounds,
    Rect? MineOverviewBounds,
    double BestMatchScore)
{
    public static MineOverviewAnalysis NotFound { get; } = new(false, new Rect(0, 0, 1, 1), null, null, 0);
}
