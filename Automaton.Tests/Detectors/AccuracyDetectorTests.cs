using Automaton.Detectors;
using OpenCvSharp;

namespace Automaton.Tests.Detectors;

public sealed class AccuracyDetectorTests
{
    [Theory]
    [InlineData("Discovery/active_playfield_single_cluster.png", 99.0, "99.0%")]
    public void Detect_ImageContainsAccuracy_ReturnsPercentage(string imagePath, double expectedPercentage, string expectedText)
    {
        // Arrange
        using var image = ScreenshotLoader.LoadOrSkip(imagePath);
        var detector = new AccuracyDetector();

        // Act
        var detection = detector.Detect(image);

        // Assert
        Assert.True(detection.IsFound, $"SearchBounds: {detection.SearchBounds}");
        Assert.Equal(expectedPercentage, detection.Percentage.GetValueOrDefault(), precision: 1);
        Assert.Equal(expectedText, detection.Text);
        Assert.NotNull(detection.TextBounds);
    }

    [Fact]
    public void Detect_ImageDoesNotContainAccuracy_ReturnsNotFound()
    {
        // Arrange
        using var image = new Mat(2008, 2551, MatType.CV_8UC3, Scalar.Black);
        var detector = new AccuracyDetector();

        // Act
        var detection = detector.Detect(image);

        // Assert
        Assert.False(detection.IsFound);
        Assert.Null(detection.Percentage);
        Assert.Equal(string.Empty, detection.Text);
    }

    [Fact]
    public void Detect_EmptyImage_ReturnsNotFound()
    {
        // Arrange
        using var image = new Mat();
        var detector = new AccuracyDetector();

        // Act
        var detection = detector.Detect(image);

        // Assert
        Assert.False(detection.IsFound);
        Assert.Null(detection.Percentage);
        Assert.Equal(string.Empty, detection.Text);
    }
}
