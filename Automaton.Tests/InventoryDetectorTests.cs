using Automaton.Detectors;
using Automaton.Primitives;

namespace Automaton.Tests;

public sealed class InventoryDetectorTests
{
    [Fact]
    public void Analyze_DockedItemHangarFocused_ReturnsHangarAndMiningHoldTitlesAndRows()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.LoadDockedItemHangarAndMiningHoldVisibleImage();
        var detector = new InventoryDetector();

        // Act
        var analysis = detector.Analyze(image);

        // Assert
        Assert.NotNull(analysis.ItemHangarTitleBounds);
        Assert.NotNull(analysis.MiningHoldTitleBounds);
        Assert.NotNull(analysis.ItemHangarFirstRowBounds);
        Assert.NotNull(analysis.MiningHoldFirstRowBounds);
        Assert.Equal(Settings.ItemHangarFirstRowBounds, analysis.ItemHangarFirstRowBounds!.Value);
        Assert.Equal(Settings.MiningHoldFirstRowBounds, analysis.MiningHoldFirstRowBounds!.Value);
    }

    [Fact]
    public void Analyze_UndockedScreen_ReturnsNullBounds()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.LoadUndockedWithoutLocationChangeTimerImage();
        var detector = new InventoryDetector();

        // Act
        var analysis = detector.Analyze(image);

        // Assert
        Assert.Null(analysis.MiningHoldTitleBounds);
        Assert.Null(analysis.ItemHangarTitleBounds);
    }
}
