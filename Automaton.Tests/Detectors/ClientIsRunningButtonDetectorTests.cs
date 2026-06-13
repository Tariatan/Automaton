using Automaton.Detectors;
using Automaton.Infrastructure;
using OpenCvSharp;

namespace Automaton.Tests.Detectors;

public sealed class ClientIsRunningButtonDetectorTests
{
    [Fact]
    public void Detect_ClientIsRunningButtonAtPlayNowButtonLocation_ReturnsLocation()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        using var playButtonScreen = SyntheticCommonImageFactory.LoadPlayButtonScreenImage();
        using var playNowButtonDetector = new PlayNowButtonDetector();
        var playButtonScreenPath = Path.Combine(workspace.Path, "play-button-screen.png");
        Cv2.ImWrite(playButtonScreenPath, playButtonScreen);
        Assert.True(playNowButtonDetector.Detect(playButtonScreenPath, out var playNowButtonLocation));

        using var clientIsRunningButton = EmbeddedResourceLoader.LoadMat("client_is_running.png");
        using var screen = new Mat(playButtonScreen.Size(), MatType.CV_8UC3, Scalar.Black);
        var expectedBounds = new Rect(
            playNowButtonLocation.Bounds.X,
            playNowButtonLocation.Bounds.Y,
            clientIsRunningButton.Width,
            clientIsRunningButton.Height);
        using (var region = new Mat(screen, expectedBounds))
        {
            clientIsRunningButton.CopyTo(region);
        }

        var screenPath = Path.Combine(workspace.Path, "client-is-running-screen.png");
        Cv2.ImWrite(screenPath, screen);
        using var detector = new ClientIsRunningButtonDetector();

        // Act
        var isDetected = detector.Detect(screenPath, out var location);

        // Assert
        Assert.True(isDetected, $"Score: {location.Score}");
        Assert.Equal(expectedBounds, location.Bounds);
        Assert.True(location.Score >= 0.95, $"Score: {location.Score}");
    }
}
