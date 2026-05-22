using System.IO;
using Automaton.Properties;

namespace Automaton.Infrastructure;

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
        return !string.IsNullOrWhiteSpace(hallmarkRootDirectory)
            ? Path.Combine(hallmarkRootDirectory, ExpectedFolderName)
            : BuildDirectoryPath(ExpectedFolderName);
    }

    public static string? GetConfiguredRootDirectory()
    {
        try
        {
            var configuredRootDirectory = Settings.Default.TelemetryRootDirectory;
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
        Settings.Default.TelemetryRootDirectory = fullRootDirectory;
        Settings.Default.Save();
    }

    public static string? GetConfiguredHallmarkRootDirectory()
    {
        try
        {
            var configuredRootDirectory = Settings.Default.HallmarkRootDirectory;
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
        Settings.Default.HallmarkRootDirectory = fullRootDirectory;
        Settings.Default.Save();
    }

    private static string BuildDirectoryPath(string folderName)
    {
        var rootDirectory = GetConfiguredRootDirectory();
        return string.IsNullOrWhiteSpace(rootDirectory) ? folderName : Path.Combine(rootDirectory, folderName);
    }
}
