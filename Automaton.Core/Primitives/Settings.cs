namespace Automaton.Primitives;

public static class Settings
{
    public const string CapturesFolderName = "captures";
    public const string LogsFolderName = "logs";

    public const long HideUiFileSizeThreshold = 1024 * 1024 * 2;

    public const int MaxLoginAttempts = 3;
    public const int MaximumStartingGameTransitionsBeforeReboot = 5;

    public const int DetectionRetryAttempts = 2;
    public const int DetectionRetryDelayMs = 500;
}