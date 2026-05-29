using Automaton.Detectors;

namespace Automaton.Tests;

public sealed class FirstAsteroidWithinReachDetectorTests
{
    [Fact]
    public void Detect_LandedOnAsteroidBeltImageWithMetersDistance_ReturnsTrue()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.LoadLandedOnAsteroidBeltImageWithMetersDistance();
        var mineOverviewDetector = new MineOverviewDetector();
        var detector = new FirstAsteroidWithinReachDetector();

        // Act
        var mineOverviewAnalysis = mineOverviewDetector.Detect(image);
        var asteroids = mineOverviewAnalysis.MineOverviewLocated && mineOverviewAnalysis.MineOverviewBounds is not null
            ? AsteroidRowsDetector.Detect(image, mineOverviewAnalysis.MineOverviewBounds.Value)
            : [];
        var analysis = mineOverviewAnalysis.MineOverviewLocated &&
                       mineOverviewAnalysis.MineOverviewBounds is not null &&
                       asteroids.Count > 0
            ? detector.Detect(image, mineOverviewAnalysis.MineOverviewBounds.Value, asteroids[0].Bounds)
            : FirstAsteroidWithinReachAnalysis.NotFound;

        // Assert
        Assert.True(mineOverviewAnalysis.MineOverviewLocated);
        Assert.NotEmpty(asteroids);
        Assert.True(analysis.IsWithinReach);
        Assert.True(analysis.BestScore >= 0.70);
        Assert.NotNull(analysis.MatchedScale);
    }
}
