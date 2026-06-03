using Automaton.Detectors;
using OpenCvSharp;

namespace Automaton.Tests.Detectors;

public sealed class PopupDetectorTests
{
    [Fact]
    public void DetectPopupState_TwoClusterImage_ReturnsNone()
    {
        // Arrange
        using var image = SyntheticDiscoveryImageFactory.LoadTwoClusterImage();

        // Act
        var popupState = DetectPopupState(image);

        // Assert
        Assert.Equal(PopupState.None, popupState);
    }

    [Fact]
    public void DetectPopupState_SlowDownPopupImage_ReturnsSlowDown()
    {
        // Arrange
        using var image = SyntheticDiscoveryImageFactory.LoadSlowDownPopupImage();

        // Act
        var popupState = DetectPopupState(image);

        // Assert
        Assert.Equal(PopupState.SlowDown, popupState);
    }

    [Fact]
    public void DetectPopupState_MaximumSubmissionsPopupImage_ReturnsMaximumSubmissions()
    {
        // Arrange
        using var image = SyntheticDiscoveryImageFactory.LoadMaximumSubmissionsPopupImage();

        // Act
        var popupState = DetectPopupState(image);

        // Assert
        Assert.Equal(PopupState.MaxSubmissions, popupState);
    }

    private static PopupState DetectPopupState(Mat image)
    {
        var connectionLostDetection = ConnectionLostPopupDetectionEngine.DetectPopup(image);
        return connectionLostDetection.State == PopupState.ConnectionLost ? PopupState.ConnectionLost : PopupDetectionEngine.Detect(image).State;
    }
}
