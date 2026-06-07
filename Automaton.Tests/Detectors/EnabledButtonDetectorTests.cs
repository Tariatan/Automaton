using Automaton.Detectors;
using OpenCvSharp;

namespace Automaton.Tests.Detectors;

public sealed class EnabledButtonDetectorTests
{
    [Fact]
    public void Detect_OverlapImage_ReturnsNotFound()
    {
        // Arrange
        using var image = ScreenshotLoader.LoadOrSkip("Discovery/overlap.png");
        var playfieldBounds = DetectPlayfieldBounds(image);

        // Act
        var detection = EnabledButtonDetector.Detect(image, playfieldBounds);

        // Assert
        Assert.False(detection.IsFound, $"Score: {detection.Score}, HSV: {detection.HsvDistance}");
    }

    [Fact]
    public void Detect_SubmitEnabledImage_ReturnsFound()
    {
        // Arrange
        using var image = ScreenshotLoader.LoadOrSkip("Discovery/submit_enabled.png");
        var playfieldBounds = DetectPlayfieldBounds(image);

        // Act
        var detection = EnabledButtonDetector.Detect(image, playfieldBounds);

        // Assert
        Assert.True(detection.IsFound, $"Score: {detection.Score}, HSV: {detection.HsvDistance}");
        Assert.NotNull(detection.ButtonBounds);
    }

    private static Rect DetectPlayfieldBounds(Mat image)
    {
        using var detector = new PlayfieldDetector();
        var detection = detector.Detect(image);
        Assert.True(detection.IsFound);
        return detection.Bounds;
    }
}
