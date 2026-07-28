namespace Automaton.Core.Infrastructure;

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
            var configuredDirectory = UserSettings.Default.PilotAvatarDirectory;
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
        UserSettings.Default.PilotAvatarDirectory = fullDirectory;
        UserSettings.Default.Save();
    }
}
