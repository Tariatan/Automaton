using Automaton.Helpers;
using Automaton.Tests.Stubs;
using OpenCvSharp;
using DrawingRectangle = System.Drawing.Rectangle;

namespace Automaton.Tests.Helpers;

public sealed class ScreenCaptureServiceTests
{
    [Fact]
    public void CaptureAndProcessCurrentScreen_CaptureProviderCreatesImage_ReturnsProcessedSummary()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var screenCaptureProvider = new StubScreenCaptureProvider(
            () => ScreenshotLoader.LoadOrSkip("Discovery/active_playfield_two_clusters.png"));
        var screenCaptureService = new ScreenCaptureService(screenCaptureProvider, new SampleImageProcessor());
        ScreenCaptureSummary summary;

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            summary = screenCaptureService.CaptureAndProcessCurrentScreen();
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.True(File.Exists(Path.Combine(workspace.Path, summary.CapturePath)));
        Assert.True(File.Exists(Path.Combine(workspace.Path, summary.Result.OutputPath)));
        Assert.Equal("captures", summary.CapturesDirectory);
        Assert.Equal(Path.Combine("captures", Path.GetFileNameWithoutExtension(summary.CapturePath) + ".annotated.png"), summary.Result.OutputPath);
        Assert.True(summary.Result.PlayfieldFound);
        Assert.True(summary.Result.ClusterCount > 0);
    }

    [Fact]
    public void CaptureAndProcessCurrentScreen_CaptureProviderCreatesBlankImage_ReturnsFallbackSummary()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        DefaultFallbackExampleFactory.Create(workspace.Path);
        var screenCaptureProvider = new StubScreenCaptureProvider(
            () => new Mat(new Size(1200, 900), MatType.CV_8UC3, Scalar.All(0)));
        var screenCaptureService = new ScreenCaptureService(screenCaptureProvider, new SampleImageProcessor());
        ScreenCaptureSummary summary;

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            summary = screenCaptureService.CaptureAndProcessCurrentScreen();
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.True(File.Exists(Path.Combine(workspace.Path, summary.CapturePath)));
        Assert.True(File.Exists(Path.Combine(workspace.Path, summary.Result.OutputPath)));
        Assert.False(summary.Result.PlayfieldFound);
        Assert.Equal(3, summary.Result.ClusterCount);
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
