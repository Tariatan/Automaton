using Automaton.Detectors;
using OpenCvSharp;

namespace Automaton.Tests.Detectors;

public sealed class AccuracyDetectorTests
{
    [Fact]
    public void Detect_ImageContainsAccuracy_ReturnsPercentage()
    {
        // Arrange
        using var image = SyntheticDiscoveryImageFactory.LoadSingleClusterImage();

        // Act
        var detection = AccuracyDetector.Detect(image);

        // Assert
        Assert.True(detection.IsFound, $"SearchBounds: {detection.SearchBounds}");
        Assert.Equal(99.0, detection.Percentage.GetValueOrDefault(), precision: 1);
        Assert.Equal("99.0%", detection.Text);
        Assert.NotNull(detection.TextBounds);
    }

    [Fact]
    public void Detect_ImageDoesNotContainAccuracy_ReturnsNotFound()
    {
        // Arrange
        using var image = new Mat(2008, 2551, MatType.CV_8UC3, Scalar.Black);

        // Act
        var detection = AccuracyDetector.Detect(image);

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

        // Act
        var detection = AccuracyDetector.Detect(image);

        // Assert
        Assert.False(detection.IsFound);
        Assert.Null(detection.Percentage);
        Assert.Equal(string.Empty, detection.Text);
    }
}
