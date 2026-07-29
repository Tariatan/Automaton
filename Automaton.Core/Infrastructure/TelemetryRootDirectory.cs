using Automaton.Core.Primitives;

namespace Automaton.Core.Infrastructure;

internal static class TelemetryRootDirectory
{
    public static string GetCapturesDirectory() => Path.Combine(GetTelemetryRoot(), Config.CapturesFolderName);

    public static string GetLogsDirectory() => Path.Combine(GetTelemetryRoot(), Config.LogsFolderName);

    public static string GetTemplatesDirectory(string folderName) => Path.Combine(GetTemplatesRoot(), folderName);

    private static string GetTelemetryRoot()
    {
        var rootBase = UserSettings.Default.TelemetryRootBase;
        if (string.IsNullOrWhiteSpace(rootBase))
            return Directory.GetCurrentDirectory();

        var userName = PrivateSettings.UserName;
        return string.IsNullOrWhiteSpace(userName) ? rootBase : Path.Combine(rootBase, userName);
    }

    private static string GetTemplatesRoot()
    {
        var configured = UserSettings.Default.TemplatesDirectory;
        return string.IsNullOrWhiteSpace(configured) ? Directory.GetCurrentDirectory() : configured;
    }
}