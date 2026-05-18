using Automaton.Detectors;

namespace Automaton.Tests;

public sealed class AsteroidBeltLandingDetectorTests
{
    [Fact]
    public void Analyze_LandedOnAsteroidBeltImage_ReturnsLabelMineOverviewAndAsteroidRows()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.CreateLandedOnAsteroidBeltImage();
        var detector = new AsteroidBeltLandingDetector();

        // Act
        var analysis = detector.Analyze(image);

        // Assert
        Assert.True(analysis.LandedOnAsteroidBelt);
    }

    [Fact]
    public void Analyze_WarpToAsteroidFieldImage_ReturnsNotFound()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.CreateWarpToAsteroidFieldImage();
        var detector = new AsteroidBeltLandingDetector();

        // Act
        var analysis = detector.Analyze(image);

        // Assert
        Assert.False(analysis.LandedOnAsteroidBelt);
    }

    [Fact]
    public void Analyze_WarpDriveActiveTextMentionsAsteroidBelt_ReturnsNotFound()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.CreateWarpDriveActiveImage();
        var detector = new AsteroidBeltLandingDetector();

        // Act
        var analysis = detector.Analyze(image);

        // Assert
        Assert.False(analysis.LandedOnAsteroidBelt);
    }

    [Fact]
    public void Analyze_MineOverviewContainsHeaderLikeIcon_IgnoresHeaderAndKeepsAsteroidRows()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.CreateLandedOnAsteroidBeltImageWithMineHeaderLikeIcon();
        var detector = new AsteroidBeltLandingDetector();

        // Act
        var analysis = detector.Analyze(image);

        // Assert
        Assert.True(analysis.LandedOnAsteroidBelt);
    }

    [Fact]
    public void Analyze_LandedOnEmptyAsteroidBeltImage_DetectsNothingFound()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.CreateLandedOnEmptyAsteroidBeltImage();
        var detector = new AsteroidBeltLandingDetector();

        // Act
        var analysis = detector.Analyze(image);

        // Assert
        Assert.True(analysis.LandedOnAsteroidBelt);
    }

    [Fact]
    public void Analyze_LandedOnAsteroidBeltImageWithMetersDistance_DetectsMetersUnit()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.CreateLandedOnAsteroidBeltImageWithMetersDistance();
        var detector = new AsteroidBeltLandingDetector();

        // Act
        var analysis = detector.Analyze(image);

        // Assert
        Assert.True(analysis.LandedOnAsteroidBelt);
    }
}
