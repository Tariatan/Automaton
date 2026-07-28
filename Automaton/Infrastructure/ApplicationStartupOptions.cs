namespace Automaton.Infrastructure;

internal sealed record ApplicationStartupOptions(
    bool ProcessSamples,
    bool AutoStartAutomation)
{
    public static ApplicationStartupOptions Parse(IEnumerable<string> arguments)
    {
        var normalizedArguments = arguments.ToArray();
        var processSamples = normalizedArguments.Contains("--process-samples", StringComparer.OrdinalIgnoreCase);
        var hasDiscoveryArgument = normalizedArguments.Any(IsDiscoveryArgument);
        return new ApplicationStartupOptions(processSamples, hasDiscoveryArgument);
    }

    private static bool IsDiscoveryArgument(string argument)
    {
        return string.Equals(argument, "-discovery", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(argument, "--discovery", StringComparison.OrdinalIgnoreCase);
    }
}