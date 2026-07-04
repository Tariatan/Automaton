using System.Drawing;
using System.IO;
using Automaton.Infrastructure;
using OpenCvSharp;
using Serilog;

namespace Automaton.Helpers;

internal sealed class ScreenCaptureService(
    IScreenCaptureProvider screenCaptureProvider,
    SampleImageProcessor sampleImageProcessor,
    ClickTraceRecorder? clickTraceRecorder = null,
    bool persistCaptures = true)
{
    private const string CaptureFilePrefix = "capture-";
    private const string CaptureTimestampFormat = "yyyyMMdd-HHmmss";
    private const int MinimumCaptureDimension = 1;
    private const int GameCaptureLeft = 0;
    private const int GameCaptureTop = 0;
    private const int GameCaptureWidth = 2_560;
    private const int GameCaptureHeight = 2_160;
    private const int CaptureAttemptCount = 2;
    private const int CaptureRetryDelayMilliseconds = 300;
    private const int VirtualScreenLeftMetric = 76;
    private const int VirtualScreenTopMetric = 77;
    private const int VirtualScreenWidthMetric = 78;
    private const int VirtualScreenHeightMetric = 79;
    private static readonly ILogger Logger = Log.ForContext<ScreenCaptureService>();

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
        var annotatedPath = WriteAnnotatedOutput(capture.Image, analysis, capture.CapturePath);
        var resultWithAnnotatedPath = analysis.Result with { OutputPath = annotatedPath };
        var analysisWithAnnotatedPath = analysis with { Result = resultWithAnnotatedPath };

        return new ScreenCaptureAnalysisSummary(capturesDirectory, capture.CapturePath, analysisWithAnnotatedPath);
    }

    internal ScreenCaptureResult CaptureCurrentScreen(string suffix = "")
    {
        var image = CaptureScreenWithRetry();
        var captureBounds = GetCurrentCaptureBounds(image);

        var capturesDirectory = TelemetryRootDirectory.GetCapturesDirectory();
        var capturePath = Path.Combine(
            capturesDirectory,
            $"{CaptureFilePrefix}{DateTime.Now.ToString(CaptureTimestampFormat)}{suffix}.png");

        if (persistCaptures)
        {
            Directory.CreateDirectory(capturesDirectory);
            Cv2.ImWrite(capturePath, image);
            Logger.Information("Captured current screen trace. CapturePath={CapturePath}", capturePath);
            clickTraceRecorder?.BeginCapture(capturePath, captureBounds);
        }

        return new ScreenCaptureResult(image, capturePath, captureBounds);
    }

    internal Mat CaptureCurrentScreenImage()
    {
        return CaptureScreenWithRetry();
    }

    internal void CaptureCurrentScreenToFile(string outputPath)
    {
        using var image = CaptureScreenWithRetry();
        var captureBounds = GetCurrentCaptureBounds(image);
        if (persistCaptures)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            Cv2.ImWrite(outputPath, image);
            Logger.Information("Captured current screen to file. OutputPath={OutputPath}", outputPath);
            clickTraceRecorder?.BeginCapture(outputPath, captureBounds);
        }
    }

    internal void FlushClickTrace()
    {
        clickTraceRecorder?.Flush();
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

    internal static string WriteAnnotatedOutput(Mat image, SampleImageAnalysisResult analysis, string sourceImagePath)
    {
        using var annotated = image.Clone();
        DebugOverlay.DrawPlayfieldOverlay(annotated, analysis.PlayfieldDetection, analysis.Polygons);

        var outputSuffix = analysis.UsedKnownSampleTemplate
            ? $".annotated.byexample{BuildMatchedExampleSuffix(analysis.MatchedSampleFileName)}.png"
            : ".annotated.png";
        var outputPath = Path.Combine(
            Path.GetDirectoryName(sourceImagePath)!,
            Path.GetFileNameWithoutExtension(sourceImagePath) + outputSuffix);
        Cv2.ImWrite(outputPath, annotated);
        return outputPath;
    }

    private static string BuildMatchedExampleSuffix(string? matchedSampleFileName)
    {
        if (string.IsNullOrWhiteSpace(matchedSampleFileName))
        {
            return string.Empty;
        }

        var sampleName = Path.GetFileNameWithoutExtension(matchedSampleFileName);
        var firstSegment = sampleName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(firstSegment)
            ? string.Empty
            : $".{firstSegment}";
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

    internal static Rectangle GetCurrentCaptureBounds()
    {
        return OperatingSystem.IsWindows()
            ? BuildGameCaptureBounds(GetPhysicalVirtualScreenBounds())
            : new Rectangle(0, 0, MinimumCaptureDimension, MinimumCaptureDimension);
    }

    private static Rectangle GetCurrentCaptureBounds(Mat capturedImage)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new Rectangle(0, 0, capturedImage.Width, capturedImage.Height);
        }

        var screenCaptureBounds = GetCurrentCaptureBounds();
        return screenCaptureBounds.Width == capturedImage.Width &&
               screenCaptureBounds.Height == capturedImage.Height
            ? screenCaptureBounds
            : new Rectangle(0, 0, capturedImage.Width, capturedImage.Height);
    }

    private static Rectangle GetPhysicalVirtualScreenBounds()
    {
        return new Rectangle(
            GetSystemMetrics(VirtualScreenLeftMetric),
            GetSystemMetrics(VirtualScreenTopMetric),
            Math.Max(MinimumCaptureDimension, GetSystemMetrics(VirtualScreenWidthMetric)),
            Math.Max(MinimumCaptureDimension, GetSystemMetrics(VirtualScreenHeightMetric)));
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetSystemMetrics")]
    private static extern int GetSystemMetrics(int nIndex);
}

internal sealed record ScreenCaptureResult(Mat Image, string CapturePath, Rectangle CaptureBounds) : IDisposable
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
