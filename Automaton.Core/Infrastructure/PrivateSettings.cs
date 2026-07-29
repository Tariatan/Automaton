namespace Automaton.Core.Infrastructure;

internal static class PrivateSettings
{
    private const string UserNameKey = "AUTOMATON_USER_NAME";
    private const string SettingsFilePathKey = "AUTOMATON_SETTINGS_FILE_PATH";
    public static string UserName => Environment.GetEnvironmentVariable(UserNameKey) ?? "";

    public static string SettingsFilePath => Environment.GetEnvironmentVariable(SettingsFilePathKey) ?? "";

    public static void SetUserName(string value) => Persist(UserNameKey, value);

    public static void SetSettingsFilePath(string value) => Persist(SettingsFilePathKey, value);

    private static void Persist(string name, string value)
    {
        if (OperatingSystem.IsWindows())
        {
            Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.User);
        }
    }
}