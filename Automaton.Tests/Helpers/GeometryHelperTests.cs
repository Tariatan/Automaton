using Automaton.Helpers;
using OpenCvSharp;

namespace Automaton.Tests.Helpers;

public sealed class GeometryHelperTests
{
    [Fact]
    public void Center_StandardRect_ReturnsCenterPoint()
    {
        // Arrange
        var bounds = new Rect(100, 200, 50, 80);

        // Act
        var center = GeometryHelper.Center(bounds);

        // Assert
        Assert.Equal(new Point(125, 240), center);
    }

    [Fact]
    public void Center_ZeroOriginRect_ReturnsMidpoint()
    {
        // Arrange
        var bounds = new Rect(0, 0, 100, 100);

        // Act
        var center = GeometryHelper.Center(bounds);

        // Assert
        Assert.Equal(new Point(50, 50), center);
    }

    [Fact]
    public void CenterX_StandardRect_ReturnsFloatingPointCenter()
    {
        // Arrange
        var bounds = new Rect(10, 20, 51, 30);

        // Act
        var centerX = GeometryHelper.CenterX(bounds);

        // Assert
        Assert.Equal(35.5, centerX);
    }

    [Fact]
    public void CenterY_StandardRect_ReturnsFloatingPointCenter()
    {
        // Arrange
        var bounds = new Rect(10, 20, 30, 51);

        // Act
        var centerY = GeometryHelper.CenterY(bounds);

        // Assert
        Assert.Equal(45.5, centerY);
    }

    [Fact]
    public void IsUnscaled_ExactlyOne_ReturnsTrue()
    {
        // Arrange & Act & Assert
        Assert.True(GeometryHelper.IsUnscaled(1.0));
    }

    [Fact]
    public void IsUnscaled_SlightlyAboveOne_ReturnsFalse()
    {
        // Arrange & Act & Assert
        Assert.False(GeometryHelper.IsUnscaled(1.01));
    }

    [Fact]
    public void IsUnscaled_SlightlyBelowOne_ReturnsFalse()
    {
        // Arrange & Act & Assert
        Assert.False(GeometryHelper.IsUnscaled(0.99));
    }

    [Fact]
    public void BuildClampedBounds_WithinContainingSize_ReturnsOriginalBounds()
    {
        // Arrange
        var containingSize = new Size(1920, 1080);

        // Act
        var bounds = GeometryHelper.BuildClampedBounds(100, 200, 300, 400, containingSize);

        // Assert
        Assert.Equal(new Rect(100, 200, 300, 400), bounds);
    }

    [Fact]
    public void BuildClampedBounds_NegativeCoordinates_ClampsToZero()
    {
        // Arrange
        var containingSize = new Size(1920, 1080);

        // Act
        var bounds = GeometryHelper.BuildClampedBounds(-50, -30, 200, 150, containingSize);

        // Assert
        Assert.Equal(0, bounds.X);
        Assert.Equal(0, bounds.Y);
        Assert.Equal(200, bounds.Width);
        Assert.Equal(150, bounds.Height);
    }

    [Fact]
    public void BuildClampedBounds_ExceedsContainingSize_ClampsWidthAndHeight()
    {
        // Arrange
        var containingSize = new Size(500, 400);

        // Act
        var bounds = GeometryHelper.BuildClampedBounds(400, 350, 200, 200, containingSize);

        // Assert
        Assert.Equal(400, bounds.X);
        Assert.Equal(350, bounds.Y);
        Assert.Equal(100, bounds.Width);
        Assert.Equal(50, bounds.Height);
    }

    [Fact]
    public void BuildRelativeBounds_SizeOverload_CalculatesFromImageOrigin()
    {
        // Arrange
        var imageSize = new Size(1000, 800);

        // Act
        var bounds = GeometryHelper.BuildRelativeBounds(imageSize, 0.5, 0.25, 0.3, 0.4);

        // Assert
        Assert.Equal(500, bounds.X);
        Assert.Equal(200, bounds.Y);
        Assert.Equal(300, bounds.Width);
        Assert.Equal(320, bounds.Height);
    }

    [Fact]
    public void BuildRelativeBounds_RectOverload_CalculatesFromBoundsOrigin()
    {
        // Arrange
        var parentBounds = new Rect(100, 100, 1000, 800);

        // Act
        var bounds = GeometryHelper.BuildRelativeBounds(parentBounds, 0.1, 0.1, 0.5, 0.5);

        // Assert
        Assert.Equal(200, bounds.X);
        Assert.Equal(180, bounds.Y);
        Assert.Equal(500, bounds.Width);
        Assert.Equal(400, bounds.Height);
    }

    [Fact]
    public void BuildRelativeBounds_RatiosExceedBounds_ClampsToParentBounds()
    {
        // Arrange
        var parentBounds = new Rect(0, 0, 100, 100);

        // Act
        var bounds = GeometryHelper.BuildRelativeBounds(parentBounds, 0.9, 0.9, 0.5, 0.5);

        // Assert
        Assert.True(bounds.Right <= parentBounds.Right);
        Assert.True(bounds.Bottom <= parentBounds.Bottom);
        Assert.True(bounds.Width >= 1);
        Assert.True(bounds.Height >= 1);
    }

    [Fact]
    public void BuildRelativeBounds_ZeroWidthRatio_ReturnsMinimumWidthOfOne()
    {
        // Arrange
        var imageSize = new Size(1000, 800);

        // Act
        var bounds = GeometryHelper.BuildRelativeBounds(imageSize, 0.5, 0.5, 0.0, 0.0);

        // Assert
        Assert.Equal(1, bounds.Width);
        Assert.Equal(1, bounds.Height);
    }
}