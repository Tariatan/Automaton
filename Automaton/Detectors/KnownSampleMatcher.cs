using System.Collections.Concurrent;
using System.IO;
using Automaton.Infrastructure;
using OpenCvSharp;

namespace Automaton.Detectors;

internal sealed class KnownSampleMatcher(PlayfieldDetector playfieldDetector)
{
    private const string DefaultFallbackExampleName = "25.sample";
    private const string MaskedExpectedSuffix = ".expected.masked.png";
    private const int SignatureWidth = 96;
    private const int SignatureHeight = 96;
    private const double MaximumMatchScore = 4.0;
    private const int OverlayDifferenceThreshold = 20;
    private const int OverlayNoiseBorder = 20;
    private const int OverlayTopNoiseHeight = 60;
    private const int OverlayBottomNoiseHeight = 24;
    private const int OverlayMinimumContourArea = 400;
    private const int StrokeValueMinimum = 120;
    private const int StrokeSaturationMaximum = 120;
    private const int StrokeDifferenceThreshold = 18;
    private const int StrokeCloseKernelSize = 5;
    private const int BrownHueMinimum = 5;
    private const int BrownHueMaximum = 35;
    private const int BrownSaturationMinimum = 30;
    private const int BrownValueMinimum = 15;
    private const int BrownRedMinimum = 20;
    private const int BrownGreenMinimum = 10;
    private const int BrownBlueMaximum = 130;
    private const int BrownDominanceMinimum = 3;
    private const int OverlayOpenKernelSize = 3;
    private const int OverlayCloseKernelSize = 3;
    private const int OverlaySignedDeltaThreshold = 24;
    private const int OverlayValueGainThreshold = 12;
    private const int OverlayDifferenceFloor = 10;
    private const int FilteredOpenKernelSize = 3;
    private const int FilteredCloseKernelSize = 9;
    private const int VisibleOverlayCloseKernelSize = 13;
    private const int MaskedThreshold = 200;
    private const int MaskedOpenKernelSize = 7;
    private const int MaskedCloseKernelSize = 5;
    private const int MaskedNoiseBorder = 8;
    private const int MaskedMinimumComponentWidth = 30;
    private const int MaskedMinimumComponentHeight = 30;
    private const double MaskedMinimumFillRatio = 0.45;
    private const double MaskedMinimumHullRatio = 0.65;
    private const int MaximumPolygonPoints = 10;
    private const double MinimumSimplificationEpsilon = 3.0;
    private const double SimplificationEpsilonScale = 0.01;
    private const double SimplificationGrowthFactor = 1.35;
    private const int MaxSimplificationAttempts = 12;

    private static readonly ConcurrentDictionary<string, Lazy<IReadOnlyList<KnownSampleTemplate>>> TemplateCache = new(StringComparer.OrdinalIgnoreCase);

    public bool TryMatch(Mat playfieldImage, out IReadOnlyList<Point[]> polygons, out string? matchedSampleFileName)
    {
        return TryMatch(playfieldImage, null, out polygons, out matchedSampleFileName);
    }

    public bool TryMatch(
        Mat playfieldImage,
        string? sourceImagePath,
        out IReadOnlyList<Point[]> polygons,
        out string? matchedSampleFileName)
    {
        polygons = [];
        matchedSampleFileName = null;

        if (TryLoadAdjacentExpectedPolygons(sourceImagePath, out polygons, out matchedSampleFileName))
        {
            return true;
        }

        var templates = GetTemplateDirectories(sourceImagePath)
            .SelectMany(GetTemplates)
            .ToArray();
        if (templates.Length == 0)
        {
            return false;
        }

        using var signature = BuildSignature(playfieldImage);
        KnownSampleTemplate? bestTemplate = null;
        var bestScore = double.MaxValue;

        foreach (var template in templates)
        {
            using var difference = new Mat();
            Cv2.Absdiff(signature, template.Signature, difference);
            var score = Cv2.Mean(difference).Val0;
            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestTemplate = template;
        }

        if (bestTemplate is null || bestScore > MaximumMatchScore)
        {
            return false;
        }

        matchedSampleFileName = bestTemplate.FileName;
        polygons = bestTemplate.Polygons
            .Select(points => points.ToArray())
            .ToArray();
        return polygons.Count > 0;
    }

    private bool TryLoadAdjacentExpectedPolygons(
        string? sourceImagePath,
        out IReadOnlyList<Point[]> polygons,
        out string? matchedSampleFileName)
    {
        polygons = [];
        matchedSampleFileName = null;

        if (!TryGetAdjacentExpectedPaths(sourceImagePath, out var samplePath, out var expectedPath, out var maskedExpectedPath))
        {
            return false;
        }

        using var sampleImage = Cv2.ImRead(samplePath);
        using var expectedImage = Cv2.ImRead(expectedPath);
        if (sampleImage.Empty() || expectedImage.Empty())
        {
            return false;
        }

        var samplePlayfieldDetection = playfieldDetector.Detect(sampleImage);
        if (!samplePlayfieldDetection.IsFound)
        {
            return false;
        }

        var fallbackPolygons = LoadExpectedPolygons(sampleImage, expectedImage, maskedExpectedPath, samplePlayfieldDetection.Bounds);
        var expectedPlayfieldDetection = playfieldDetector.Detect(expectedImage);
        if (expectedPlayfieldDetection.IsFound)
        {
            var visiblePolygons = ExtractVisibleExpectedPolygons(
                sampleImage,
                expectedImage,
                samplePlayfieldDetection.Bounds,
                expectedPlayfieldDetection.Bounds);
            if (ShouldPreferVisibleExpectedPolygons(visiblePolygons, fallbackPolygons))
            {
                polygons = ScalePolygons(
                    visiblePolygons,
                    expectedPlayfieldDetection.Bounds.Size,
                    samplePlayfieldDetection.Bounds.Size);
                matchedSampleFileName = Path.GetFileName(samplePath);
                return true;
            }
        }

        polygons = fallbackPolygons;
        matchedSampleFileName = Path.GetFileName(samplePath);
        return polygons.Count > 0;
    }

    private static bool ShouldPreferVisibleExpectedPolygons(
        Point[][] visiblePolygons,
        Point[][] fallbackPolygons)
    {
        if (visiblePolygons.Length == 0)
        {
            return false;
        }

        if (fallbackPolygons.Length == 0)
        {
            return true;
        }

        if (fallbackPolygons.Length == 1 && visiblePolygons.Length > 1)
        {
            return true;
        }

        if (visiblePolygons.Length == fallbackPolygons.Length)
        {
            return true;
        }

        var visibleArea = SumPolygonArea(visiblePolygons);
        var fallbackArea = SumPolygonArea(fallbackPolygons);
        if (visibleArea <= double.Epsilon || fallbackArea <= double.Epsilon)
        {
            return false;
        }

        return visiblePolygons.Length <= fallbackPolygons.Length &&
               visibleArea < fallbackArea * 0.85;
    }

    private static double SumPolygonArea(IReadOnlyList<Point[]> polygons)
    {
        return polygons.Sum(polygon => Math.Abs(Cv2.ContourArea(polygon)));
    }

    private static List<string> GetTemplateDirectories(string? sourceImagePath)
    {
        var directories = new List<string>();
        if (TryGetAdjacentSamplesDirectory(sourceImagePath, out var adjacentSamplesDirectory))
        {
            AddTemplateDirectory(directories, adjacentSamplesDirectory);
        }

        AddTemplateDirectory(directories, TelemetryRootDirectory.GetExpectedDirectory());
        return directories;
    }

    private static bool TryGetAdjacentSamplesDirectory(string? sourceImagePath, out string samplesDirectory)
    {
        samplesDirectory = string.Empty;
        if (!TryGetAdjacentExpectedPaths(sourceImagePath, out var samplePath, out _, out _))
        {
            return false;
        }

        samplesDirectory = Path.GetDirectoryName(samplePath)!;
        return true;
    }

    private static bool TryGetAdjacentExpectedPaths(
        string? sourceImagePath,
        out string samplePath,
        out string expectedPath,
        out string maskedExpectedPath)
    {
        samplePath = string.Empty;
        expectedPath = string.Empty;
        maskedExpectedPath = string.Empty;

        if (string.IsNullOrWhiteSpace(sourceImagePath) ||
            !Path.GetFileName(sourceImagePath).EndsWith(".sample.png", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var directory = Path.GetDirectoryName(sourceImagePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        samplePath = sourceImagePath;
        expectedPath = Path.Combine(
            directory,
            Path.GetFileNameWithoutExtension(sourceImagePath) + ".expected.png");
        maskedExpectedPath = Path.Combine(
            directory,
            Path.GetFileNameWithoutExtension(sourceImagePath) + MaskedExpectedSuffix);
        if (File.Exists(expectedPath))
        {
            return true;
        }

        samplePath = string.Empty;
        expectedPath = string.Empty;
        maskedExpectedPath = string.Empty;
        return false;
    }

    private static void AddTemplateDirectory(ICollection<string> directories, string samplesDirectory)
    {
        if (!Directory.Exists(samplesDirectory))
        {
            return;
        }

        var fullDirectory = Path.GetFullPath(samplesDirectory);
        if (directories.Any(directory => string.Equals(Path.GetFullPath(directory), fullDirectory, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        directories.Add(samplesDirectory);
    }

    public bool TryLoadDefaultFallbackPolygons(out IReadOnlyList<Point[]> polygons, out Size playfieldSize)
    {
        polygons = [];
        playfieldSize = default;

        var samplesDirectory = TelemetryRootDirectory.GetExpectedDirectory();
        if (!Directory.Exists(samplesDirectory) ||
            !TryFindDefaultFallbackSample(samplesDirectory, out var samplePath, out var expectedPath, out var maskedExpectedPath))
        {
            return false;
        }

        using var sampleImage = Cv2.ImRead(samplePath);
        using var expectedImage = Cv2.ImRead(expectedPath);
        if (sampleImage.Empty() || expectedImage.Empty())
        {
            return false;
        }

        var playfieldDetection = playfieldDetector.Detect(sampleImage);
        if (!playfieldDetection.IsFound)
        {
            return false;
        }

        polygons = LoadExpectedPolygons(sampleImage, expectedImage, maskedExpectedPath, playfieldDetection.Bounds);
        playfieldSize = playfieldDetection.Bounds.Size;
        return polygons.Count > 0;
    }

    public bool TryLoadDefaultFallbackScreenPolygons(out IReadOnlyList<Point[]> polygons)
    {
        polygons = [];

        var samplesDirectory = TelemetryRootDirectory.GetExpectedDirectory();
        if (!Directory.Exists(samplesDirectory) ||
            !TryFindDefaultFallbackSample(samplesDirectory, out var samplePath, out var expectedPath, out var maskedExpectedPath))
        {
            return false;
        }

        using var sampleImage = Cv2.ImRead(samplePath);
        using var expectedImage = Cv2.ImRead(expectedPath);
        if (sampleImage.Empty() || expectedImage.Empty())
        {
            return false;
        }

        var playfieldDetection = playfieldDetector.Detect(sampleImage);
        if (!playfieldDetection.IsFound)
        {
            return false;
        }

        polygons = LoadExpectedPolygons(sampleImage, expectedImage, maskedExpectedPath, playfieldDetection.Bounds)
            .Select(points => TranslatePolygon(points, playfieldDetection.Bounds.Location))
            .ToArray();
        return polygons.Count > 0;
    }

    private IReadOnlyList<KnownSampleTemplate> GetTemplates(string samplesDirectory)
    {
        return TemplateCache.GetOrAdd(
            samplesDirectory,
            key => new Lazy<IReadOnlyList<KnownSampleTemplate>>(() => LoadTemplates(key)))
            .Value;
    }

    private List<KnownSampleTemplate> LoadTemplates(string samplesDirectory)
    {
        var templates = new List<KnownSampleTemplate>();
        var sampleFiles = Directory
            .EnumerateFiles(samplesDirectory, "*.sample.png", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);

        foreach (var sampleFile in sampleFiles)
        {
            var expectedPath = Path.Combine(
                samplesDirectory,
                Path.GetFileNameWithoutExtension(sampleFile) + ".expected.png");
            var maskedExpectedPath = Path.Combine(
                samplesDirectory,
                Path.GetFileNameWithoutExtension(sampleFile) + MaskedExpectedSuffix);
            if (!File.Exists(expectedPath))
            {
                continue;
            }

            using var sampleImage = Cv2.ImRead(sampleFile);
            using var expectedImage = Cv2.ImRead(expectedPath);
            if (sampleImage.Empty() || expectedImage.Empty())
            {
                continue;
            }

            var playfieldDetection = playfieldDetector.Detect(sampleImage);
            if (!playfieldDetection.IsFound)
            {
                continue;
            }

            using var playfieldImage = new Mat(sampleImage, playfieldDetection.Bounds);
            using var signature = BuildSignature(playfieldImage);
            var polygons = LoadExpectedPolygons(
                sampleImage,
                expectedImage,
                maskedExpectedPath,
                playfieldDetection.Bounds);
            if (polygons.Length == 0)
            {
                continue;
            }

            templates.Add(new KnownSampleTemplate(Path.GetFileName(sampleFile), signature.Clone(), polygons));
        }

        return templates;
    }

    private static bool TryFindDefaultFallbackSample(
        string samplesDirectory,
        out string samplePath,
        out string expectedPath,
        out string maskedExpectedPath)
    {
        samplePath = Path.Combine(samplesDirectory, $"{DefaultFallbackExampleName}.png");
        expectedPath = Path.Combine(
            samplesDirectory,
            Path.GetFileNameWithoutExtension(samplePath) + ".expected.png");
        maskedExpectedPath = Path.Combine(
            samplesDirectory,
            Path.GetFileNameWithoutExtension(samplePath) + MaskedExpectedSuffix);

        if (File.Exists(samplePath) && File.Exists(expectedPath))
        {
            return true;
        }

        samplePath = string.Empty;
        expectedPath = string.Empty;
        maskedExpectedPath = string.Empty;
        return false;
    }

    private static Point[] TranslatePolygon(Point[] polygon, Point offset)
    {
        return polygon
            .Select(point => new Point(point.X + offset.X, point.Y + offset.Y))
            .ToArray();
    }

    private static IReadOnlyList<Point[]> ScalePolygons(
        IReadOnlyList<Point[]> polygons,
        Size sourceSize,
        Size targetSize)
    {
        if (sourceSize.Width == targetSize.Width &&
            sourceSize.Height == targetSize.Height)
        {
            return polygons;
        }

        var scaleX = targetSize.Width / (double)sourceSize.Width;
        var scaleY = targetSize.Height / (double)sourceSize.Height;
        return polygons
            .Select(polygon => polygon
                .Select(point => new Point(
                    (int)Math.Round(point.X * scaleX),
                    (int)Math.Round(point.Y * scaleY)))
                .ToArray())
            .ToArray();
    }

    private static Point[][] LoadExpectedPolygons(
        Mat sampleImage,
        Mat expectedImage,
        string maskedExpectedPath,
        Rect playfieldBounds)
    {
        if (File.Exists(maskedExpectedPath))
        {
            using var maskedExpectedImage = Cv2.ImRead(maskedExpectedPath);
            if (!maskedExpectedImage.Empty())
            {
                var maskedPolygons = ExtractMaskedExpectedPolygons(maskedExpectedImage, playfieldBounds);
                if (maskedPolygons.Length > 0)
                {
                    return maskedPolygons;
                }
            }
        }

        return ExtractExpectedPolygons(sampleImage, expectedImage, playfieldBounds);
    }

    private static Mat BuildSignature(Mat playfieldImage)
    {
        using var grayscale = new Mat();
        using var resized = new Mat();
        using var blurred = new Mat();
        Cv2.CvtColor(playfieldImage, grayscale, ColorConversionCodes.BGR2GRAY);
        Cv2.Resize(grayscale, resized, new Size(SignatureWidth, SignatureHeight), 0, 0, InterpolationFlags.Area);
        Cv2.GaussianBlur(resized, blurred, new Size(0, 0), 1.5, 1.5);
        return blurred.Clone();
    }

    private static Point[][] ExtractExpectedPolygons(Mat originalImage, Mat expectedImage, Rect playfieldBounds)
    {
        using var originalPlayfield = new Mat(originalImage, playfieldBounds);
        using var expectedPlayfield = new Mat(expectedImage, playfieldBounds);
        using var overlayMask = BuildOverlayMask(originalPlayfield, expectedPlayfield);
        Cv2.FindContours(
            overlayMask,
            out var contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        return contours
            .Where(contour => Cv2.ContourArea(contour) >= OverlayMinimumContourArea)
            .OrderByDescending(contour => Cv2.ContourArea(contour))
            .Select(SimplifyPolygon)
            .Where(points => points.Length >= 3)
            .ToArray();
    }

    private static Point[][] ExtractVisibleExpectedPolygons(
        Mat sampleImage,
        Mat expectedImage,
        Rect samplePlayfieldBounds,
        Rect expectedPlayfieldBounds)
    {
        using var expectedPlayfield = new Mat(expectedImage, expectedPlayfieldBounds);
        using var samplePlayfield = BuildComparablePlayfield(sampleImage, samplePlayfieldBounds, expectedPlayfield.Size());
        using var overlayMask = BuildVisibleExpectedOverlayMask(samplePlayfield, expectedPlayfield);
        Cv2.FindContours(
            overlayMask,
            out var contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        var polygons = contours
            .Where(contour => Cv2.ContourArea(contour) >= OverlayMinimumContourArea)
            .OrderByDescending(contour => Cv2.ContourArea(contour))
            .Select(SimplifyPolygon)
            .Where(points => points.Length >= 3)
            .ToArray();
        return RemoveNestedPolygons(polygons);
    }

    private static Point[][] RemoveNestedPolygons(Point[][] polygons)
    {
        var retainedPolygons = new List<Point[]>(polygons.Length);
        foreach (var polygon in polygons.OrderByDescending(points => Math.Abs(Cv2.ContourArea(points))))
        {
            var center = GetPolygonCenter(polygon);
            if (retainedPolygons.Any(retainedPolygon => Cv2.PointPolygonTest(retainedPolygon, center, false) >= 0))
            {
                continue;
            }

            retainedPolygons.Add(polygon);
        }

        return retainedPolygons.ToArray();
    }

    private static Point2f GetPolygonCenter(Point[] polygon)
    {
        var moments = Cv2.Moments(polygon);
        if (Math.Abs(moments.M00) <= double.Epsilon)
        {
            var bounds = Cv2.BoundingRect(polygon);
            return new Point2f(bounds.X + (bounds.Width / 2f), bounds.Y + (bounds.Height / 2f));
        }

        return new Point2f(
            (float)(moments.M10 / moments.M00),
            (float)(moments.M01 / moments.M00));
    }

    private static Mat BuildComparablePlayfield(Mat image, Rect playfieldBounds, Size targetSize)
    {
        using var sourcePlayfield = new Mat(image, playfieldBounds);
        if (sourcePlayfield.Size() == targetSize)
        {
            return sourcePlayfield.Clone();
        }

        var resized = new Mat();
        Cv2.Resize(sourcePlayfield, resized, targetSize, 0, 0, InterpolationFlags.Area);
        return resized;
    }

    private static Mat BuildVisibleExpectedOverlayMask(Mat samplePlayfield, Mat expectedPlayfield)
    {
        using var brownMask = BuildBrownOverlayMask(expectedPlayfield);
        using var opened = new Mat();
        using var closed = new Mat();
        using var openKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(OverlayOpenKernelSize, OverlayOpenKernelSize));
        using var closeKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(VisibleOverlayCloseKernelSize, VisibleOverlayCloseKernelSize));
        Cv2.MorphologyEx(brownMask, opened, MorphTypes.Open, openKernel);
        Cv2.MorphologyEx(opened, closed, MorphTypes.Close, closeKernel);
        var filledBrownMask = FillSignificantContours(closed);
        if (Cv2.CountNonZero(filledBrownMask) > OverlayMinimumContourArea)
        {
            return filledBrownMask;
        }

        filledBrownMask.Dispose();

        using var strokeMask = BuildStrokeOverlayMask(samplePlayfield, expectedPlayfield);
        using var closedStrokeMask = new Mat();
        Cv2.MorphologyEx(strokeMask, closedStrokeMask, MorphTypes.Close, closeKernel);
        return FillSignificantContours(closedStrokeMask);
    }

    private static Point[][] ExtractMaskedExpectedPolygons(Mat maskedExpectedImage, Rect playfieldBounds)
    {
        using var maskedPlayfield = new Mat(maskedExpectedImage, playfieldBounds);
        using var overlayMask = BuildMaskedOverlayMask(maskedPlayfield);
        Cv2.FindContours(
            overlayMask,
            out var contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        return contours
            .Where(contour => Cv2.ContourArea(contour) >= OverlayMinimumContourArea)
            .OrderByDescending(contour => Cv2.ContourArea(contour))
            .Select(SimplifyPolygon)
            .Where(points => points.Length >= 3)
            .ToArray();
    }

    private static Mat BuildMaskedOverlayMask(Mat maskedPlayfield)
    {
        using var grayscale = new Mat();
        using var thresholded = new Mat();
        using var opened = new Mat();
        using var closed = new Mat();
        using var openKernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(MaskedOpenKernelSize, MaskedOpenKernelSize));
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(MaskedCloseKernelSize, MaskedCloseKernelSize));

        Cv2.CvtColor(maskedPlayfield, grayscale, ColorConversionCodes.BGR2GRAY);
        Cv2.Threshold(grayscale, thresholded, MaskedThreshold, SampleImageProcessorDebug.BinaryMaskMaxValue, ThresholdTypes.Binary);
        SuppressMaskedOverlayNoise(thresholded);
        Cv2.MorphologyEx(thresholded, opened, MorphTypes.Open, openKernel);
        Cv2.MorphologyEx(opened, closed, MorphTypes.Close, kernel);
        return FillMaskedComponents(closed);
    }

    private static void SuppressMaskedOverlayNoise(Mat mask)
    {
        var border = Math.Min(MaskedNoiseBorder, Math.Min(mask.Width / 12, mask.Height / 12));
        if (border <= 0)
        {
            return;
        }

        mask[new Rect(0, 0, mask.Width, border)].SetTo(Scalar.Black);
        mask[new Rect(0, mask.Height - border, mask.Width, border)].SetTo(Scalar.Black);
        mask[new Rect(0, 0, border, mask.Height)].SetTo(Scalar.Black);
        mask[new Rect(mask.Width - border, 0, border, mask.Height)].SetTo(Scalar.Black);
    }

    private static Mat FillMaskedComponents(Mat mask)
    {
        var filled = new Mat(mask.Size(), MatType.CV_8UC1, Scalar.Black);
        Cv2.FindContours(
            mask,
            out var contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        foreach (var contour in contours)
        {
            var contourArea = Cv2.ContourArea(contour);
            if (contourArea < OverlayMinimumContourArea)
            {
                continue;
            }

            var bounds = Cv2.BoundingRect(contour);
            if (bounds.Width < MaskedMinimumComponentWidth || bounds.Height < MaskedMinimumComponentHeight)
            {
                continue;
            }

            var boundingArea = bounds.Width * bounds.Height;
            var fillRatio = contourArea / boundingArea;
            if (fillRatio < MaskedMinimumFillRatio)
            {
                continue;
            }

            var hull = Cv2.ConvexHull(contour);
            var hullArea = Math.Max(1.0, Cv2.ContourArea(hull));
            var hullRatio = contourArea / hullArea;
            if (hullRatio < MaskedMinimumHullRatio)
            {
                continue;
            }

            Cv2.DrawContours(filled, [contour], -1, Scalar.White, -1);
        }

        return filled;
    }

    private static Mat BuildOverlayMask(Mat originalPlayfield, Mat expectedPlayfield)
    {
        using var filteredMask = BuildFilteredOverlayMask(originalPlayfield, expectedPlayfield);
        using var strokeMask = BuildStrokeOverlayMask(originalPlayfield, expectedPlayfield);
        using var colorMask = BuildBrownOverlayMask(expectedPlayfield);
        using var differenceMask = BuildDifferenceMask(originalPlayfield, expectedPlayfield);
        using var combinedMask = new Mat();
        using var opened = new Mat();
        using var closed = new Mat();
        using var openKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(OverlayOpenKernelSize, OverlayOpenKernelSize));
        using var closeKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(OverlayCloseKernelSize, OverlayCloseKernelSize));
        Cv2.BitwiseAnd(colorMask, differenceMask, combinedMask);
        SuppressOverlayNoise(combinedMask);
        Cv2.MorphologyEx(combinedMask, opened, MorphTypes.Open, openKernel);
        Cv2.MorphologyEx(opened, closed, MorphTypes.Close, closeKernel);

        var filled = new Mat(closed.Size(), MatType.CV_8UC1, Scalar.Black);
        Cv2.FindContours(
            closed,
            out var contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        foreach (var contour in contours)
        {
            if (Cv2.ContourArea(contour) < OverlayMinimumContourArea)
            {
                continue;
            }

            Cv2.DrawContours(filled, [contour], -1, Scalar.White, -1);
        }

        return SelectBestOverlayMask(filteredMask, strokeMask, filled);
    }

    private static Mat BuildFilteredOverlayMask(Mat originalPlayfield, Mat expectedPlayfield)
    {
        using var originalGray = new Mat();
        using var expectedGray = new Mat();
        using var valueGain = new Mat();
        using var differenceMask = BuildDifferenceMask(originalPlayfield, expectedPlayfield);
        using var warmShiftMask = BuildWarmShiftMask(originalPlayfield, expectedPlayfield);
        using var valueGainMask = new Mat();
        using var unionMask = new Mat();
        using var gatedMask = new Mat();
        using var opened = new Mat();
        using var closed = new Mat();
        using var openKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(FilteredOpenKernelSize, FilteredOpenKernelSize));
        using var closeKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(FilteredCloseKernelSize, FilteredCloseKernelSize));

        Cv2.CvtColor(originalPlayfield, originalGray, ColorConversionCodes.BGR2GRAY);
        Cv2.CvtColor(expectedPlayfield, expectedGray, ColorConversionCodes.BGR2GRAY);
        Cv2.Subtract(expectedGray, originalGray, valueGain);
        Cv2.Threshold(valueGain, valueGainMask, OverlayValueGainThreshold, SampleImageProcessorDebug.BinaryMaskMaxValue, ThresholdTypes.Binary);
        Cv2.BitwiseOr(warmShiftMask, valueGainMask, unionMask);
        Cv2.Threshold(differenceMask, differenceMask, OverlayDifferenceFloor, SampleImageProcessorDebug.BinaryMaskMaxValue, ThresholdTypes.Binary);
        Cv2.BitwiseAnd(unionMask, differenceMask, gatedMask);
        SuppressOverlayNoise(gatedMask);
        Cv2.MorphologyEx(gatedMask, opened, MorphTypes.Open, openKernel);
        Cv2.MorphologyEx(opened, closed, MorphTypes.Close, closeKernel);
        return FillSignificantContours(closed);
    }

    private static Mat BuildWarmShiftMask(Mat originalPlayfield, Mat expectedPlayfield)
    {
        var originalChannels = originalPlayfield.Split();
        var expectedChannels = expectedPlayfield.Split();

        try
        {
            using var redGain = new Mat();
            using var greenGain = new Mat();
            using var blueLoss = new Mat();
            using var warmResponse = new Mat();
            using var boostedWarmResponse = new Mat();
            using var shiftedWarmResponse = new Mat();
            using var thresholded = new Mat();

            Cv2.Subtract(expectedChannels[2], originalChannels[2], redGain);
            Cv2.Subtract(expectedChannels[1], originalChannels[1], greenGain);
            Cv2.Subtract(originalChannels[0], expectedChannels[0], blueLoss);
            Cv2.AddWeighted(redGain, 1.0, greenGain, 0.7, 0, warmResponse);
            Cv2.AddWeighted(warmResponse, 1.0, blueLoss, 0.8, 0, boostedWarmResponse);
            Cv2.Normalize(boostedWarmResponse, shiftedWarmResponse, 0, SampleImageProcessorDebug.BinaryMaskMaxValue, NormTypes.MinMax);
            Cv2.Threshold(
                shiftedWarmResponse,
                thresholded,
                OverlaySignedDeltaThreshold,
                SampleImageProcessorDebug.BinaryMaskMaxValue,
                ThresholdTypes.Binary);
            return thresholded.Clone();
        }
        finally
        {
            foreach (var channel in originalChannels)
            {
                channel.Dispose();
            }

            foreach (var channel in expectedChannels)
            {
                channel.Dispose();
            }
        }
    }

    private static Mat BuildStrokeOverlayMask(Mat originalPlayfield, Mat expectedPlayfield)
    {
        using var expectedHsv = new Mat();
        using var strokeColorMask = new Mat();
        using var differenceMask = BuildDifferenceMask(originalPlayfield, expectedPlayfield);
        using var strokeDifferenceMask = new Mat();
        using var combinedMask = new Mat();
        using var closed = new Mat();
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(StrokeCloseKernelSize, StrokeCloseKernelSize));
        Cv2.CvtColor(expectedPlayfield, expectedHsv, ColorConversionCodes.BGR2HSV);
        Cv2.InRange(
            expectedHsv,
            new Scalar(0, 0, StrokeValueMinimum),
            new Scalar(180, StrokeSaturationMaximum, 255),
            strokeColorMask);
        Cv2.Threshold(differenceMask, strokeDifferenceMask, StrokeDifferenceThreshold, SampleImageProcessorDebug.BinaryMaskMaxValue, ThresholdTypes.Binary);
        Cv2.BitwiseAnd(strokeColorMask, strokeDifferenceMask, combinedMask);
        SuppressOverlayNoise(combinedMask);
        Cv2.MorphologyEx(combinedMask, closed, MorphTypes.Close, kernel);
        return closed.Clone();
    }

    private static Mat SelectBestOverlayMask(params Mat[] masks)
    {
        Mat? bestMask = null;
        var bestScore = double.MinValue;
        var bestArea = double.MinValue;

        foreach (var mask in masks)
        {
            Cv2.FindContours(
                mask,
                out var contours,
                out _,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple);

            var significantContours = contours
                .Where(contour => Cv2.ContourArea(contour) >= OverlayMinimumContourArea)
                .ToArray();
            if (significantContours.Length == 0)
            {
                continue;
            }

            var totalArea = significantContours.Sum(contour => Cv2.ContourArea(contour));
            var largestArea = significantContours.Max(contour => Cv2.ContourArea(contour));
            var averageArea = totalArea / significantContours.Length;
            var score = totalArea + largestArea + (averageArea * 0.5) - (significantContours.Length * 250.0);
            if (score < bestScore)
            {
                continue;
            }

            if (Math.Abs(score - bestScore) < double.Epsilon && totalArea <= bestArea)
            {
                continue;
            }

            bestMask = mask;
            bestScore = score;
            bestArea = totalArea;
        }

        return bestMask?.Clone() ?? masks[0].Clone();
    }

    private static Mat FillSignificantContours(Mat mask)
    {
        var filled = new Mat(mask.Size(), MatType.CV_8UC1, Scalar.Black);
        Cv2.FindContours(
            mask,
            out var contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        foreach (var contour in contours)
        {
            if (Cv2.ContourArea(contour) < OverlayMinimumContourArea)
            {
                continue;
            }

            Cv2.DrawContours(filled, [contour], -1, Scalar.White, -1);
        }

        return filled;
    }

    private static Mat BuildBrownOverlayMask(Mat expectedPlayfield)
    {
        using var hsv = new Mat();
        using var hsvMask = new Mat();
        using var bgrMask = new Mat();
        using var brownMask = new Mat();
        Cv2.CvtColor(expectedPlayfield, hsv, ColorConversionCodes.BGR2HSV);
        Cv2.InRange(
            hsv,
            new Scalar(BrownHueMinimum, BrownSaturationMinimum, BrownValueMinimum),
            new Scalar(BrownHueMaximum, 255, 255),
            hsvMask);

        var channels = expectedPlayfield.Split();
        try
        {
            using var redMinimumMask = new Mat();
            using var greenMinimumMask = new Mat();
            using var blueMaximumMask = new Mat();
            using var redGreenDifferenceMask = new Mat();
            using var greenBlueDifferenceMask = new Mat();
            using var redGreenDifferenceThresholdMask = new Mat();
            using var greenBlueDifferenceThresholdMask = new Mat();
            using var redGreenDominantMask = new Mat();

            Cv2.Threshold(channels[2], redMinimumMask, BrownRedMinimum, SampleImageProcessorDebug.BinaryMaskMaxValue, ThresholdTypes.Binary);
            Cv2.Threshold(channels[1], greenMinimumMask, BrownGreenMinimum, SampleImageProcessorDebug.BinaryMaskMaxValue, ThresholdTypes.Binary);
            Cv2.Threshold(channels[0], blueMaximumMask, BrownBlueMaximum, SampleImageProcessorDebug.BinaryMaskMaxValue, ThresholdTypes.BinaryInv);
            Cv2.Subtract(channels[2], channels[1], redGreenDifferenceMask);
            Cv2.Subtract(channels[1], channels[0], greenBlueDifferenceMask);
            Cv2.Threshold(redGreenDifferenceMask, redGreenDifferenceThresholdMask, BrownDominanceMinimum, SampleImageProcessorDebug.BinaryMaskMaxValue, ThresholdTypes.Binary);
            Cv2.Threshold(greenBlueDifferenceMask, greenBlueDifferenceThresholdMask, 0, SampleImageProcessorDebug.BinaryMaskMaxValue, ThresholdTypes.Binary);

            Cv2.BitwiseAnd(redMinimumMask, greenMinimumMask, bgrMask);
            Cv2.BitwiseAnd(bgrMask, blueMaximumMask, bgrMask);
            Cv2.BitwiseAnd(redGreenDifferenceThresholdMask, greenBlueDifferenceThresholdMask, redGreenDominantMask);
            Cv2.BitwiseAnd(bgrMask, redGreenDominantMask, bgrMask);
            Cv2.BitwiseAnd(hsvMask, bgrMask, brownMask);
            return brownMask.Clone();
        }
        finally
        {
            foreach (var channel in channels)
            {
                channel.Dispose();
            }
        }
    }

    private static Mat BuildDifferenceMask(Mat originalPlayfield, Mat expectedPlayfield)
    {
        using var difference = new Mat();
        using var differenceMax = new Mat();
        Cv2.Absdiff(originalPlayfield, expectedPlayfield, difference);
        Cv2.ExtractChannel(difference, differenceMax, 0);

        using var greenChannel = new Mat();
        using var redChannel = new Mat();
        Cv2.ExtractChannel(difference, greenChannel, 1);
        Cv2.ExtractChannel(difference, redChannel, 2);
        Cv2.Max(differenceMax, greenChannel, differenceMax);
        Cv2.Max(differenceMax, redChannel, differenceMax);

        var thresholded = new Mat();
        Cv2.Threshold(differenceMax, thresholded, OverlayDifferenceThreshold, SampleImageProcessorDebug.BinaryMaskMaxValue, ThresholdTypes.Binary);
        return thresholded;
    }

    private static void SuppressOverlayNoise(Mat mask)
    {
        var border = Math.Min(OverlayNoiseBorder, Math.Min(mask.Width / 8, mask.Height / 8));
        if (border > 0)
        {
            mask[new Rect(0, 0, mask.Width, border)].SetTo(Scalar.Black);
            mask[new Rect(0, mask.Height - border, mask.Width, border)].SetTo(Scalar.Black);
            mask[new Rect(0, 0, border, mask.Height)].SetTo(Scalar.Black);
            mask[new Rect(mask.Width - border, 0, border, mask.Height)].SetTo(Scalar.Black);
        }

        var topNoiseHeight = Math.Min(OverlayTopNoiseHeight, mask.Height / 6);
        var bottomNoiseHeight = Math.Min(OverlayBottomNoiseHeight, mask.Height / 10);
        if (topNoiseHeight > 0)
        {
            mask[new Rect(0, 0, mask.Width, topNoiseHeight)].SetTo(Scalar.Black);
        }

        if (bottomNoiseHeight > 0)
        {
            mask[new Rect(0, mask.Height - bottomNoiseHeight, mask.Width, bottomNoiseHeight)].SetTo(Scalar.Black);
        }
    }

    private static Point[] SimplifyPolygon(Point[] contour)
    {
        var contourInput = contour.ToArray();
        var perimeter = Cv2.ArcLength(contourInput, true);
        var epsilon = Math.Max(MinimumSimplificationEpsilon, perimeter * SimplificationEpsilonScale);
        var bestApproximation = contourInput;

        for (var attempt = 0; attempt < MaxSimplificationAttempts; attempt++)
        {
            var approximation = Cv2.ApproxPolyDP(contourInput, epsilon, true);
            if (approximation.Length >= 3)
            {
                bestApproximation = approximation;
            }

            if (approximation.Length <= MaximumPolygonPoints)
            {
                return approximation;
            }

            epsilon *= SimplificationGrowthFactor;
        }

        return bestApproximation;
    }

    private sealed record KnownSampleTemplate(
        string FileName,
        Mat Signature,
        IReadOnlyList<Point[]> Polygons);

    private static class SampleImageProcessorDebug
    {
        public const int BinaryMaskMaxValue = 255;
    }
}