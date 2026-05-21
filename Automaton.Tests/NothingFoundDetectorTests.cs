using Automaton.Detectors;
using OpenCvSharp;

namespace Automaton.Tests;

public sealed class NothingFoundDetectorTests
{
    private static readonly Rect MineOverviewBounds = new(1778, 1339, 180, 420);

    [Fact]
    public void Detect_LandedOnEmptyAsteroidBeltImage_ReturnsTrue()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.CreateLandedOnEmptyAsteroidBeltImage();

        // Act
        var detected = NothingFoundDetector.Detect(image, MineOverviewBounds);

        // Assert
        Assert.True(detected);
    }

    [Fact]
    public void Detect_LandedOnAsteroidBeltImage_ReturnsFalse()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.CreateLandedOnAsteroidBeltImage();

        // Act
        var detected = NothingFoundDetector.Detect(image, MineOverviewBounds);

        // Assert
        Assert.False(detected);
    }
}
