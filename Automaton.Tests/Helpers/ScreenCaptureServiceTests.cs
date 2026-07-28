using Automaton.Core.Helpers;
using Automaton.Tests.Stubs;
using OpenCvSharp;
using DrawingRectangle = System.Drawing.Rectangle;

namespace Automaton.Tests.Helpers;

public sealed class ScreenCaptureServiceTests
{
    [Fact]
    public void CaptureCurrentScreenInMemory_PersistCapturesEnabled_DoesNotWriteCapture()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var screenCaptureProvider = new StubScreenCaptureProvider(
            () => new Mat(new Size(4, 3), MatType.CV_8UC3, Scalar.Black));
        var screenCaptureService = new ScreenCaptureService(screenCaptureProvider);
        var currentDirectory = Directory.GetCurrentDirectory();

        // Act
        Directory.SetCurrentDirectory(workspace.Path);
        try
        {
            using var capture = screenCaptureService.CaptureCurrentScreenInMemory(".probe");

            // Assert
            Assert.Equal(new Size(4, 3), capture.Image.Size());
            Assert.EndsWith(".probe.png", capture.CapturePath);
            Assert.False(File.Exists(Path.Combine(workspace.Path, capture.CapturePath)));
            Assert.False(Directory.Exists(Path.Combine(workspace.Path, "captures")));
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }
    }

    [Fact]
    public void SaveCapture_InMemoryCapture_WritesCapture()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var screenCaptureProvider = new StubScreenCaptureProvider(
            () => new Mat(new Size(4, 3), MatType.CV_8UC3, Scalar.Black));
        var screenCaptureService = new ScreenCaptureService(screenCaptureProvider);
        var currentDirectory = Directory.GetCurrentDirectory();

        // Act
        Directory.SetCurrentDirectory(workspace.Path);
        try
        {
            using var capture = screenCaptureService.CaptureCurrentScreenInMemory(".probe");
            screenCaptureService.SaveCapture(capture);

            // Assert
            Assert.True(File.Exists(Path.Combine(workspace.Path, capture.CapturePath)));
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }
    }

    [Fact]
    public void BuildGameCaptureBounds_VirtualScreenLargerThanGameViewport_ReturnsLeftGameViewport()
    {
        // Arrange
        var virtualScreenBounds = new DrawingRectangle(0, 0, 7680, 2160);

        // Act
        var captureBounds = ScreenCaptureService.BuildGameCaptureBounds(virtualScreenBounds);

        // Assert
        Assert.Equal(new DrawingRectangle(0, 0, 2560, 2160), captureBounds);
    }

    [Fact]
    public void BuildGameCaptureBounds_VirtualScreenSmallerThanGameViewport_ClampsToVirtualScreen()
    {
        // Arrange
        var virtualScreenBounds = new DrawingRectangle(0, 0, 1920, 1080);

        // Act
        var captureBounds = ScreenCaptureService.BuildGameCaptureBounds(virtualScreenBounds);

        // Assert
        Assert.Equal(virtualScreenBounds, captureBounds);
    }

    [Fact]
    public void BuildGameCaptureBounds_GameViewportOutsideVirtualScreen_FallsBackToVirtualScreen()
    {
        // Arrange
        var virtualScreenBounds = new DrawingRectangle(3000, 0, 1920, 1080);

        // Act
        var captureBounds = ScreenCaptureService.BuildGameCaptureBounds(virtualScreenBounds);

        // Assert
        Assert.Equal(virtualScreenBounds, captureBounds);
    }
}
