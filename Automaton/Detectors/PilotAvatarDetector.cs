using System.IO;
using Automaton.Infrastructure;
using Automaton.Helpers;
using OpenCvSharp;
using Serilog;

namespace Automaton.Detectors;

internal sealed class PilotAvatarDetector : IDisposable
{
    private const double MinimumMatchScore = 0.90;
    private const double EarlyExitScore = 0.98;
    private static readonly double[] AllScales = [1.0, 0.95, 1.05, 0.90, 1.10];
    private static readonly ILogger Logger = Log.ForContext<PilotAvatarDetector>();

    private readonly Dictionary<string, CachedTemplate> m_TemplateCache = new();

    public void Dispose()
    {
        foreach (var cached in m_TemplateCache.Values)
        {
            cached.Dispose();
        }

        m_TemplateCache.Clear();
    }

    public bool Detect(Mat screen, int requestedPilotIndex, out PilotAvatarLocation location)
    {
        var match = TryLocateBest(screen, requestedPilotIndex);

        if (match is null || match.Value.Score < MinimumMatchScore)
        {
            Logger.Error("RequestedPilotIndex={RequestedPilotIndex} not found!", requestedPilotIndex);
            location = default;
            return false;
        }

        location = match.Value;
        return true;
    }

    private PilotAvatarLocation? TryLocateBest(Mat screen, int pilotIndex)
    {
        var pilotDirectory = Path.GetFullPath(PilotAvatarDirectory.GetDirectory());
        if (screen.Empty() || !Directory.Exists(pilotDirectory))
        {
            return null;
        }

        PilotAvatarLocation? bestLocation = null;
        foreach (var candidate in BuildCandidates(pilotDirectory, pilotIndex))
        {
            var variants = GetOrLoadVariants(candidate);
            if (variants is null)
            {
                continue;
            }

            var searchableScreen = PrepareScreen(screen, candidate.UsesColor, out var ownsScreen);
            try
            {
                if (searchableScreen.Empty())
                {
                    continue;
                }

                bestLocation = MatchAcrossScales(searchableScreen, variants, bestLocation);
            }
            finally
            {
                if (ownsScreen)
                {
                    searchableScreen.Dispose();
                }
            }

            if (bestLocation is not null && bestLocation.Value.Score >= EarlyExitScore)
            {
                break;
            }
        }

        return bestLocation;
    }

    private static PilotAvatarLocation? MatchAcrossScales(
        Mat searchableScreen,
        Mat[] variants,
        PilotAvatarLocation? currentBest)
    {
        var bestLocation = currentBest;

        foreach (var template in variants)
        {
            if (template.Width > searchableScreen.Width || template.Height > searchableScreen.Height)
            {
                continue;
            }

            using var result = new Mat();
            Cv2.MatchTemplate(searchableScreen, template, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out var score, out _, out var point);

            if (bestLocation is null || score > bestLocation.Value.Score)
            {
                bestLocation = new PilotAvatarLocation(
                    new Rect(point.X, point.Y, template.Width, template.Height),
                    score);

                if (score >= EarlyExitScore)
                {
                    break;
                }
            }
        }

        return bestLocation;
    }

    private Mat[]? GetOrLoadVariants(PilotAvatarCandidate candidate)
    {
        if (m_TemplateCache.TryGetValue(candidate.Path, out var cached))
        {
            return cached.Variants;
        }

        if (!File.Exists(candidate.Path))
        {
            return null;
        }

        var mode = candidate.UsesColor ? ImreadModes.Color : ImreadModes.Grayscale;
        using var original = Cv2.ImRead(candidate.Path, mode);
        if (original.Empty())
        {
            return null;
        }

        var variants = BuildScaledVariants(original);
        m_TemplateCache[candidate.Path] = new CachedTemplate(variants);
        return variants;
    }

    private static Mat[] BuildScaledVariants(Mat original)
    {
        var variants = new Mat[AllScales.Length];
        for (var i = 0; i < AllScales.Length; i++)
        {
            var scale = AllScales[i];
            if (GeometryHelper.IsUnscaled(scale))
            {
                variants[i] = original.Clone();
            }
            else
            {
                var width = Math.Max(1, (int)Math.Round(original.Width * scale));
                var height = Math.Max(1, (int)Math.Round(original.Height * scale));
                var scaled = new Mat();
                Cv2.Resize(original, scaled, new Size(width, height));
                variants[i] = scaled;
            }
        }

        return variants;
    }

    private static Mat PrepareScreen(Mat screen, bool useColor, out bool ownsResult)
    {
        if (useColor)
        {
            if (screen.Channels() >= 3)
            {
                ownsResult = false;
                return screen;
            }

            ownsResult = true;
            var colorScreen = new Mat();
            Cv2.CvtColor(screen, colorScreen, ColorConversionCodes.GRAY2BGR);
            return colorScreen;
        }

        if (screen.Channels() == 1)
        {
            ownsResult = false;
            return screen;
        }

        ownsResult = true;
        var grayScreen = new Mat();
        Cv2.CvtColor(screen, grayScreen, ColorConversionCodes.BGR2GRAY);
        return grayScreen;
    }

    private static IReadOnlyList<PilotAvatarCandidate> BuildCandidates(string pilotDirectory, int pilotIndex)
    {
        return
        [
            new PilotAvatarCandidate(Path.Combine(pilotDirectory, $"{pilotIndex}_focused.png"), UsesColor: true),
            new PilotAvatarCandidate(Path.Combine(pilotDirectory, $"{pilotIndex}.png"), UsesColor: false)
        ];
    }

    private readonly record struct PilotAvatarCandidate(string Path, bool UsesColor);

    private sealed class CachedTemplate(Mat[] variants) : IDisposable
    {
        public Mat[] Variants => variants;

        public void Dispose()
        {
            foreach (var mat in variants)
            {
                mat.Dispose();
            }
        }
    }
}

internal readonly record struct PilotAvatarLocation(
    Rect Bounds,
    double Score);
