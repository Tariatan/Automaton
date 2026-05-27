using Automaton.Infrastructure;
using OpenCvSharp;
using Serilog;

namespace Automaton.Detectors;

internal sealed class PlayNowButtonLocator
{
    private const double MinimumMatchScore = 0.82;
    private const double DebugOverlayTextScale = 0.8;
    private const int DebugOverlayTextThickness = 2;
    private const int DebugOverlayLeftPadding = 30;
    private const int DebugOverlayTopPadding = 40;
    private static readonly Scalar DebugOverlayColor = new(80, 120, 255);
    private static readonly double[] TemplateScales = [1.0, 0.95, 1.05, 0.90, 1.10];
    private static readonly ILogger Logger = Log.ForContext<PlayNowButtonLocator>();

    private readonly Mat m_Template = EmbeddedResourceLoader.LoadMat("play.png");

    public bool TryLocateAndDrawDebugOverlay(string imagePath, out PlayNowButtonLocation location)
    {
        using var image = Cv2.ImRead(imagePath);
        if (image.Empty())
        {
            location = default;
            return false;
        }

        var found = TryLocateCore(image, out location);

        DrawDebugOverlay(image, found, location);
        Cv2.ImWrite(imagePath, image);

        return found;
    }

    private bool TryLocateCore(Mat screen, out PlayNowButtonLocation location)
    {
        location = default;
        if (screen.Empty())
        {
            return false;
        }

        using var searchableScreen = BuildSearchableScreen(screen);
        using var searchableScreenGray = new Mat();
        Cv2.CvtColor(searchableScreen, searchableScreenGray, ColorConversionCodes.BGR2GRAY);
        PlayNowButtonLocation? bestLocation = null;
        foreach (var scale in TemplateScales)
        {
            using var scaledTemplate = BuildScaledTemplate(scale);
            using var scaledTemplateGray = new Mat();
            Cv2.CvtColor(scaledTemplate, scaledTemplateGray, ColorConversionCodes.BGR2GRAY);
            if (scaledTemplateGray.Width > searchableScreenGray.Width || scaledTemplateGray.Height > searchableScreenGray.Height)
            {
                continue;
            }

            using var result = new Mat();
            Cv2.MatchTemplate(searchableScreenGray, scaledTemplateGray, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out var score, out _, out var locationPoint);
            var bounds = new Rect(locationPoint.X, locationPoint.Y, scaledTemplateGray.Width, scaledTemplateGray.Height);
            if (bestLocation is null || score > bestLocation.Value.Score)
            {
                bestLocation = new PlayNowButtonLocation(bounds, score);
            }
        }

        if (bestLocation is null || bestLocation.Value.Score < MinimumMatchScore)
        {
            return false;
        }

        location = bestLocation.Value;
        return true;
    }

    private static void DrawDebugOverlay(Mat image, bool found, PlayNowButtonLocation location)
    {
        if (image.Empty())
        {
            return;
        }

        if (found)
        {
            Cv2.Rectangle(image, location.Bounds, DebugOverlayColor, 2);
        }

        Cv2.PutText(
            image,
            found ? "PLAY NOW found" : "PLAY NOW not found",
            new Point(DebugOverlayLeftPadding, DebugOverlayTopPadding),
            HersheyFonts.HersheySimplex,
            DebugOverlayTextScale,
            DebugOverlayColor,
            DebugOverlayTextThickness,
            LineTypes.AntiAlias);

        Logger.Information("PLAY NOW {Found}found", found ? "" : "not ");
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

    private Mat BuildScaledTemplate(double scale)
    {
        if (Math.Abs(scale - 1.0) < double.Epsilon)
        {
            return m_Template.Clone();
        }

        var width = Math.Max(1, (int)Math.Round(m_Template.Width * scale));
        var height = Math.Max(1, (int)Math.Round(m_Template.Height * scale));
        var scaledTemplate = new Mat();
        Cv2.Resize(m_Template, scaledTemplate, new Size(width, height));
        return scaledTemplate;
    }
}

internal readonly record struct PlayNowButtonLocation(Rect Bounds, double Score);
