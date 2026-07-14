using System.IO;
using Automaton.Infrastructure;

namespace Automaton.Detectors;

internal static class PilotRegistry
{
    public static bool TryGetNextPilotIndex(int currentPilotIndex, out int nextPilotIndex)
    {
        foreach (var pilotIndex in ScanPilotIndices())
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
        var pilotDirectory = Path.GetFullPath(PilotAvatarDirectory.GetDirectory());
        if (!Directory.Exists(pilotDirectory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(pilotDirectory, "*.png", SearchOption.TopDirectoryOnly)
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
