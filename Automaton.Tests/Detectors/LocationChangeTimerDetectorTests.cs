using Automaton.Detectors;

namespace Automaton.Tests.Detectors;

public sealed class LocationChangeTimerDetectorTests
{
    [Fact]
    public void TryLocate_UndockedCompleteImage_ReturnsLocationChangeTimer()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.LoadUndockedCompleteImage();
        var locator = new LocationChangeTimerDetector();

        // Act
        var located = locator.Detect(image, out var location);

        // Assert
        Assert.True(located);
        Assert.InRange(location.Bounds.X, 140, 170);
        Assert.InRange(location.Bounds.Y, 60, 90);
    }

    [Fact]
    public void TryLocate_UndockedImageWithoutTimer_ReturnsFalse()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.LoadUndockedWithoutLocationChangeTimerImage();
        var locator = new LocationChangeTimerDetector();

        // Act
        var located = locator.Detect(image, out _);

        // Assert
        Assert.False(located);
    }
}
