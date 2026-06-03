using Automaton.Detectors;
using OpenCvSharp;

namespace Automaton.Tests.Detectors;

public sealed class PlayfieldDetectionResultTests
{
    [Fact]
    public void IsFound_BoundsAreEmpty_ReturnsFalse()
    {
        // Arrange
        var result = PlayfieldDetectionResult.NotFound;

        // Act
        var isFound = result.IsFound;

        // Assert
        Assert.False(isFound);
        Assert.Empty(result.MarkerBounds);
    }

    [Fact]
    public void IsFound_BoundsAreNotEmpty_ReturnsTrue()
    {
        // Arrange
        var result = new PlayfieldDetectionResult(new Rect(10, 20, 30, 40), []);

        // Act
        var isFound = result.IsFound;

        // Assert
        Assert.True(isFound);
    }
}
