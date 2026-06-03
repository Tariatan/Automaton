using Automaton.Detectors;
using OpenCvSharp;

namespace Automaton.Tests.Detectors;

public sealed class NothingFoundDetectorTests
{
    private static readonly Rect MineOverviewBounds = new(1778, 1339, 180, 420);
    private readonly WarOverviewDetector m_WarOverviewDetector = new();

    [Fact]
    public void Detect_LandedOnEmptyAsteroidBeltImage_ReturnsTrue()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.LoadLandedOnEmptyAsteroidBeltImage();

        // Act
        var detected = NothingFoundDetector.Detect(image, MineOverviewBounds);

        // Assert
        Assert.True(detected);
    }

    [Fact]
    public void Detect_LandedOnAsteroidBeltImage_ReturnsFalse()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.LoadLandedOnAsteroidBeltImage();

        // Act
        var detected = NothingFoundDetector.Detect(image, MineOverviewBounds);

        // Assert
        Assert.False(detected);
    }

    [Fact]
    public void Detect_MiningGtfoImageWarOverview_ReturnsFalse()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.LoadMiningGtfoImage();
        var warAnalysis = m_WarOverviewDetector.Detect(image);

        // Act
        var detected = warAnalysis is { WarOverviewLocated: true, WarOverviewBounds: not null }
                       && NothingFoundDetector.Detect(image, warAnalysis.WarOverviewBounds.Value);

        // Assert
        Assert.True(warAnalysis.WarOverviewLocated);
        Assert.False(detected);
    }
}
