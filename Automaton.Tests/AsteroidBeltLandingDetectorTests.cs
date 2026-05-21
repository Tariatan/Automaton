using Automaton.Detectors;

namespace Automaton.Tests;

public sealed class AsteroidBeltLandingDetectorTests
{
    [Fact]
    public void Analyze_LandedOnAsteroidBeltImage_DetectsAsteroidBeltLabelSuccessfully()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.CreateLandedOnAsteroidBeltImage();

        // Act
        var analysis = AsteroidBeltLandingDetector.Analyze(image);

        // Assert
        Assert.True(analysis.LandedOnAsteroidBelt);
    }

    [Fact]
    public void Analyze_WarpToAsteroidFieldImage_ReturnsNotFound()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.CreateWarpToAsteroidFieldImage();

        // Act
        var analysis = AsteroidBeltLandingDetector.Analyze(image);

        // Assert
        Assert.False(analysis.LandedOnAsteroidBelt);
    }

    [Fact]
    public void Analyze_WarpDriveActiveTextMentionsAsteroidBelt_ReturnsNotFound()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.CreateWarpDriveActiveImage();

        // Act
        var analysis = AsteroidBeltLandingDetector.Analyze(image);

        // Assert
        Assert.False(analysis.LandedOnAsteroidBelt);
    }

    [Fact]
    public void Analyze_LandedOnEmptyAsteroidBeltImage_DetectsNothingFound()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.CreateLandedOnEmptyAsteroidBeltImage();

        // Act
        var analysis = AsteroidBeltLandingDetector.Analyze(image);

        // Assert
        Assert.True(analysis.LandedOnAsteroidBelt);
    }
}
