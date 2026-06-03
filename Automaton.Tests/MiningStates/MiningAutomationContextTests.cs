using Automaton.Helpers;
using Automaton.MiningStates;
using Automaton.Tests.Stubs;
using OpenCvSharp;

namespace Automaton.Tests.MiningStates;

public sealed class MiningAutomationContextTests
{
    [Fact]
    public void BlacklistAsteroidBelt_SingleBelt_IncrementsBlacklistCount()
    {
        // Arrange
        var context = CreateContext();

        // Act
        context.BlacklistAsteroidBelt(new Rect(100, 200, 50, 30));

        // Assert
        Assert.Equal(1, context.BlacklistedAsteroidBeltCount);
    }

    [Fact]
    public void BlacklistAsteroidBelt_DuplicateSimilarBounds_DoesNotAddTwice()
    {
        // Arrange
        var context = CreateContext();
        var bounds = new Rect(100, 200, 50, 30);
        var similarBounds = new Rect(105, 203, 48, 28);

        // Act
        context.BlacklistAsteroidBelt(bounds);
        context.BlacklistAsteroidBelt(similarBounds);

        // Assert
        Assert.Equal(1, context.BlacklistedAsteroidBeltCount);
    }

    [Fact]
    public void BlacklistAsteroidBelt_DifferentBounds_AddsBoth()
    {
        // Arrange
        var context = CreateContext();
        var firstBounds = new Rect(100, 200, 50, 30);
        var secondBounds = new Rect(500, 600, 50, 30);

        // Act
        context.BlacklistAsteroidBelt(firstBounds);
        context.BlacklistAsteroidBelt(secondBounds);

        // Assert
        Assert.Equal(2, context.BlacklistedAsteroidBeltCount);
    }

    [Fact]
    public void IsAsteroidBeltBlacklisted_BlacklistedBounds_ReturnsTrue()
    {
        // Arrange
        var context = CreateContext();
        var bounds = new Rect(100, 200, 50, 30);
        context.BlacklistAsteroidBelt(bounds);

        // Act
        var isBlacklisted = context.IsAsteroidBeltBlacklisted(bounds);

        // Assert
        Assert.True(isBlacklisted);
    }

    [Fact]
    public void IsAsteroidBeltBlacklisted_SimilarBounds_ReturnsTrue()
    {
        // Arrange
        var context = CreateContext();
        context.BlacklistAsteroidBelt(new Rect(100, 200, 50, 30));

        // Act
        var isBlacklisted = context.IsAsteroidBeltBlacklisted(new Rect(104, 203, 47, 28));

        // Assert
        Assert.True(isBlacklisted);
    }

    [Fact]
    public void IsAsteroidBeltBlacklisted_DifferentBounds_ReturnsFalse()
    {
        // Arrange
        var context = CreateContext();
        context.BlacklistAsteroidBelt(new Rect(100, 200, 50, 30));

        // Act
        var isBlacklisted = context.IsAsteroidBeltBlacklisted(new Rect(500, 600, 50, 30));

        // Assert
        Assert.False(isBlacklisted);
    }

    [Fact]
    public void IsAsteroidBeltBlacklisted_EmptyBlacklist_ReturnsFalse()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var isBlacklisted = context.IsAsteroidBeltBlacklisted(new Rect(100, 200, 50, 30));

        // Assert
        Assert.False(isBlacklisted);
    }

    [Fact]
    public void TryGetCurrentAsteroidBelt_NoBeltSet_ReturnsFalse()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var found = context.TryGetCurrentAsteroidBelt(out var beltBounds);

        // Assert
        Assert.False(found);
        Assert.Equal(default, beltBounds);
    }

    [Fact]
    public void TryGetCurrentAsteroidBelt_BeltSet_ReturnsTrueWithBounds()
    {
        // Arrange
        var context = CreateContext();
        var expectedBounds = new Rect(100, 200, 50, 30);
        context.SetCurrentAsteroidBelt(expectedBounds);

        // Act
        var found = context.TryGetCurrentAsteroidBelt(out var beltBounds);

        // Assert
        Assert.True(found);
        Assert.Equal(expectedBounds, beltBounds);
    }

    [Fact]
    public void SetCurrentAsteroidBelt_OverwritesPreviousValue()
    {
        // Arrange
        var context = CreateContext();
        context.SetCurrentAsteroidBelt(new Rect(100, 200, 50, 30));
        var newBounds = new Rect(500, 600, 80, 40);

        // Act
        context.SetCurrentAsteroidBelt(newBounds);
        context.TryGetCurrentAsteroidBelt(out var beltBounds);

        // Assert
        Assert.Equal(newBounds, beltBounds);
    }

    [Fact]
    public void BlacklistedAsteroidBeltCount_InitialState_ReturnsZero()
    {
        // Arrange
        var context = CreateContext();

        // Act & Assert
        Assert.Equal(0, context.BlacklistedAsteroidBeltCount);
    }

    private static MiningAutomationContext CreateContext()
    {
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() => new Mat(1, 1, MatType.CV_8UC3)),
            new SampleImageProcessor(),
            persistCaptures: false);
        return new MiningAutomationContext(screenCaptureService, new StubAutomationClock());
    }
}