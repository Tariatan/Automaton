namespace Automaton.Infrastructure;

internal static class TelemetryRootDirectory
{

    public static string GetCapturesDirectory()
    {
        return BuildDirectoryPath(Primitives.Settings.CapturesFolderName);
    }

    public static string GetLogsDirectory()
    {
        return BuildDirectoryPath(Primitives.Settings.LogsFolderName);
    }

    public static string GetExpectedDirectory(string folderName)
    {
        var hallmarkRootDirectory = GetConfiguredHallmarkRootDirectory();
        return !string.IsNullOrWhiteSpace(hallmarkRootDirectory)
            ? Path.Combine(hallmarkRootDirectory, folderName)
            : BuildDirectoryPath(folderName);
    }

    public static string? GetConfiguredRootDirectory()
    {
        try
        {
            var configuredRootDirectory = UserSettings.Default.TelemetryRootDirectory;
            return string.IsNullOrWhiteSpace(configuredRootDirectory) ? null : configuredRootDirectory;
        }
        catch (Exception) when (!OperatingSystem.IsWindows())
        {
            return null;
        }
    }

    public static void SetConfiguredRootDirectory(string rootDirectory)
    {
        var fullRootDirectory = Path.GetFullPath(rootDirectory);
        UserSettings.Default.TelemetryRootDirectory = fullRootDirectory;
        UserSettings.Default.Save();
    }

    public static string? GetConfiguredHallmarkRootDirectory()
    {
        try
        {
            var configuredRootDirectory = UserSettings.Default.HallmarkRootDirectory;
            return string.IsNullOrWhiteSpace(configuredRootDirectory) ? null : configuredRootDirectory;
        }
        catch (Exception) when (!OperatingSystem.IsWindows())
        {
            return null;
        }
    }

    public static void SetConfiguredHallmarkRootDirectory(string rootDirectory)
    {
        var fullRootDirectory = Path.GetFullPath(rootDirectory);
        UserSettings.Default.HallmarkRootDirectory = fullRootDirectory;
        UserSettings.Default.Save();
    }

    private static string BuildDirectoryPath(string folderName)
    {
        var rootDirectory = GetConfiguredRootDirectory();
        return string.IsNullOrWhiteSpace(rootDirectory) ? folderName : Path.Combine(rootDirectory, folderName);
    }
}
