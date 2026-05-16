using Automaton.Detectors;
using OpenCvSharp;

namespace Automaton.Tests;

public sealed class MiningHoldDetectorTests
{
    [Fact]
    public void Analyze_DockedItemHangarFocused_ReturnsHangarAndMiningHoldTitlesAndRows()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.CreateDockedItemHangarFocusedImage();
        var detector = new MiningHoldDetector();

        // Act
        var analysis = detector.Analyze(image);

        // Assert
        Assert.NotNull(analysis.MiningHoldTitleBounds);
        Assert.NotNull(analysis.ItemHangarTitleBounds);
        Assert.NotNull(analysis.MiningHoldFirstRowBounds);
        Assert.NotNull(analysis.ItemHangarFirstRowBounds);
    }

    [Fact]
    public void Analyze_DockedMiningHoldFocusedEmpty_ReturnsMiningHoldAndItemHangarRows()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.CreateDockedMiningHoldFocusedEmptyImage();
        var detector = new MiningHoldDetector();

        // Act
        var analysis = detector.Analyze(image);

        // Assert
        Assert.NotNull(analysis.MiningHoldTitleBounds);
        Assert.NotNull(analysis.ItemHangarTitleBounds);
        Assert.NotNull(analysis.MiningHoldFirstRowBounds);
        Assert.NotNull(analysis.ItemHangarFirstRowBounds);
    }

    [Fact]
    public void Analyze_DockedMiningHoldFocusedNotEmpty_ReturnsMiningHoldAndItemHangarRows()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.CreateDockedMiningHoldFocusedNotEmptyImage();
        var detector = new MiningHoldDetector();

        // Act
        var analysis = detector.Analyze(image);

        // Assert
        Assert.NotNull(analysis.MiningHoldTitleBounds);
        Assert.NotNull(analysis.ItemHangarTitleBounds);
        Assert.NotNull(analysis.MiningHoldFirstRowBounds);
        Assert.NotNull(analysis.ItemHangarFirstRowBounds);
    }

    [Fact]
    public void Analyze_UndockedScreen_ReturnsFallbackBounds()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.CreateUndockedImage();
        var detector = new MiningHoldDetector();

        // Act
        var analysis = detector.Analyze(image);

        // Assert
        Assert.NotNull(analysis.MiningHoldTitleBounds);
        Assert.NotNull(analysis.ItemHangarTitleBounds);
        Assert.NotNull(analysis.MiningHoldFirstRowBounds);
        Assert.NotNull(analysis.ItemHangarFirstRowBounds);
    }
}
