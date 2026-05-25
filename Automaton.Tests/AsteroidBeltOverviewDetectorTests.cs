using Automaton.Detectors;
using OpenCvSharp;
using System.IO;

namespace Automaton.Tests;

public sealed class AsteroidBeltOverviewDetectorTests
{
    [Fact]
    public void AnalyzeAndDrawDebugOverlay_OverviewWithAsteroidBelts_ReturnsControlsAndBeltRows()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.LoadWarpToAsteroidFieldImage();
        using var temporaryDirectory = new TemporaryDirectory();
        var imagePath = Path.Combine(temporaryDirectory.Path, "overview.png");
        Cv2.ImWrite(imagePath, image);
        var detector = new AsteroidBeltOverviewDetector();

        // Act
        var analysis = detector.AnalyzeAndDrawDebugOverlay(imagePath);

        // Assert
        Assert.True(analysis.OverviewLocated);
        Assert.NotNull(analysis.OverviewBounds);
        Assert.NotNull(analysis.OverviewBeltButtonBounds);
        Assert.NotNull(analysis.HomeStationBounds);
        Assert.Equal(18, analysis.AsteroidBelts.Count);
    }
}
