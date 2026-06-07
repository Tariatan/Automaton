using Automaton.Detectors;
using OpenCvSharp;

namespace Automaton.Tests.Detectors;

[Collection(CurrentDirectorySensitiveCollection.Name)]
public sealed class LoggedInPilotDetectorTests
{
    [Fact]
    public void Detect_PilotLoggedInScreenshot_ReturnsPilotTwo()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        WriteFocusedPilotAvatarTemplates(Path.Combine(workspace.Path, "pilot"), 1, 2, 3);
        using var image = SyntheticCommonImageFactory.LoadLoggedInPilotScreenImage();
        using var detector = new LoggedInPilotDetector();

        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            // Act
            var isDetected = detector.Detect(image, out var detection);

            // Assert
            Assert.True(isDetected, $"Score: {detection.Score}");
            Assert.Equal(2, detection.PilotIndex);
            Assert.Equal(new Rect(0, 48, 48, 48), detection.Bounds);
            Assert.True(detection.Score >= 0.84, $"Score: {detection.Score}");
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }
    }

    private static void WriteFocusedPilotAvatarTemplates(string pilotDirectory, params int[] pilotIndices)
    {
        Directory.CreateDirectory(pilotDirectory);

        foreach (var pilotIndex in pilotIndices)
        {
            using var focusedAvatar = SyntheticCommonImageFactory.LoadFocusedPilotAvatarImage(pilotIndex);
            Cv2.ImWrite(Path.Combine(pilotDirectory, $"{pilotIndex}_focused.png"), focusedAvatar);
        }
    }
}
