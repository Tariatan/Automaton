using Automaton.Detectors;
using OpenCvSharp;

namespace Automaton.Tests.Detectors;

public sealed class DailyGiftDetectorTests
{
    [Theory]
    [InlineData("close.png")]
    public void Detect_CurrentScreenshot_ReturnsCloseButton(string filename)
    {
        // Arrange
        using var image = ScreenshotLoader.LoadOrSkip($"DailyLogin/{filename}");
        using var detector = new DailyGiftDetector();

        // Act
        var found = detector.Detect(image, out var detection);

        // Assert
        Assert.True(found);
        Assert.Null(detection.ClaimButtonBounds);
        Assert.NotNull(detection.CloseButtonBounds);
    }

    [Theory]
    [InlineData(390, 250)]
    [InlineData(490, 650)]
    [InlineData(444, 560)]
    public void Detect_WindowShiftedWithinSearchMargins_ReturnsTranslatedButton(int left, int top)
    {
        // Arrange
        using var image = ScreenshotLoader.LoadOrSkip("DailyLogin/close.png");
        using var source = new Mat(image, new Rect(443, 463, 1664, 1082));
        using var screen = new Mat(2160, 2560, MatType.CV_8UC3, Scalar.Black);
        using var destination = new Mat(screen, new Rect(left, top, 1664, 1082));
        source.CopyTo(destination);
        using var detector = new DailyGiftDetector();

        // Act
        var found = detector.Detect(screen, out var detection);

        // Assert
        Assert.True(found);
        Assert.Equal(new Rect(left + 1350, top + 920, 94, 48), detection.CloseButtonBounds);
        Assert.Null(detection.ClaimButtonBounds);
    }

    [Fact]
    public void Detect_WindowClippedByScreen_ReturnsNotFound()
    {
        // Arrange
        using var image = ScreenshotLoader.LoadOrSkip("DailyLogin/close.png");
        using var cropped = new Mat(image, new Rect(0, 0, 2000, 1600));
        using var detector = new DailyGiftDetector();

        // Act
        var found = detector.Detect(cropped, out var detection);

        // Assert
        Assert.False(found);
        Assert.Equal(default, detection);
    }

    [Theory]
    [InlineData("claim.png", true)]
    [InlineData("close.png", false)]
    public void Detect_SuppliedScreenshot_ReturnsWindowAndCorrectButton(string filename, bool claim)
    {
        // Arrange
        using var image = ScreenshotLoader.LoadOrSkip($"DailyLogin/{filename}");
        using var detector = new DailyGiftDetector();

        // Act
        var found = detector.Detect(image, out var detection);

        // Assert
        Assert.True(found);
        Assert.True(detection.IsFound);
        Assert.Equal(claim ? new Rect(1762, 1383, 156, 48) : null, detection.ClaimButtonBounds);
        Assert.Equal(claim ? null : new Rect(1793, 1383, 94, 48), detection.CloseButtonBounds);
    }

    [Theory]
    [InlineData(467, 480, 323, 39)]
    [InlineData(1988, 486, 92, 27)]
    [InlineData(497, 1477, 288, 31)]
    public void Detect_MissingWindowAnchor_ReturnsNotFound(int x, int y, int width, int height)
    {
        // Arrange
        using var image = ScreenshotLoader.LoadOrSkip("DailyLogin/claim.png");
        using var anchor = new Mat(image, new Rect(x, y, width, height));
        anchor.SetTo(Scalar.Black);
        using var detector = new DailyGiftDetector();

        // Act
        var found = detector.Detect(image, out var detection);

        // Assert
        Assert.False(found);
        Assert.Equal(default, detection);
    }

    [Fact]
    public void Detect_CampaignArtworkRemoved_StillReturnsClaimButton()
    {
        // Arrange
        using var image = ScreenshotLoader.LoadOrSkip("DailyLogin/claim.png");
        using var artwork = new Mat(image, new Rect(485, 540, 1577, 804));
        artwork.SetTo(Scalar.Black);
        using var detector = new DailyGiftDetector();

        // Act
        var found = detector.Detect(image, out var detection);

        // Assert
        Assert.True(found);
        Assert.Equal(new Rect(1762, 1383, 156, 48), detection.ClaimButtonBounds);
        Assert.Null(detection.CloseButtonBounds);
    }

    [Fact]
    public void Detect_MissingActionButton_ReturnsWindowWithNullButtons()
    {
        // Arrange
        using var image = ScreenshotLoader.LoadOrSkip("DailyLogin/claim.png");
        using var button = new Mat(image, new Rect(1758, 1379, 164, 56));
        button.SetTo(Scalar.Black);
        using var detector = new DailyGiftDetector();

        // Act
        var found = detector.Detect(image, out var detection);

        // Assert
        Assert.True(found);
        Assert.True(detection.IsFound);
        Assert.Null(detection.ClaimButtonBounds);
        Assert.Null(detection.CloseButtonBounds);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void Detect_GrayscaleOrBgraScreenshot_ReturnsClaimButton(int channels)
    {
        // Arrange
        using var image = ScreenshotLoader.LoadOrSkip("DailyLogin/claim.png");
        using var converted = new Mat();
        Cv2.CvtColor(image, converted, channels == 1 ? ColorConversionCodes.BGR2GRAY : ColorConversionCodes.BGR2BGRA);
        using var detector = new DailyGiftDetector();

        // Act
        var found = detector.Detect(converted, out var detection);

        // Assert
        Assert.True(found);
        Assert.NotNull(detection.ClaimButtonBounds);
        Assert.Null(detection.CloseButtonBounds);
    }

    [Theory]
    [InlineData(2108, 1545)]
    [InlineData(2560, 2160)]
    public void Detect_DifferentScreenSizeWithFixedWindowPosition_ReturnsFixedBounds(int width, int height)
    {
        // Arrange
        using var image = ScreenshotLoader.LoadOrSkip("DailyLogin/close.png");
        using var window = new Mat(image, new Rect(443, 463, 1664, 1082));
        using var screen = new Mat(height, width, MatType.CV_8UC3, Scalar.Black);
        using var destination = new Mat(screen, new Rect(444, 463, 1664, 1082));
        window.CopyTo(destination);
        using var detector = new DailyGiftDetector();

        // Act
        var found = detector.Detect(screen, out var detection);

        // Assert
        Assert.True(found);
        Assert.Equal(new Rect(1794, 1383, 94, 48), detection.CloseButtonBounds);
        Assert.Null(detection.ClaimButtonBounds);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1000, 1000)]
    [InlineData(2107, 1545)]
    [InlineData(2108, 1544)]
    [InlineData(2551, 2008)]
    public void Detect_EmptySmallOrBlankScreen_ReturnsNotFound(int width, int height)
    {
        // Arrange
        using var image = new Mat(height, width, MatType.CV_8UC3, Scalar.Black);
        using var detector = new DailyGiftDetector();

        // Act
        var found = detector.Detect(image, out var detection);

        // Assert
        Assert.False(found);
        Assert.Equal(default, detection);
    }
}