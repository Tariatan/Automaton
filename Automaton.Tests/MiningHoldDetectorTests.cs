using Automaton.Detectors;
using OpenCvSharp;

namespace Automaton.Tests;

public sealed class MiningHoldDetectorTests
{
    [Fact]
    public void Analyze_DockedItemHangarFocused_ReturnsHangarAndMiningHoldTitlesAndRows()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.CreateDockedItemHangarAndMiningHoldVisibleImage();
        var detector = new MiningHoldDetector();

        // Act
        var analysis = detector.Analyze(image);

        // Assert
        Assert.NotNull(analysis.MiningHoldTitleBounds);
        Assert.NotNull(analysis.ItemHangarTitleBounds);
        Assert.NotNull(analysis.MiningHoldFirstRowBounds);
        Assert.NotNull(analysis.ItemHangarFirstRowBounds);
        Assert.Equal(new Rect(75, 495, 300, 30), analysis.MiningHoldFirstRowBounds!.Value);
        Assert.Equal(new Rect(75, 205, 300, 30), analysis.ItemHangarFirstRowBounds!.Value);
    }

    [Fact]
    public void Analyze_UndockedScreen_ReturnsFallbackBounds()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.CreateUndockedWithoutLocationChangeTimerImage();
        var detector = new MiningHoldDetector();

        // Act
        var analysis = detector.Analyze(image);

        // Assert
        Assert.NotNull(analysis.MiningHoldTitleBounds);
        Assert.NotNull(analysis.ItemHangarTitleBounds);
        Assert.NotNull(analysis.MiningHoldFirstRowBounds);
        Assert.NotNull(analysis.ItemHangarFirstRowBounds);
        Assert.Equal(new Rect(75, 495, 300, 30), analysis.MiningHoldFirstRowBounds!.Value);
        Assert.Equal(new Rect(75, 205, 300, 30), analysis.ItemHangarFirstRowBounds!.Value);
    }
}
