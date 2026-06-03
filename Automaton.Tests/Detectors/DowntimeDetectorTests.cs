using Automaton.Detectors;

namespace Automaton.Tests.Detectors;

public sealed class DowntimeDetectorTests
{
    [Fact]
    public void IsDowntimeImminent_WithinThresholdBeforeDowntime_ReturnsTrue()
    {
        // Arrange
        var detector = new DowntimeDetector(new TimeOnly(11, 0), TimeSpan.FromMinutes(20));
        var currentTime = new DateTime(2026, 5, 3, 10, 45, 0, DateTimeKind.Utc);

        // Act
        var isImminent = detector.IsDowntimeImminent(currentTime);

        // Assert
        Assert.True(isImminent);
    }

    [Fact]
    public void IsDowntimeImminent_ExactlyAtThresholdBoundary_ReturnsTrue()
    {
        // Arrange
        var detector = new DowntimeDetector(new TimeOnly(11, 0), TimeSpan.FromMinutes(20));
        var currentTime = new DateTime(2026, 5, 3, 10, 40, 0, DateTimeKind.Utc);

        // Act
        var isImminent = detector.IsDowntimeImminent(currentTime);

        // Assert
        Assert.True(isImminent);
    }

    [Fact]
    public void IsDowntimeImminent_OutsideThreshold_ReturnsFalse()
    {
        // Arrange
        var detector = new DowntimeDetector(new TimeOnly(11, 0), TimeSpan.FromMinutes(20));
        var currentTime = new DateTime(2026, 5, 3, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var isImminent = detector.IsDowntimeImminent(currentTime);

        // Assert
        Assert.False(isImminent);
    }

    [Fact]
    public void IsDowntimeImminent_WellBeforeDowntime_ReturnsFalse()
    {
        // Arrange
        var detector = new DowntimeDetector(new TimeOnly(11, 0), TimeSpan.FromMinutes(20));
        var currentTime = new DateTime(2026, 5, 3, 8, 0, 0, DateTimeKind.Utc);

        // Act
        var isImminent = detector.IsDowntimeImminent(currentTime);

        // Assert
        Assert.False(isImminent);
    }

    [Fact]
    public void IsDowntimeImminent_AfterDowntimeToday_ChecksNextDayDowntime()
    {
        // Arrange
        var detector = new DowntimeDetector(new TimeOnly(11, 0), TimeSpan.FromMinutes(20));
        var currentTime = new DateTime(2026, 5, 3, 14, 0, 0, DateTimeKind.Utc);

        // Act
        var isImminent = detector.IsDowntimeImminent(currentTime);

        // Assert
        Assert.False(isImminent);
    }

    [Fact]
    public void IsDowntimeImminent_LateNightBeforeNextDayDowntime_ReturnsFalse()
    {
        // Arrange
        var detector = new DowntimeDetector(new TimeOnly(11, 0), TimeSpan.FromMinutes(20));
        var currentTime = new DateTime(2026, 5, 3, 23, 0, 0, DateTimeKind.Utc);

        // Act
        var isImminent = detector.IsDowntimeImminent(currentTime);

        // Assert
        Assert.False(isImminent);
    }

    [Fact]
    public void IsDowntimeImminent_DefaultConstructor_Uses1100UtcAnd20Minutes()
    {
        // Arrange
        var detector = new DowntimeDetector();
        var withinThreshold = new DateTime(2026, 5, 3, 10, 45, 0, DateTimeKind.Utc);
        var outsideThreshold = new DateTime(2026, 5, 3, 10, 30, 0, DateTimeKind.Utc);

        // Act & Assert
        Assert.True(detector.IsDowntimeImminent(withinThreshold));
        Assert.False(detector.IsDowntimeImminent(outsideThreshold));
    }

    [Fact]
    public void IsDowntimeImminent_CustomDowntimeTime_RespectsConfiguration()
    {
        // Arrange
        var detector = new DowntimeDetector(new TimeOnly(19, 0), TimeSpan.FromMinutes(30));
        var withinThreshold = new DateTime(2026, 5, 3, 18, 35, 0, DateTimeKind.Utc);
        var outsideThreshold = new DateTime(2026, 5, 3, 18, 20, 0, DateTimeKind.Utc);

        // Act & Assert
        Assert.True(detector.IsDowntimeImminent(withinThreshold));
        Assert.False(detector.IsDowntimeImminent(outsideThreshold));
    }

    [Fact]
    public void IsDowntimeImminent_ExactlyAtDowntimeTime_ReturnsTrue()
    {
        // Arrange
        var detector = new DowntimeDetector(new TimeOnly(11, 0), TimeSpan.FromMinutes(20));
        var currentTime = new DateTime(2026, 5, 3, 11, 0, 0, DateTimeKind.Utc);

        // Act
        var isImminent = detector.IsDowntimeImminent(currentTime);

        // Assert
        Assert.True(isImminent);
    }
}