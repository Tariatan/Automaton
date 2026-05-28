using Automaton.Detectors;

namespace Automaton.Tests;

public sealed class AsteroidBeltLandingDetectorTests
{
    [Fact]
    public void Analyze_LandedOnAsteroidBeltImage_DetectsAsteroidBeltLabelSuccessfully()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.LoadLandedOnAsteroidBeltImage();

        // Act
        var analysis = AsteroidBeltLandingDetector.Detect(image);

        // Assert
        Assert.True(analysis.LandedOnAsteroidBelt);
    }

    [Fact]
    public void Analyze_WarpToAsteroidFieldImage_ReturnsNotFound()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.LoadWarpToAsteroidFieldImage();

        // Act
        var analysis = AsteroidBeltLandingDetector.Detect(image);

        // Assert
        Assert.False(analysis.LandedOnAsteroidBelt);
    }

    [Fact]
    public void Analyze_WarpDriveActiveTextMentionsAsteroidBelt_ReturnsNotFound()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.LoadWarpDriveActiveImage();

        // Act
        var analysis = AsteroidBeltLandingDetector.Detect(image);

        // Assert
        Assert.False(analysis.LandedOnAsteroidBelt);
    }

    [Fact]
    public void Analyze_LandedOnEmptyAsteroidBeltImage_DetectsNothingFound()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.LoadLandedOnEmptyAsteroidBeltImage();

        // Act
        var analysis = AsteroidBeltLandingDetector.Detect(image);

        // Assert
        Assert.True(analysis.LandedOnAsteroidBelt);
    }
}
