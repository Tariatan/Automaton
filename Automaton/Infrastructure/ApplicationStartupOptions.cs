namespace Automaton.Infrastructure;

internal sealed record ApplicationStartupOptions(
    bool ProcessSamples,
    ApplicationAutomationMode AutomationMode,
    bool AutoStartAutomation)
{
    public static ApplicationStartupOptions Parse(IEnumerable<string> arguments)
    {
        var normalizedArguments = arguments.ToArray();
        var processSamples = normalizedArguments.Contains("--process-samples", StringComparer.OrdinalIgnoreCase);
        var hasMinerArgument = normalizedArguments.Any(IsMinerArgument);
        var hasDiscoveryArgument = normalizedArguments.Any(IsDiscoveryArgument);
        var automationMode = hasMinerArgument
            ? ApplicationAutomationMode.Mining
            : ApplicationAutomationMode.ProjectDiscovery;
        var autoStartAutomation = hasMinerArgument || hasDiscoveryArgument;

        return new ApplicationStartupOptions(processSamples, automationMode, autoStartAutomation);
    }

    private static bool IsMinerArgument(string argument)
    {
        return string.Equals(argument, "-miner", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(argument, "--miner", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDiscoveryArgument(string argument)
    {
        return string.Equals(argument, "-discovery", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(argument, "--discovery", StringComparison.OrdinalIgnoreCase);
    }
}

public enum ApplicationAutomationMode
{
    ProjectDiscovery,
    Mining
}
