using Automaton.Detectors;

namespace Automaton.Tests;

public sealed class AsteroidBeltOverviewDetectorTests
{
    [Fact]
    public void Analyze_OverviewWithAsteroidBelts_ReturnsControlsAndBeltRows()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.CreateWarpToAsteroidFieldImage();
        var detector = new AsteroidBeltOverviewDetector();

        // Act
        var analysis = detector.Analyze(image);

        // Assert
        Assert.True(analysis.OverviewLocated);
        Assert.NotNull(analysis.OverviewBounds);
        Assert.NotNull(analysis.OverviewBeltButtonBounds);
        Assert.Equal(18, analysis.AsteroidBelts.Count);
    }
}
