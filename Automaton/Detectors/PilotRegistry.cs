using System.IO;

namespace Automaton.Detectors;

internal static class PilotRegistry
{
    private const string PilotFolderName = "pilot";
    private static readonly Lazy<int[]> CachedPilotIndices = new(ScanPilotIndices);

    public static bool TryGetNextPilotIndex(int currentPilotIndex, out int nextPilotIndex)
    {
        foreach (var pilotIndex in CachedPilotIndices.Value)
        {
            if (pilotIndex > currentPilotIndex)
            {
                nextPilotIndex = pilotIndex;
                return true;
            }
        }

        nextPilotIndex = currentPilotIndex;
        return false;
    }

    private static int[] ScanPilotIndices()
    {
        if (!Directory.Exists(PilotFolderName))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(PilotFolderName, "*.png", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Select(ParsePilotIndex)
            .Where(pilotIndex => pilotIndex > 0)
            .Distinct()
            .Order()
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
}
