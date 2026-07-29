namespace Automaton.Core.Infrastructure;

internal static class AvatarsDirectory
{
    private const string DefaultFolderName = "avatars";

    public static string GetDirectory()
    {
        var configured = UserSettings.Default.PilotAvatarDirectory;
        if (string.IsNullOrWhiteSpace(configured))
            return DefaultFolderName;

        var userName = PrivateSettings.UserName;
        return string.IsNullOrWhiteSpace(userName) ? configured : Path.Combine(configured, userName);
    }
}
