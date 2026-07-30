using System.IO;
using Automaton.Detectors;
using OpenCvSharp;
using Serilog;

namespace Automaton.ImageAnalysis;

internal sealed partial class SampleImageProcessor(
    PlayfieldDetector playfieldDetector,
    KnownSampleMatcher? knownSampleMatcher)
{
    private static class MaskParams
    {
        public const int SaturationThreshold = 45;
        public const int BrightnessThreshold = 55;
        public const int BinaryMaskMaxValue = 255;
        public const int CandidateOpenKernelSize = 2;
        public const int CandidateRefineCloseKernelSize = 5;
        public const int ClusterBlurSigma = 20;
        public const int ClusterDilateKernelSize = 15;
        public const int ClusterThreshold = 10;
        public const int ClusterCloseKernelSize = 31;
        public const int ClusterOpenKernelSize = 5;
        public const int MinimumClusterArea = 900;
    }

    private static class RecoveryParams
    {
        public const int MinimumCandidatePoints = 300;
        public const int MinimumGapBelowPrimaryCluster = 12;
        public const int BlurSigma = 14;
        public const int DilateKernelSize = 15;
        public const int Threshold = 4;
        public const int MinimumContourArea = 1200;
    }

    private static class SplitParams
    {
        public const int MinimumContourArea = 12000;
        public const int MinimumRefinedComponentArea = 450;
        public const int MinimumRefinedComponentBoundingArea = 15_000;
        public const int MinimumSegmentHeight = 70;
        public const int MinimumSegmentWidth = 70;
        public const int MinimumContourHeightForSideBySideSplit = 140;
        public const int MinimumPointCount = 180;
        public const double MinimumAspectRatio = 0.55;
        public const int HistogramSmoothingRadius = 6;
        public const double MaximumValleyRatio = 0.72;
        public const int MinimumPeakDensity = 10;
        public const int DensitySeedBlurSigma = 12;
        public const double DensitySeedThresholdRatio = 0.42;
        public const int DensitySeedMinimumContourArea = 180;
        public const int DensitySeedMinimumCentroidDistance = 70;
        public const int MaximumDensitySeedCount = 4;
        public const int MaximumPointClusterCount = 3;
        public const int PointClusterAttempts = 5;
        public const int PointClusterMinimumCentroidDistance = 90;
        public const double PointClusterMinimumSeparationRatio = 1.20;
    }

    private static class PolygonParams
    {
        public const int SeparationPixels = 2;
        public const double MinimumNeighboringPointSpacing = 30.0;
        public const double MinimumInterPolygonPointSpacing = 15.0;
        public const int MaximumPointSpacingResolutionPasses = 15;
        public const int MaximumPerSession = 8;
        public const int MaximumPoints = 10;
        public const int MinimumBoundingArea = 35_000;
        public const double BalloonExpansionScale = 0.08;
        public const int MinimumBalloonExpansion = 3;
        public const int MaximumBalloonExpansion = 14;
        public const double MaskPaddingScale = 0.08;
        public const int MinimumMaskPadding = 6;
        public const int MaximumMaskPadding = 18;
        public const int MaskCloseKernelSize = 7;
        public const int PointCloudSeedRadius = 2;
        public const int PointCloudMargin = 6;
        public const double MinimumOverlapArea = 1.0;
        public const int MaximumCollisionResolutionPasses = 6;
        public const int MaximumSiblingMergeGap = 16;
        public const double MinimumSiblingAxisOverlapRatio = 0.70;
        public const double MaximumSiblingAreaRatio = 0.55;
        public const double TopMarkerBandCentroidScale = 1.5;
        public const double RandomizedPointRatio = 0.90;
        public const int MinimumRandomizedPointDistance = 10;
        public const int MaximumRandomizedPointDistance = 35;
    }

    private static readonly ILogger Logger = Log.ForContext<SampleImageProcessor>();

    private readonly KnownSampleMatcher m_KnownSampleMatcher = knownSampleMatcher ?? new KnownSampleMatcher(playfieldDetector);

    public SampleImageProcessor()
        : this(new PlayfieldDetector(), null)
    {
    }

    internal SampleImageAnalysisResult AnalyzeImageFile(string imagePath)
    {
        using var image = Cv2.ImRead(imagePath);
        if (image.Empty())
        {
            Logger.Error("Could not read image. ImagePath={ImagePath}", imagePath);
            throw new InvalidOperationException($"Could not read image: {imagePath}");
        }

        return AnalyzeImage(image, imagePath);
    }

    internal static IReadOnlyList<string> EnumerateSampleImageFiles(string samplesDirectory)
    {
        return
        [
            .. Directory
                .EnumerateFiles(samplesDirectory, "*.sample.png", SearchOption.TopDirectoryOnly)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
        ];
    }

    internal SampleImageAnalysisResult AnalyzeImage(Mat image, string imagePath)
    {
        using var analysisRegion = SampleImageProcessor.CropAnalysisRegion(image);
        var playfieldDetection = playfieldDetector.Detect(analysisRegion);
        IReadOnlyList<Point[]> polygons;
        var usedKnownSampleTemplate = false;
        string? matchedSampleFileName = null;

        if (!playfieldDetection.IsFound)
        {
            polygons = BuildDefaultFallbackPolygons();
        }
        else
        {
            using var playfieldImage = new Mat(analysisRegion, playfieldDetection.Bounds);
            // Try looking for existing template first
            if (m_KnownSampleMatcher.TryMatch(playfieldImage, imagePath, out var matchedPolygons, out matchedSampleFileName))
            {
                usedKnownSampleTemplate = true;
                polygons = [.. matchedPolygons.Select(points => SampleImageProcessor.TranslatePolygon(points, playfieldDetection.Bounds))];
            }
            else
            {
                // Otherwise try manual polygons detection
                var (candidateMask, candidateDensityMap) = SampleImageProcessor.BuildCandidateMaskAndDensityMap(playfieldImage);
                using var mask = candidateMask;
                using var densityMap = candidateDensityMap;
                using var clusterMask = SampleImageProcessor.BuildClusterMask(candidateMask);

                polygons = SampleImageProcessor.BuildClusterPolygons(candidateMask, candidateDensityMap, clusterMask, playfieldDetection.Bounds);
                if (polygons.Count == 0)
                {
                    // As a last resort, build fallback polygons - two big horizontal blobs one under the other
                    polygons = BuildDefaultFallbackPolygons(playfieldDetection.Bounds);
                }
            }

            var mutablePolygons = polygons.ToList();
            SampleImageProcessor.RandomizePolygons(mutablePolygons);
            SampleImageProcessor.FinalizeDetectedPolygons(mutablePolygons, playfieldDetection.MarkerBounds);
            polygons = [.. mutablePolygons];
        }

        var result = new SampleProcessingResult(
            Path.GetFileName(imagePath),
            playfieldDetection.IsFound,
            polygons.Count,
            imagePath);
        Logger.Information(
            "Analyzed image. PlayfieldFound={PlayfieldFound}, ClusterCount={ClusterCount}, UsedKnownSampleTemplate={UsedKnownSampleTemplate}, MatchedSampleFileName={MatchedSampleFileName}",
            result.PlayfieldFound,
            result.ClusterCount,
            usedKnownSampleTemplate,
            matchedSampleFileName);

        return new SampleImageAnalysisResult(result, playfieldDetection, polygons, usedKnownSampleTemplate, matchedSampleFileName);
    }

    private Point[][] BuildDefaultFallbackPolygons(Rect targetPlayfield)
    {
        if (!m_KnownSampleMatcher.TryLoadDefaultFallbackPolygons(out var fallbackPolygons, out var sourcePlayfieldSize) ||
            sourcePlayfieldSize.Width <= 0 ||
            sourcePlayfieldSize.Height <= 0)
        {
            return [];
        }

        var scaleX = targetPlayfield.Width / (double)sourcePlayfieldSize.Width;
        var scaleY = targetPlayfield.Height / (double)sourcePlayfieldSize.Height;
        return
        [
            .. fallbackPolygons
                .Select(polygon => polygon
                    .Select(point => new Point(
                        targetPlayfield.X + (int)Math.Round(point.X * scaleX),
                        targetPlayfield.Y + (int)Math.Round(point.Y * scaleY)))
                    .ToArray())
        ];
    }

    private IReadOnlyList<Point[]> BuildDefaultFallbackPolygons()
    {
        return m_KnownSampleMatcher.TryLoadDefaultFallbackScreenPolygons(out var fallbackPolygons)
            ? fallbackPolygons
            : [];
    }
}

internal sealed record SampleProcessingResult(
    string FileName,
    bool PlayfieldFound,
    int ClusterCount,
    string OutputPath);

internal sealed record SampleImageAnalysisResult(
    SampleProcessingResult Result,
    PlayfieldDetectionResult PlayfieldDetection,
    IReadOnlyList<Point[]> Polygons,
    bool UsedKnownSampleTemplate = false,
    string? MatchedSampleFileName = null);
