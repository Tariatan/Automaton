using System.IO;
using OpenCvSharp;
using Serilog;

namespace Automaton.Detectors;

internal sealed class LoggedInPilotDetector : IDisposable
{
    private const string PilotFolderName = "pilot";
    private const int PortraitX = 0;
    private const int PortraitY = 48;
    private const int PortraitSize = 48;
    private const double MinimumMatchScore = 0.84;
    private const double EarlyExitScore = 0.98;
    private static readonly Rect PortraitBounds = new(PortraitX, PortraitY, PortraitSize, PortraitSize);
    private static readonly ILogger Logger = Log.ForContext<LoggedInPilotDetector>();

    private readonly Dictionary<string, CachedTemplate> m_TemplateCache = new(StringComparer.OrdinalIgnoreCase);

    public void Dispose()
    {
        foreach (var cached in m_TemplateCache.Values)
        {
            cached.Dispose();
        }

        m_TemplateCache.Clear();
    }

    public bool Detect(Mat screen, out LoggedInPilotDetection detection)
    {
        detection = default;

        if (screen.Empty() || !IsPortraitRegionAvailable(screen.Size()) || !Directory.Exists(PilotFolderName))
        {
            return false;
        }

        using var portrait = new Mat(screen, PortraitBounds);
        using var searchablePortrait = PrepareBgr(portrait);
        LoggedInPilotDetection? bestDetection = null;

        foreach (var candidate in BuildCandidates(PilotFolderName))
        {
            var template = GetOrLoadTemplate(candidate);
            if (template is null)
            {
                continue;
            }

            using var result = new Mat();
            Cv2.MatchTemplate(searchablePortrait, template, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out var score, out _, out _);

            if (bestDetection is null || score > bestDetection.Value.Score)
            {
                bestDetection = new LoggedInPilotDetection(candidate.PilotIndex, PortraitBounds, score);
            }

            if (bestDetection.Value.Score >= EarlyExitScore)
            {
                break;
            }
        }

        if (bestDetection is null || bestDetection.Value.Score < MinimumMatchScore)
        {
            Logger.Error("Logged in pilot not found!");
            return false;
        }

        detection = bestDetection.Value;
        return true;
    }

    private Mat? GetOrLoadTemplate(LoggedInPilotCandidate candidate)
    {
        if (m_TemplateCache.TryGetValue(candidate.Path, out var cached))
        {
            return cached.Template;
        }

        if (!File.Exists(candidate.Path))
        {
            return null;
        }

        using var original = Cv2.ImRead(candidate.Path);
        if (original.Empty())
        {
            return null;
        }

        using var originalBgr = PrepareBgr(original);
        var scaled = new Mat();
        Cv2.Resize(originalBgr, scaled, new Size(PortraitSize, PortraitSize), 0, 0, InterpolationFlags.Area);
        m_TemplateCache[candidate.Path] = new CachedTemplate(scaled);
        return scaled;
    }

    private static IReadOnlyList<LoggedInPilotCandidate> BuildCandidates(string pilotDirectory)
    {
        return Directory
            .EnumerateFiles(pilotDirectory, "*_focused.png", SearchOption.TopDirectoryOnly)
            .Select(path => new LoggedInPilotCandidate(ParsePilotIndex(Path.GetFileNameWithoutExtension(path)), path))
            .Where(candidate => candidate.PilotIndex > 0)
            .OrderBy(candidate => candidate.PilotIndex)
            .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static int ParsePilotIndex(string? fileNameWithoutExtension)
    {
        if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
        {
            return 0;
        }

        var indexText = fileNameWithoutExtension.EndsWith("_focused", StringComparison.OrdinalIgnoreCase)
            ? fileNameWithoutExtension[..^"_focused".Length]
            : fileNameWithoutExtension;
        return int.TryParse(indexText, out var pilotIndex)
            ? pilotIndex
            : 0;
    }

    private static bool IsPortraitRegionAvailable(Size screenSize)
    {
        return screenSize.Width >= PortraitBounds.Right && screenSize.Height >= PortraitBounds.Bottom;
    }

    private static Mat PrepareBgr(Mat image)
    {
        if (image.Channels() == 3)
        {
            return image.Clone();
        }

        var converted = new Mat();
        var conversion = image.Channels() == 1
            ? ColorConversionCodes.GRAY2BGR
            : ColorConversionCodes.BGRA2BGR;
        Cv2.CvtColor(image, converted, conversion);
        return converted;
    }

    private readonly record struct LoggedInPilotCandidate(int PilotIndex, string Path);

    private sealed class CachedTemplate(Mat template) : IDisposable
    {
        public Mat Template => template;

        public void Dispose()
        {
            template.Dispose();
        }
    }
}

internal readonly record struct LoggedInPilotDetection(
    int PilotIndex,
    Rect Bounds,
    double Score);
