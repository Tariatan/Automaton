namespace Automaton.Infrastructure;

internal sealed record ApplicationStartupOptions(
    bool ProcessSamples,
    bool AutoStartAutomation,
    string? SettingsFilePath)
{
    public static ApplicationStartupOptions Parse(IEnumerable<string> arguments)
    {
        var args = arguments.ToArray();
        var processSamples = args.Contains("--process-samples", StringComparer.OrdinalIgnoreCase);
        var hasDiscoveryArgument = args.Any(IsDiscoveryArgument);
        var settingsFilePath = GetArgumentValue(args, "--settings-file");
        return new ApplicationStartupOptions(processSamples, hasDiscoveryArgument, settingsFilePath);
    }

    private static string? GetArgumentValue(string[] args, string name)
    {
        var index = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static bool IsDiscoveryArgument(string argument)
    {
        return string.Equals(argument, "-discovery", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(argument, "--discovery", StringComparison.OrdinalIgnoreCase);
    }
}