using Automaton.Core.Infrastructure;
using Automaton.Detectors;

namespace Automaton.Tests.Detectors;

[Collection(CurrentDirectorySensitiveCollection.Name)]
public sealed class PilotRegistryTests
{
    [Fact]
    public void TryGetNextPilotIndex_ConfiguredPilotAvatarDirectoryContainsHigherPilot_ReturnsNextPilotIndex()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var pilotRoot = Path.Combine(workspace.Path, "avatars");
        var originalDirectory = UserSettings.Default.PilotAvatarDirectory;

        try
        {
            UserSettings.Default.PilotAvatarDirectory = pilotRoot;
            var pilotDirectory = AvatarsDirectory.GetDirectory();
            Directory.CreateDirectory(pilotDirectory);
            File.WriteAllText(Path.Combine(pilotDirectory, "1.png"), string.Empty);
            File.WriteAllText(Path.Combine(pilotDirectory, "2_focused.png"), string.Empty);

            // Act
            var hasNextPilot = PilotRegistry.TryGetNextPilotIndex(1, out var nextPilotIndex);

            // Assert
            Assert.True(hasNextPilot);
            Assert.Equal(2, nextPilotIndex);
        }
        finally
        {
            UserSettings.Default.PilotAvatarDirectory = originalDirectory;
        }
    }
}
