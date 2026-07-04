using System.Collections.Concurrent;
using System.IO;
using Automaton.Helpers;
using Automaton.Infrastructure;
using OpenCvSharp;
using Serilog;

namespace Automaton.Detectors;

internal sealed class KnownSampleMatcher(PlayfieldDetector playfieldDetector)
{
    private const string DefaultFallbackExampleName = "25.sample";
    private const string MaskedExpectedSuffix = ".expected.masked.png";
    private const int SignatureWidth = 96;
    private const int SignatureHeight = 96;
    private const double MaximumMatchScore = 4.0;
    private const int MinimumContourArea = 400;
    private const int BinaryMaskMaxValue = 255;
    private const int MaskedThreshold = 200;
    private const int MaskedOpenKernelSize = 7;
    private const int MaskedCloseKernelSize = 5;
    private const int MaskedNoiseBorder = 8;
    private const int MaskedMinimumComponentWidth = 30;
    private const int MaskedMinimumComponentHeight = 30;
    private const double MaskedMinimumFillRatio = 0.45;
    private const double MaskedMinimumHullRatio = 0.65;

    private static readonly ConcurrentDictionary<string, Lazy<IReadOnlyList<KnownSampleTemplate>>> TemplateCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ILogger Logger = Log.ForContext<KnownSampleMatcher>();

    public bool TryMatch(
        Mat playfieldImage,
        string? sourceImagePath,
        out IReadOnlyList<Point[]> polygons,
        out string? matchedSampleFileName)
    {
        polygons = [];
        matchedSampleFileName = null;

        if (TryLoadAdjacentMaskedPolygons(sourceImagePath, out polygons, out matchedSampleFileName))
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

    private bool TryLoadAdjacentMaskedPolygons(
        string? sourceImagePath,
        out IReadOnlyList<Point[]> polygons,
        out string? matchedSampleFileName)
    {
        polygons = [];
        matchedSampleFileName = null;

        if (!TryGetAdjacentMaskedPath(sourceImagePath, out var samplePath, out var maskedExpectedPath))
        {
            return false;
        }

        using var sampleImage = Cv2.ImRead(samplePath);
        if (sampleImage.Empty())
        {
            return false;
        }

        var playfieldDetection = playfieldDetector.Detect(sampleImage);
        if (!playfieldDetection.IsFound)
        {
            return false;
        }

        polygons = LoadMaskedPolygons(maskedExpectedPath, playfieldDetection.Bounds);
        matchedSampleFileName = Path.GetFileName(samplePath);
        return polygons.Count > 0;
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
        if (!TryGetAdjacentMaskedPath(sourceImagePath, out var samplePath, out _))
        {
            return false;
        }

        samplesDirectory = Path.GetDirectoryName(samplePath)!;
        return true;
    }

    private static bool TryGetAdjacentMaskedPath(
        string? sourceImagePath,
        out string samplePath,
        out string maskedExpectedPath)
    {
        samplePath = string.Empty;
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
        maskedExpectedPath = Path.Combine(
            directory,
            Path.GetFileNameWithoutExtension(sourceImagePath) + MaskedExpectedSuffix);

        if (File.Exists(maskedExpectedPath))
        {
            return true;
        }

        samplePath = string.Empty;
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

        directories.Add(fullDirectory);
    }

    public bool TryLoadDefaultFallbackPolygons(out IReadOnlyList<Point[]> polygons, out Size playfieldSize)
    {
        polygons = [];
        playfieldSize = default;

        var samplesDirectory = TelemetryRootDirectory.GetExpectedDirectory();
        if (!Directory.Exists(samplesDirectory) ||
            !TryFindDefaultFallbackSample(samplesDirectory, out var samplePath, out var maskedExpectedPath))
        {
            return false;
        }

        using var sampleImage = Cv2.ImRead(samplePath);
        if (sampleImage.Empty())
        {
            return false;
        }

        var playfieldDetection = playfieldDetector.Detect(sampleImage);
        if (!playfieldDetection.IsFound)
        {
            return false;
        }

        polygons = LoadMaskedPolygons(maskedExpectedPath, playfieldDetection.Bounds);
        playfieldSize = playfieldDetection.Bounds.Size;
        return polygons.Count > 0;
    }

    public bool TryLoadDefaultFallbackScreenPolygons(out IReadOnlyList<Point[]> polygons)
    {
        polygons = [];

        var samplesDirectory = TelemetryRootDirectory.GetExpectedDirectory();
        if (!Directory.Exists(samplesDirectory) ||
            !TryFindDefaultFallbackSample(samplesDirectory, out var samplePath, out var maskedExpectedPath))
        {
            return false;
        }

        using var sampleImage = Cv2.ImRead(samplePath);
        if (sampleImage.Empty())
        {
            return false;
        }

        var playfieldDetection = playfieldDetector.Detect(sampleImage);
        if (!playfieldDetection.IsFound)
        {
            return false;
        }

        polygons = LoadMaskedPolygons(maskedExpectedPath, playfieldDetection.Bounds)
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
            var maskedExpectedPath = Path.Combine(
                samplesDirectory,
                Path.GetFileNameWithoutExtension(sampleFile) + MaskedExpectedSuffix);
            if (!File.Exists(maskedExpectedPath))
            {
                Logger.Error(
                    "Known sample is missing masked counterpart. SamplePath={SamplePath}, MaskedExpectedPath={MaskedExpectedPath}",
                    sampleFile,
                    maskedExpectedPath);
                continue;
            }

            using var sampleImage = Cv2.ImRead(sampleFile);
            if (sampleImage.Empty())
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
            var polygons = LoadMaskedPolygons(maskedExpectedPath, playfieldDetection.Bounds);
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
        out string maskedExpectedPath)
    {
        samplePath = Path.Combine(samplesDirectory, $"{DefaultFallbackExampleName}.png");
        maskedExpectedPath = Path.Combine(
            samplesDirectory,
            Path.GetFileNameWithoutExtension(samplePath) + MaskedExpectedSuffix);

        if (File.Exists(samplePath) && File.Exists(maskedExpectedPath))
        {
            return true;
        }

        samplePath = string.Empty;
        maskedExpectedPath = string.Empty;
        return false;
    }

    private static Point[] TranslatePolygon(Point[] polygon, Point offset)
    {
        return polygon
            .Select(point => new Point(point.X + offset.X, point.Y + offset.Y))
            .ToArray();
    }

    private static Point[][] LoadMaskedPolygons(string maskedExpectedPath, Rect playfieldBounds)
    {
        if (!File.Exists(maskedExpectedPath))
        {
            return [];
        }

        using var maskedExpectedImage = Cv2.ImRead(maskedExpectedPath);
        return maskedExpectedImage.Empty() ? [] : ExtractMaskedExpectedPolygons(maskedExpectedImage, playfieldBounds);
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
            .Where(contour => Cv2.ContourArea(contour) >= MinimumContourArea)
            .OrderByDescending(contour => Cv2.ContourArea(contour))
            .Select(contour => GeometryHelper.SimplifyContour(contour))
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
        Cv2.Threshold(grayscale, thresholded, MaskedThreshold, BinaryMaskMaxValue, ThresholdTypes.Binary);
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
            if (contourArea < MinimumContourArea)
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

    private sealed record KnownSampleTemplate(
        string FileName,
        Mat Signature,
        IReadOnlyList<Point[]> Polygons);
}
