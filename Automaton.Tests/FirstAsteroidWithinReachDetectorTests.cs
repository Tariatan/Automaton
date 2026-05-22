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
        var telemetry = default(DistanceUnitDetectionTelemetry);

        // Act
        var mineOverviewLocated = mineOverviewDetector.TryLocate(image, out var mineOverviewBounds);
        var asteroids = mineOverviewLocated
            ? AsteroidRowsDetector.Locate(image, mineOverviewBounds)
            : [];
        var detected = mineOverviewLocated &&
                       asteroids.Count > 0 &&
                       detector.Detect(image, mineOverviewBounds, asteroids[0].Bounds, out telemetry);

        // Assert
        Assert.True(mineOverviewLocated);
        Assert.NotEmpty(asteroids);
        Assert.True(detected);
        Assert.True(telemetry.BestMetersScore >= 0.70);
        Assert.NotNull(telemetry.MatchedMetersScale);
        Assert.True(telemetry.IsMetersTemplateMatch);
    }
}
