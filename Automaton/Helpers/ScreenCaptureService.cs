using System.Drawing;
using System.IO;
using Automaton.Infrastructure;
using OpenCvSharp;
using Serilog;

namespace Automaton.Helpers;

internal sealed class ScreenCaptureService
{
    private const string CaptureFilePrefix = "capture-";
    private const string CaptureTimestampFormat = "yyyyMMdd-HHmmss";
    internal const int MinimumCaptureDimension = 1;
    private const int GameCaptureLeft = 0;
    private const int GameCaptureTop = 0;
    private const int GameCaptureWidth = 2_560;
    private const int GameCaptureHeight = 2_160;
    internal const int VirtualScreenLeftMetric = 76;
    internal const int VirtualScreenTopMetric = 77;
    internal const int VirtualScreenWidthMetric = 78;
    internal const int VirtualScreenHeightMetric = 79;
    private static readonly ILogger Logger = Log.ForContext<ScreenCaptureService>();

    private readonly IScreenCaptureProvider m_ScreenCaptureProvider;
    private readonly SampleImageProcessor m_SampleImageProcessor;
    private readonly bool m_PersistCaptures;

    internal ScreenCaptureService(
        IScreenCaptureProvider screenCaptureProvider,
        SampleImageProcessor sampleImageProcessor,
        bool persistCaptures = true)
    {
        m_ScreenCaptureProvider = screenCaptureProvider;
        m_SampleImageProcessor = sampleImageProcessor;
        m_PersistCaptures = persistCaptures;
    }

    public void ProcessSamples()
    {
        var summary = m_SampleImageProcessor.ProcessSamples();
        Logger.Information(
            "Processed samples from screen capture service. SamplesDirectory={SamplesDirectory}, ResultCount={ResultCount}",
            summary.SamplesDirectory,
            summary.Results.Count);
    }

    public ScreenCaptureSummary CaptureAndProcessCurrentScreen()
    {
        var analysis = CaptureAndAnalyzeCurrentScreen();
        return new ScreenCaptureSummary(analysis.CapturesDirectory, analysis.CapturePath, analysis.Analysis.Result);
    }

    internal ScreenCaptureAnalysisSummary CaptureAndAnalyzeCurrentScreen()
    {
        var capturesDirectory = TelemetryRootDirectory.GetCapturesDirectory();
        using var capture = CaptureCurrentScreen();
        var analysis = m_SampleImageProcessor.AnalyzeImage(capture.Image, capture.CapturePath);
        Logger.Information(
            "Captured and analyzed current screen. CapturePath={CapturePath}, PlayfieldFound={PlayfieldFound}, ClusterCount={ClusterCount}, OutputPath={OutputPath}",
            capture.CapturePath,
            analysis.Result.PlayfieldFound,
            analysis.Result.ClusterCount,
            analysis.Result.OutputPath);

        return new ScreenCaptureAnalysisSummary(capturesDirectory, capture.CapturePath, analysis);
    }

    internal ScreenCaptureResult CaptureCurrentScreen(string suffix = "")
    {
        var image = m_ScreenCaptureProvider.CaptureScreen();
        var capturesDirectory = TelemetryRootDirectory.GetCapturesDirectory();
        var capturePath = Path.Combine(
            capturesDirectory,
            $"{CaptureFilePrefix}{DateTime.Now.ToString(CaptureTimestampFormat)}{suffix}.png");

        if (m_PersistCaptures)
        {
            Directory.CreateDirectory(capturesDirectory);
            Cv2.ImWrite(capturePath, image);
            Logger.Information("Captured current screen trace. CapturePath={CapturePath}", capturePath);
        }

        return new ScreenCaptureResult(image, capturePath);
    }

    internal string CaptureCurrentScreenTrace(string suffix = "")
    {
        using var capture = CaptureCurrentScreen(suffix);
        return capture.CapturePath;
    }

    internal void CaptureCurrentScreenToFile(string outputPath)
    {
        using var image = m_ScreenCaptureProvider.CaptureScreen();
        if (m_PersistCaptures)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            Cv2.ImWrite(outputPath, image);
            Logger.Debug("Captured current screen to file. OutputPath={OutputPath}", outputPath);
        }
    }

    internal SampleImageAnalysisResult AnalyzeImageFile(string imagePath, bool writeAnnotatedOutput = true)
    {
        return m_SampleImageProcessor.AnalyzeImageFile(imagePath, writeAnnotatedOutput);
    }

    internal static Rectangle BuildGameCaptureBounds(Rectangle virtualScreenBounds)
    {
        var gameBounds = new Rectangle(
            GameCaptureLeft,
            GameCaptureTop,
            GameCaptureWidth,
            GameCaptureHeight);
        var captureBounds = Rectangle.Intersect(virtualScreenBounds, gameBounds);
        if (captureBounds.Width < MinimumCaptureDimension ||
            captureBounds.Height < MinimumCaptureDimension)
        {
            return virtualScreenBounds;
        }

        return captureBounds;
    }

}

internal sealed record ScreenCaptureResult(Mat Image, string CapturePath) : IDisposable
{
    public void Dispose() => Image.Dispose();
}

internal sealed record ScreenCaptureSummary(
    string CapturesDirectory,
    string CapturePath,
    SampleProcessingResult Result);

internal sealed record ScreenCaptureAnalysisSummary(
    string CapturesDirectory,
    string CapturePath,
    SampleImageAnalysisResult Analysis);
