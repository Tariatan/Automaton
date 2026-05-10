using System.IO;
using Automaton.Properties;

namespace Automaton;

internal static class TelemetryRootDirectory
{
    private const string CapturesFolderName = "captures";
    private const string LogsFolderName = "logs";
    private const string ExpectedFolderName = "expected";

    public static string GetCapturesDirectory()
    {
        return BuildDirectoryPath(CapturesFolderName);
    }

    public static string GetLogsDirectory()
    {
        return BuildDirectoryPath(LogsFolderName);
    }

    public static string GetExpectedDirectory()
    {
        var hallmarkRootDirectory = GetConfiguredHallmarkRootDirectory();
        if (!string.IsNullOrWhiteSpace(hallmarkRootDirectory))
        {
            return Path.Combine(hallmarkRootDirectory, ExpectedFolderName);
        }

        return BuildDirectoryPath(ExpectedFolderName);
    }

    public static string? GetConfiguredRootDirectory()
    {
        var configuredRootDirectory = Settings.Default.TelemetryRootDirectory;
        if (string.IsNullOrWhiteSpace(configuredRootDirectory))
        {
            return null;
        }

        return configuredRootDirectory;
    }

    public static void SetConfiguredRootDirectory(string rootDirectory)
    {
        var fullRootDirectory = Path.GetFullPath(rootDirectory);
        Settings.Default.TelemetryRootDirectory = fullRootDirectory;
        Settings.Default.Save();
    }

    public static string? GetConfiguredHallmarkRootDirectory()
    {
        var configuredRootDirectory = Settings.Default.HallmarkRootDirectory;
        if (string.IsNullOrWhiteSpace(configuredRootDirectory))
        {
            return null;
        }

        return configuredRootDirectory;
    }

    public static void SetConfiguredHallmarkRootDirectory(string rootDirectory)
    {
        var fullRootDirectory = Path.GetFullPath(rootDirectory);
        Settings.Default.HallmarkRootDirectory = fullRootDirectory;
        Settings.Default.Save();
    }

    private static string BuildDirectoryPath(string folderName)
    {
        var rootDirectory = GetConfiguredRootDirectory();
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            return folderName;
        }

        return Path.Combine(rootDirectory, folderName);
    }
}
