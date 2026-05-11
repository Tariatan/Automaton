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
        Assert.NotNull(analysis.AsteroidBeltLabelBounds);
        Assert.NotNull(analysis.MineOverviewBounds);
        Assert.Equal(5, analysis.Asteroids.Count);
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
        Assert.Null(analysis.AsteroidBeltLabelBounds);
        Assert.Null(analysis.MineOverviewBounds);
        Assert.Empty(analysis.Asteroids);
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
        Assert.Null(analysis.AsteroidBeltLabelBounds);
        Assert.Null(analysis.MineOverviewBounds);
        Assert.Empty(analysis.Asteroids);
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
        Assert.NotNull(analysis.MineOverviewBounds);
        Assert.Equal(5, analysis.Asteroids.Count);
        Assert.All(
            analysis.Asteroids,
            asteroid => Assert.True(asteroid.Bounds.Top - analysis.MineOverviewBounds!.Value.Top >= 120));
    }
}
