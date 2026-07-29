namespace Automaton.Core.Infrastructure;

internal static class PilotAvatarDirectory
{
    private const string DefaultFolderName = "pilot";

    public static string GetDirectory()
    {
        var configured = UserSettings.Default.PilotAvatarDirectory;
        return string.IsNullOrWhiteSpace(configured) ? DefaultFolderName : configured;
    }
}