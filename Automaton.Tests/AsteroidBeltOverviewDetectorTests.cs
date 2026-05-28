using Automaton.Detectors;

namespace Automaton.Tests;

public sealed class AsteroidBeltOverviewDetectorTests
{
    [Fact]
    public void Detect_OverviewWithAsteroidBelts_ReturnsControlsAndBeltRows()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.LoadWarpToAsteroidFieldImage();
        using var detector = new AsteroidBeltOverviewDetector();

        // Act
        var analysis = detector.Detect(image);

        // Assert
        Assert.True(analysis.OverviewLocated);
        Assert.NotNull(analysis.OverviewBounds);
        Assert.NotNull(analysis.OverviewBeltButtonBounds);
        Assert.NotNull(analysis.HomeStationBounds);
        Assert.Equal(18, analysis.AsteroidBelts.Count);
    }
}
