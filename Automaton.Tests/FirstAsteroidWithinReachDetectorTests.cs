using Automaton.Detectors;
using OpenCvSharp;
using System.IO;

namespace Automaton.Tests;

public sealed class FirstAsteroidWithinReachDetectorTests
{
    [Fact]
    public void Detect_LandedOnAsteroidBeltImageWithMetersDistance_ReturnsTrue()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.LoadLandedOnAsteroidBeltImageWithMetersDistance();
        using var temporaryDirectory = new TemporaryDirectory();
        var imagePath = Path.Combine(temporaryDirectory.Path, "mine-overview.png");
        Cv2.ImWrite(imagePath, image);
        var mineOverviewDetector = new MineOverviewDetector();
        var detector = new FirstAsteroidWithinReachDetector();
        var telemetry = default(DistanceUnitDetectionTelemetry);

        // Act
        var mineOverviewAnalysis = mineOverviewDetector.AnalyzeAndDrawDebugOverlay(imagePath);
        var asteroids = mineOverviewAnalysis.MineOverviewLocated && mineOverviewAnalysis.MineOverviewBounds is not null
            ? AsteroidRowsDetector.Locate(image, mineOverviewAnalysis.MineOverviewBounds.Value)
            : [];
        var detected = mineOverviewAnalysis.MineOverviewLocated &&
                       mineOverviewAnalysis.MineOverviewBounds is not null &&
                       asteroids.Count > 0 &&
                       detector.Detect(image, mineOverviewAnalysis.MineOverviewBounds.Value, asteroids[0].Bounds, out telemetry);

        // Assert
        Assert.True(mineOverviewAnalysis.MineOverviewLocated);
        Assert.NotEmpty(asteroids);
        Assert.True(detected);
        Assert.True(telemetry.BestMetersScore >= 0.70);
        Assert.NotNull(telemetry.MatchedMetersScale);
        Assert.True(telemetry.IsMetersTemplateMatch);
    }
}
