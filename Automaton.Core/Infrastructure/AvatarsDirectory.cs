namespace Automaton.Core.Infrastructure;

internal static class AvatarsDirectory
{
    private const string DefaultFolderName = "avatars";

    public static string GetDirectory()
    {
        var configured = UserSettings.Default.PilotAvatarDirectory;
        return string.IsNullOrWhiteSpace(configured) ? DefaultFolderName : configured;
    }
}