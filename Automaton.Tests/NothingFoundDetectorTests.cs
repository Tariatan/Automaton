using Automaton.Detectors;

namespace Automaton.Tests;

public sealed class NothingFoundDetectorTests
{
    private static readonly OpenCvSharp.Rect MineOverviewBounds = new(1700, 1226, 270, 336);

    [Fact]
    public void Detect_LandedOnEmptyAsteroidBeltImage_ReturnsTrue()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.CreateLandedOnEmptyAsteroidBeltImage();
        var nothingFoundDetector = new NothingFoundDetector();

        // Act
        var detected = nothingFoundDetector.Detect(image, MineOverviewBounds);

        // Assert
        Assert.True(detected);
    }

    [Fact]
    public void Detect_LandedOnAsteroidBeltImage_ReturnsFalse()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.CreateLandedOnAsteroidBeltImage();
        var nothingFoundDetector = new NothingFoundDetector();

        // Act
        var detected = nothingFoundDetector.Detect(image, MineOverviewBounds);

        // Assert
        Assert.False(detected);
    }
}
