using Automaton.Core.Infrastructure;
using OpenCvSharp;
using Serilog;

namespace Automaton.Core.Detectors;

internal sealed class LoggedInPilotDetector : IDisposable
{
    private const int PortraitX = 0;
    private const int PortraitY = 48;
    private const int PortraitSize = 48;
    private const double MinimumMatchScore = 0.84;
    private const double EarlyExitScore = 0.92;
    private static readonly Rect PortraitBounds = new(PortraitX, PortraitY, PortraitSize, PortraitSize);
    private static readonly ILogger Logger = Log.ForContext<LoggedInPilotDetector>();

    private readonly Dictionary<string, Mat> m_TemplateCache = new(StringComparer.OrdinalIgnoreCase);
    private string? m_CandidatesDirectory;
    private IReadOnlyList<LoggedInPilotCandidate>? m_CachedCandidates;
    private DateTime m_CandidatesCacheTime;

    public void Dispose()
    {
        foreach (var template in m_TemplateCache.Values)
        {
            template.Dispose();
        }

        m_TemplateCache.Clear();
        m_CandidatesDirectory = null;
        m_CachedCandidates = null;
    }

    public bool Detect(Mat screen, out LoggedInPilotDetection detection)
    {
        detection = default;

        var pilotDirectory = Path.GetFullPath(AvatarsDirectory.GetDirectory());
        if (screen.Empty() || !IsPortraitRegionAvailable(screen.Size()) || !Directory.Exists(pilotDirectory))
        {
            return false;
        }

        using var portrait = new Mat(screen, PortraitBounds);
        using var searchablePortrait = PrepareBgr(portrait);
        using var result = new Mat();
        LoggedInPilotDetection? bestDetection = null;

        foreach (var candidate in GetCandidates(pilotDirectory))
        {
            var template = GetOrLoadTemplate(candidate);
            if (template is null)
            {
                continue;
            }

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
            Logger.Debug("Logged in pilot not found");
            return false;
        }

        detection = bestDetection.Value;
        return true;
    }

    private IReadOnlyList<LoggedInPilotCandidate> GetCandidates(string pilotDirectory)
    {
        var lastWrite = Directory.GetLastWriteTimeUtc(pilotDirectory);
        if (m_CachedCandidates is not null &&
            lastWrite == m_CandidatesCacheTime &&
            string.Equals(pilotDirectory, m_CandidatesDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return m_CachedCandidates;
        }

        var newCandidates = BuildCandidates(pilotDirectory);
        EvictStaleCacheEntries(newCandidates);
        m_CachedCandidates = newCandidates;
        m_CandidatesDirectory = pilotDirectory;
        m_CandidatesCacheTime = lastWrite;
        return m_CachedCandidates;
    }

    private void EvictStaleCacheEntries(IReadOnlyList<LoggedInPilotCandidate> currentCandidates)
    {
        var currentPaths = new HashSet<string>(currentCandidates.Select(c => c.Path), StringComparer.OrdinalIgnoreCase);
        foreach (var key in m_TemplateCache.Keys.Where(k => !currentPaths.Contains(k)).ToList())
        {
            m_TemplateCache[key].Dispose();
            m_TemplateCache.Remove(key);
        }
    }

    private Mat? GetOrLoadTemplate(LoggedInPilotCandidate candidate)
    {
        if (m_TemplateCache.TryGetValue(candidate.Path, out var cached))
        {
            return cached;
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
        m_TemplateCache[candidate.Path] = scaled;
        return scaled;
    }

    private static LoggedInPilotCandidate[] BuildCandidates(string pilotDirectory) =>
    [
        .. Directory
            .EnumerateFiles(pilotDirectory, "*_focused.png", SearchOption.TopDirectoryOnly)
            .Select(path =>
                new LoggedInPilotCandidate(ParsePilotIndex(Path.GetFileNameWithoutExtension(path)), path))
            .Where(candidate => candidate.PilotIndex > 0)
            .OrderBy(candidate => candidate.PilotIndex)
    ];

    private static int ParsePilotIndex(string fileNameWithoutExtension)
    {
        const string Suffix = "_focused";
        var indexText = fileNameWithoutExtension.AsSpan(0, fileNameWithoutExtension.Length - Suffix.Length);
        return int.TryParse(indexText, out var pilotIndex) ? pilotIndex : 0;
    }

    private static bool IsPortraitRegionAvailable(Size screenSize)
        => screenSize.Width >= PortraitBounds.Right && screenSize.Height >= PortraitBounds.Bottom;

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
}

internal readonly record struct LoggedInPilotDetection(
    int PilotIndex,
    Rect Bounds,
    double Score);