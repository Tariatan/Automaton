using Automaton.Core.Infrastructure;

namespace Automaton.Tests.Infrastructure;

public sealed class AvatarsDirectoryTests
{
    private const string UserNameEnvironmentVariable = "AUTOMATON_USER_NAME";

    [Fact]
    public void GetDirectory_ConfiguredRootAndUserName_ReturnsUserDirectory()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var avatarRoot = Path.Combine(workspace.Path, "avatars");
        var originalAvatarDirectory = UserSettings.Default.PilotAvatarDirectory;
        var originalUserName = Environment.GetEnvironmentVariable(UserNameEnvironmentVariable);

        try
        {
            UserSettings.Default.PilotAvatarDirectory = avatarRoot;
            Environment.SetEnvironmentVariable(UserNameEnvironmentVariable, "Drone 1");

            // Act
            var directory = AvatarsDirectory.GetDirectory();

            // Assert
            Assert.Equal(Path.Combine(avatarRoot, "Drone 1"), directory);
        }
        finally
        {
            UserSettings.Default.PilotAvatarDirectory = originalAvatarDirectory;
            Environment.SetEnvironmentVariable(UserNameEnvironmentVariable, originalUserName);
        }
    }

    [Fact]
    public void GetDirectory_ConfiguredRootWithoutUserName_ReturnsConfiguredRoot()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var avatarRoot = Path.Combine(workspace.Path, "avatars");
        var originalAvatarDirectory = UserSettings.Default.PilotAvatarDirectory;
        var originalUserName = Environment.GetEnvironmentVariable(UserNameEnvironmentVariable);

        try
        {
            UserSettings.Default.PilotAvatarDirectory = avatarRoot;
            Environment.SetEnvironmentVariable(UserNameEnvironmentVariable, "");

            // Act
            var directory = AvatarsDirectory.GetDirectory();

            // Assert
            Assert.Equal(avatarRoot, directory);
        }
        finally
        {
            UserSettings.Default.PilotAvatarDirectory = originalAvatarDirectory;
            Environment.SetEnvironmentVariable(UserNameEnvironmentVariable, originalUserName);
        }
    }
}
