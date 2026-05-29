using System.IO;
using OpenCvSharp;
using Serilog;

namespace Automaton.Detectors;

internal sealed class PilotAvatarLocator
{
    private const string PilotFolderName = "pilot";
    private const double MinimumMatchScore = 0.90;
    private const double EarlyExitScore = 0.98;
    private static readonly double[] TemplateScales = [0.95, 1.05, 0.90, 1.10];
    private static readonly ILogger Logger = Log.ForContext<PilotAvatarLocator>();

    public static bool Detect(string imagePath, int requestedPilotIndex, out PilotAvatarLocation location)
    {
        using var image = Cv2.ImRead(imagePath);
        var match = TryLocateBest(image, requestedPilotIndex);

        if (match is null || match.Value.Score < MinimumMatchScore)
        {
            Logger.Error("RequestedPilotIndex={RequestedPilotIndex} not found!", requestedPilotIndex);
            location = default;
            return false;
        }

        location = match.Value;
        return true;
    }

    private static PilotAvatarLocation? TryLocateBest(Mat screen, int pilotIndex)
    {
        if (screen.Empty() || !Directory.Exists(PilotFolderName))
        {
            return null;
        }

        PilotAvatarLocation? bestLocation = null;
        foreach (var candidate in BuildCandidates(PilotFolderName, pilotIndex))
        {
            if (!File.Exists(candidate.Path))
            {
                continue;
            }

            if (!TryMatchTemplate(screen, candidate, out var candidateLocation))
            {
                continue;
            }

            if (bestLocation is null || candidateLocation.Score > bestLocation.Value.Score)
            {
                bestLocation = candidateLocation;
            }
        }

        return bestLocation;
    }

    private static IReadOnlyList<PilotAvatarCandidate> BuildCandidates(string pilotDirectory, int pilotIndex)
    {
        return
        [
            new PilotAvatarCandidate(Path.Combine(pilotDirectory, $"{pilotIndex}_focused.png"), UsesColor: true),
            new PilotAvatarCandidate(Path.Combine(pilotDirectory, $"{pilotIndex}.png"), UsesColor: false)
        ];
    }

    private static bool TryMatchTemplate(
        Mat screen,
        PilotAvatarCandidate candidate,
        out PilotAvatarLocation location)
    {
        location = default;
        using var searchableScreen = BuildSearchableScreen(screen, candidate.UsesColor);
        using var template = Cv2.ImRead(
            candidate.Path,
            candidate.UsesColor ? ImreadModes.Color : ImreadModes.Grayscale);
        if (searchableScreen.Empty() || template.Empty())
        {
            return false;
        }

        var bestLocation = MatchAtScale(searchableScreen, template, 1.0);
        if (bestLocation is not null && bestLocation.Value.Score >= EarlyExitScore)
        {
            location = bestLocation.Value;
            return true;
        }

        foreach (var scale in TemplateScales)
        {
            using var scaledTemplate = BuildScaledTemplate(template, scale);
            if (scaledTemplate.Width > searchableScreen.Width || scaledTemplate.Height > searchableScreen.Height)
            {
                continue;
            }

            using var result = new Mat();
            Cv2.MatchTemplate(searchableScreen, scaledTemplate, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out var score, out _, out var locationPoint);
            var bounds = new Rect(locationPoint.X, locationPoint.Y, scaledTemplate.Width, scaledTemplate.Height);
            if (bestLocation is null || score > bestLocation.Value.Score)
            {
                bestLocation = new PilotAvatarLocation(bounds, score);
                if (score >= EarlyExitScore)
                {
                    break;
                }
            }
        }

        if (bestLocation is null)
        {
            return false;
        }

        location = bestLocation.Value;
        return true;
    }

    private static PilotAvatarLocation? MatchAtScale(Mat searchableScreen, Mat template, double scale)
    {
        Mat effectiveTemplate;
        bool ownsTemplate;

        if (Math.Abs(scale - 1.0) < double.Epsilon)
        {
            effectiveTemplate = template;
            ownsTemplate = false;
        }
        else
        {
            effectiveTemplate = BuildScaledTemplate(template, scale);
            ownsTemplate = true;
        }

        try
        {
            if (effectiveTemplate.Width > searchableScreen.Width || effectiveTemplate.Height > searchableScreen.Height)
            {
                return null;
            }

            using var result = new Mat();
            Cv2.MatchTemplate(searchableScreen, effectiveTemplate, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out var score, out _, out var locationPoint);
            return new PilotAvatarLocation(
                new Rect(locationPoint.X, locationPoint.Y, effectiveTemplate.Width, effectiveTemplate.Height),
                score);
        }
        finally
        {
            if (ownsTemplate)
            {
                effectiveTemplate.Dispose();
            }
        }
    }

    private static Mat BuildSearchableScreen(Mat screen, bool useColor)
    {
        if (useColor)
        {
            if (screen.Channels() == 3)
            {
                return screen.Clone();
            }

            var colorScreen = new Mat();
            Cv2.CvtColor(screen, colorScreen, ColorConversionCodes.GRAY2BGR);
            return colorScreen;
        }

        if (screen.Channels() == 1)
        {
            return screen.Clone();
        }

        var grayScreen = new Mat();
        Cv2.CvtColor(screen, grayScreen, ColorConversionCodes.BGR2GRAY);
        return grayScreen;
    }

    private static Mat BuildScaledTemplate(Mat template, double scale)
    {
        var width = Math.Max(1, (int)Math.Round(template.Width * scale));
        var height = Math.Max(1, (int)Math.Round(template.Height * scale));
        var scaledTemplate = new Mat();
        Cv2.Resize(template, scaledTemplate, new Size(width, height));
        return scaledTemplate;
    }

    private readonly record struct PilotAvatarCandidate(string Path, bool UsesColor);
}

internal readonly record struct PilotAvatarLocation(
    Rect Bounds,
    double Score);
