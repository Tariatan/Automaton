using System.Drawing;
using System.IO;
using Automaton.Infrastructure;
using OpenCvSharp;
using Serilog;

namespace Automaton.Helpers;

internal sealed class ScreenCaptureService(
    IScreenCaptureProvider screenCaptureProvider,
    SampleImageProcessor sampleImageProcessor,
    bool persistCaptures = true)
{
    private const string CaptureFilePrefix = "capture-";
    private const string CaptureTimestampFormat = "yyyyMMdd-HHmmss";
    internal const int MinimumCaptureDimension = 1;
    private const int GameCaptureLeft = 0;
    private const int GameCaptureTop = 0;
    private const int GameCaptureWidth = 2_560;
    private const int GameCaptureHeight = 2_160;
    private const int CaptureAttemptCount = 2;
    private const int CaptureRetryDelayMilliseconds = 100;
    internal const int VirtualScreenLeftMetric = 76;
    internal const int VirtualScreenTopMetric = 77;
    internal const int VirtualScreenWidthMetric = 78;
    internal const int VirtualScreenHeightMetric = 79;
    private static readonly ILogger Logger = Log.ForContext<ScreenCaptureService>();

    public void ProcessSamples()
    {
        var summary = sampleImageProcessor.ProcessSamples();
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
        var analysis = sampleImageProcessor.AnalyzeImage(capture.Image, capture.CapturePath);

        return new ScreenCaptureAnalysisSummary(capturesDirectory, capture.CapturePath, analysis);
    }

    internal ScreenCaptureResult CaptureCurrentScreen(string suffix = "")
    {
        var image = CaptureScreenWithRetry();

        var capturesDirectory = TelemetryRootDirectory.GetCapturesDirectory();
        var capturePath = Path.Combine(
            capturesDirectory,
            $"{CaptureFilePrefix}{DateTime.Now.ToString(CaptureTimestampFormat)}{suffix}.png");

        if (persistCaptures)
        {
            Directory.CreateDirectory(capturesDirectory);
            Cv2.ImWrite(capturePath, image);
            Logger.Information("Captured current screen trace. CapturePath={CapturePath}", capturePath);
        }

        return new ScreenCaptureResult(image, capturePath);
    }

    internal void CaptureCurrentScreenToFile(string outputPath)
    {
        using var image = CaptureScreenWithRetry();
        if (persistCaptures)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            Cv2.ImWrite(outputPath, image);
            Logger.Debug("Captured current screen to file. OutputPath={OutputPath}", outputPath);
        }
    }

    private Mat CaptureScreenWithRetry()
    {
        for (var attempt = 1; attempt <= CaptureAttemptCount; attempt++)
        {
            try
            {
                return screenCaptureProvider.CaptureScreen();
            }
            catch (Exception exception) when (IsScreenCaptureFailure(exception) && attempt < CaptureAttemptCount)
            {
                Thread.Sleep(CaptureRetryDelayMilliseconds);
            }
        }

        return new Mat(1, 1, MatType.CV_8UC3, Scalar.Black);
    }

    private static bool IsScreenCaptureFailure(Exception exception)
    {
        return exception is ArgumentException;
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
