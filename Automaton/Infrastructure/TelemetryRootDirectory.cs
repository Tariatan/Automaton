using System.IO;

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

    public static string GetTrainingDirectory()
    {
        return BuildDirectoryPath(Primitives.Settings.TrainingFolderName);
    }

    public static string GetExpectedDirectory()
    {
        var hallmarkRootDirectory = GetConfiguredHallmarkRootDirectory();
        return !string.IsNullOrWhiteSpace(hallmarkRootDirectory)
            ? Path.Combine(hallmarkRootDirectory, Primitives.Settings.ProjectDiscoveryExpectedFolderName)
            : BuildDirectoryPath(Primitives.Settings.ProjectDiscoveryExpectedFolderName);
    }

    public static string? GetConfiguredRootDirectory()
    {
        try
        {
            var configuredRootDirectory = Properties.Settings.Default.TelemetryRootDirectory;
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
        Properties.Settings.Default.TelemetryRootDirectory = fullRootDirectory;
        Properties.Settings.Default.Save();
    }

    public static string? GetConfiguredHallmarkRootDirectory()
    {
        try
        {
            var configuredRootDirectory = Properties.Settings.Default.HallmarkRootDirectory;
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
        Properties.Settings.Default.HallmarkRootDirectory = fullRootDirectory;
        Properties.Settings.Default.Save();
    }

    private static string BuildDirectoryPath(string folderName)
    {
        var rootDirectory = GetConfiguredRootDirectory();
        return string.IsNullOrWhiteSpace(rootDirectory) ? folderName : Path.Combine(rootDirectory, folderName);
    }
}
