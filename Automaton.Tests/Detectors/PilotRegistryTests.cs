using Automaton.Detectors;
using Automaton.Infrastructure;
using Automaton.Properties;

namespace Automaton.Tests.Detectors;

[Collection(CurrentDirectorySensitiveCollection.Name)]
public sealed class PilotRegistryTests
{
    [Fact]
    public void TryGetNextPilotIndex_ConfiguredPilotAvatarDirectoryContainsHigherPilot_ReturnsNextPilotIndex()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var pilotDirectory = Path.Combine(workspace.Path, "avatars");
        Directory.CreateDirectory(pilotDirectory);
        File.WriteAllText(Path.Combine(pilotDirectory, "1.png"), string.Empty);
        File.WriteAllText(Path.Combine(pilotDirectory, "2_focused.png"), string.Empty);
        var originalDirectory = Settings.Default.PilotAvatarDirectory;

        try
        {
            PilotAvatarDirectory.SetConfiguredDirectory(pilotDirectory);

            // Act
            var hasNextPilot = PilotRegistry.TryGetNextPilotIndex(1, out var nextPilotIndex);

            // Assert
            Assert.True(hasNextPilot);
            Assert.Equal(2, nextPilotIndex);
        }
        finally
        {
            Settings.Default.PilotAvatarDirectory = originalDirectory;
            Settings.Default.Save();
        }
    }
}
