using Automaton.Detectors;
using OpenCvSharp;

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
        var analysis = detector.Detect(image);

        // Assert
        Assert.NotNull(analysis.ItemHangarTitleBounds);
        Assert.NotNull(analysis.MiningHoldTitleBounds);
        Assert.NotNull(analysis.ItemHangarFirstRowBounds);
        Assert.NotNull(analysis.MiningHoldFirstRowBounds);
        var expectedItemHangarFirstRowBounds = BuildExpectedFirstRowBounds(analysis.ItemHangarTitleBounds!.Value, image.Size());
        var expectedMiningHoldFirstRowBounds = BuildExpectedFirstRowBounds(analysis.MiningHoldTitleBounds!.Value, image.Size());
        Assert.Equal(expectedItemHangarFirstRowBounds, analysis.ItemHangarFirstRowBounds!.Value);
        Assert.Equal(expectedMiningHoldFirstRowBounds, analysis.MiningHoldFirstRowBounds!.Value);
    }

    [Fact]
    public void Analyze_UndockedScreen_ReturnsNullBounds()
    {
        // Arrange
        using var image = SyntheticMiningImageFactory.LoadUndockedWithoutLocationChangeTimerImage();
        var detector = new InventoryDetector();

        // Act
        var analysis = detector.Detect(image);

        // Assert
        Assert.Null(analysis.MiningHoldTitleBounds);
        Assert.Null(analysis.ItemHangarTitleBounds);
    }

    private static Rect BuildExpectedFirstRowBounds(Rect titleBounds, Size screenSize)
    {
        var firstRowBounds = new Rect(titleBounds.Left, titleBounds.Bottom + 110, 300, 30);
        var x = Math.Clamp(firstRowBounds.X, 0, Math.Max(0, screenSize.Width - 1));
        var y = Math.Clamp(firstRowBounds.Y, 0, Math.Max(0, screenSize.Height - 1));
        var width = Math.Clamp(firstRowBounds.Width, 1, Math.Max(1, screenSize.Width - x));
        var height = Math.Clamp(firstRowBounds.Height, 1, Math.Max(1, screenSize.Height - y));
        return new Rect(x, y, width, height);
    }
}
