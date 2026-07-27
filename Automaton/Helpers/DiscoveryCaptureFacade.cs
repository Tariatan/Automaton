using System.IO;
using Automaton.Detectors;
using Automaton.Infrastructure;
using OpenCvSharp;
using Serilog;

namespace Automaton.Helpers;

internal sealed class DiscoveryCaptureFacade(
    ScreenCaptureService screenCaptureService,
    SampleImageProcessor sampleImageProcessor)
{
    private static readonly ILogger Logger = Log.ForContext<DiscoveryCaptureFacade>();

    internal ScreenCaptureResult CaptureCurrentScreen(string suffix = "") =>
        screenCaptureService.CaptureCurrentScreen(suffix);

    internal ScreenCaptureResult CaptureCurrentScreenInMemory(string suffix = "") =>
        screenCaptureService.CaptureCurrentScreenInMemory(suffix);

    internal void SaveCapture(ScreenCaptureResult capture) =>
        screenCaptureService.SaveCapture(capture);

    internal Mat CaptureCurrentScreenImage() =>
        screenCaptureService.CaptureCurrentScreenImage();

    internal void CaptureCurrentScreenToFile(string outputPath) =>
        screenCaptureService.CaptureCurrentScreenToFile(outputPath);

    internal void FlushClickTrace() =>
        screenCaptureService.FlushClickTrace();

    internal SampleImageAnalysisResult AnalyzeImage(Mat image, string imagePath) =>
        sampleImageProcessor.AnalyzeImage(image, imagePath);

    internal ScreenCaptureSummary CaptureAndProcessCurrentScreen()
    {
        var analysis = CaptureAndAnalyzeCurrentScreen();
        return new ScreenCaptureSummary(analysis.CapturesDirectory, analysis.CapturePath, analysis.Analysis.Result);
    }

    internal ScreenCaptureAnalysisSummary CaptureAndAnalyzeCurrentScreen()
    {
        var capturesDirectory = TelemetryRootDirectory.GetCapturesDirectory();
        using var capture = screenCaptureService.CaptureCurrentScreen();
        var analysis = sampleImageProcessor.AnalyzeImage(capture.Image, capture.CapturePath);
        var annotatedPath = WriteAnnotatedOutput(capture.Image, analysis, capture.CapturePath);
        var resultWithAnnotatedPath = analysis.Result with { OutputPath = annotatedPath };
        var analysisWithAnnotatedPath = analysis with { Result = resultWithAnnotatedPath };
        return new ScreenCaptureAnalysisSummary(capturesDirectory, capture.CapturePath, analysisWithAnnotatedPath);
    }

    internal static string WriteAnnotatedOutput(Mat image, SampleImageAnalysisResult analysis, string sourceImagePath)
    {
        using var annotated = image.Clone();
        DrawPlayfieldOverlay(annotated, analysis.PlayfieldDetection, analysis.Polygons);

        var outputSuffix = analysis.UsedKnownSampleTemplate
            ? $".annotated.byexample{BuildMatchedExampleSuffix(analysis.MatchedSampleFileName)}.png"
            : ".annotated.png";
        var outputPath = Path.Combine(
            Path.GetDirectoryName(sourceImagePath)!,
            Path.GetFileNameWithoutExtension(sourceImagePath) + outputSuffix);
        ImageFileWriter.WriteImage(outputPath, annotated);
        return outputPath;
    }

    private static void DrawPlayfieldOverlay(Mat image, PlayfieldDetectionResult playfieldDetection, IReadOnlyList<Point[]> polygons)
    {
        const int StrokeThickness = 2;
        const int PointRadius = 4;
        const double TextScale = 0.8;
        const int TextThickness = 2;
        const int LabelYOffset = 14;
        const int MinimumLabelY = 30;

        var palette = new[]
        {
            new Scalar(0, 255, 255),
            new Scalar(255, 180, 0),
            new Scalar(0, 220, 120),
            new Scalar(220, 120, 255),
            new Scalar(80, 180, 255),
            new Scalar(255, 120, 120)
        };
        var textOrigin = new Point(30, 40);

        if (playfieldDetection.IsFound)
        {
            Cv2.Rectangle(image, playfieldDetection.Bounds, new Scalar(70, 150, 255), StrokeThickness);
            foreach (var marker in playfieldDetection.MarkerBounds)
            {
                Cv2.Rectangle(image, marker, new Scalar(255, 120, 80), StrokeThickness);
            }
        }

        for (var index = 0; index < polygons.Count; index++)
        {
            var color = palette[index % palette.Length];
            Cv2.Polylines(image, [polygons[index]], true, color, StrokeThickness, LineTypes.AntiAlias);
            foreach (var point in polygons[index])
            {
                Cv2.Circle(image, point, PointRadius, color, -1, LineTypes.AntiAlias);
            }
        }

        Cv2.PutText(
            image,
            playfieldDetection.IsFound
                ? $"Playfield found, clusters: {polygons.Count}"
                : polygons.Count > 0
                    ? $"Playfield not found, using fallback: {polygons.Count}"
                    : "Playfield not found",
            new Point(
                playfieldDetection.IsFound ? playfieldDetection.Bounds.X : textOrigin.X,
                playfieldDetection.IsFound ? Math.Max(MinimumLabelY, playfieldDetection.Bounds.Y - LabelYOffset) : textOrigin.Y),
            HersheyFonts.HersheySimplex,
            TextScale,
            playfieldDetection.IsFound ? new Scalar(80, 220, 120) : new Scalar(80, 120, 255),
            TextThickness,
            LineTypes.AntiAlias);
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
}

internal sealed record ScreenCaptureSummary(
    string CapturesDirectory,
    string CapturePath,
    SampleProcessingResult Result);

internal sealed record ScreenCaptureAnalysisSummary(
    string CapturesDirectory,
    string CapturePath,
    SampleImageAnalysisResult Analysis);