using Automaton.Detectors;
using OpenCvSharp;

namespace Automaton.Tests.Detectors;

public sealed class ConnectionLostPopupDetectorTests
{
    [Fact]
    public void Detect_ImageContainsConnectionLostPopup_ReturnsConnectionLost()
    {
        // Arrange
        using var image = SyntheticCommonImageFactory.LoadConnectionLostPopupImage();

        // Act
        var detection = ConnectionLostPopupDetectionEngine.DetectPopup(image);

        // Assert
        Assert.Equal(PopupState.ConnectionLost, detection.State);
    }

    [Fact]
    public void Detect_ImageDoesNotContainConnectionLostPopup_ReturnsNone()
    {
        // Arrange
        using var image = SyntheticCommonImageFactory.LoadPlayButtonScreenImage();

        // Act
        var detection = ConnectionLostPopupDetectionEngine.DetectPopup(image);

        // Assert
        Assert.Equal(PopupState.None, detection.State);
    }

    [Fact]
    public void Detect_SlowDownPopupImage_PopupDetectionEngineReturnsSlowDown()
    {
        // Arrange
        var imagePath = SyntheticDiscoveryImageFactory.GetSlowDownPopupImagePath();
        using var image = Cv2.ImRead(imagePath);

        // Act
        var detection = PopupDetectionEngine.Detect(image);

        // Assert
        Assert.Equal(PopupState.SlowDown, detection.State);
    }

    [Fact]
    public void Detect_MaximumSubmissionsPopupImage_PopupDetectionEngineReturnsMaxSubmissions()
    {
        // Arrange
        var imagePath = SyntheticDiscoveryImageFactory.GetMaximumSubmissionsPopupImagePath();
        using var image = Cv2.ImRead(imagePath);

        // Act
        var detection = PopupDetectionEngine.Detect(image);

        // Assert
        Assert.Equal(PopupState.MaxSubmissions, detection.State);
    }

    [Fact]
    public void Detect_SlowDownPopupImage_ConnectionLostDetectionReturnsNone()
    {
        // Arrange
        var imagePath = SyntheticDiscoveryImageFactory.GetSlowDownPopupImagePath();
        using var image = Cv2.ImRead(imagePath);

        // Act
        var detection = ConnectionLostPopupDetectionEngine.DetectPopup(image);

        // Assert
        Assert.Equal(PopupState.None, detection.State);
    }

    [Fact]
    public void Detect_MaximumSubmissionsPopupImage_ConnectionLostDetectionReturnsNone()
    {
        // Arrange
        var imagePath = SyntheticDiscoveryImageFactory.GetMaximumSubmissionsPopupImagePath();
        using var image = Cv2.ImRead(imagePath);

        // Act
        var detection = ConnectionLostPopupDetectionEngine.DetectPopup(image);

        // Assert
        Assert.Equal(PopupState.None, detection.State);
    }
}
