using System.IO;

namespace Automaton.Infrastructure;

internal static class PilotAvatarDirectory
{
    private const string DefaultFolderName = "pilot";

    public static string GetDirectory()
    {
        return GetConfiguredDirectory() ?? DefaultFolderName;
    }

    public static string? GetConfiguredDirectory()
    {
        try
        {
            var configuredDirectory = Properties.Settings.Default.PilotAvatarDirectory;
            return string.IsNullOrWhiteSpace(configuredDirectory) ? null : configuredDirectory;
        }
        catch (Exception) when (!OperatingSystem.IsWindows())
        {
            return null;
        }
    }

    public static void SetConfiguredDirectory(string directory)
    {
        var fullDirectory = Path.GetFullPath(directory);
        Properties.Settings.Default.PilotAvatarDirectory = fullDirectory;
        Properties.Settings.Default.Save();
    }
}
