using Automaton.Helpers;
using OpenCvSharp;
using DrawingRectangle = System.Drawing.Rectangle;
using Point = OpenCvSharp.Point;

namespace Automaton.Tests.Helpers;

public sealed class ClickTraceRecorderTests
{
    [Fact]
    public void Flush_ClickRecorded_WritesClickTraceImage()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var capturePath = Path.Combine(workspace.Path, "capture.png");
        using var capture = new Mat(new Size(120, 120), MatType.CV_8UC3, Scalar.Black);
        Cv2.ImWrite(capturePath, capture);
        var clickTraceRecorder = new ClickTraceRecorder();
        clickTraceRecorder.BeginCapture(capturePath, new DrawingRectangle(100, 200, 120, 120));

        // Act
        clickTraceRecorder.RecordClick(new Point(160, 260), DateTime.UtcNow);
        clickTraceRecorder.Flush();

        // Assert
        var clickTracePath = ClickTraceRecorder.BuildClickTracePath(capturePath);
        Assert.True(File.Exists(clickTracePath));
        using var clickTraceImage = Cv2.ImRead(clickTracePath);
        Assert.False(clickTraceImage.Empty());
        Assert.NotEqual(Scalar.Black, Cv2.Sum(clickTraceImage));
    }

    [Fact]
    public void Flush_ClickRecordingSuppressed_DoesNotWriteClickTraceImage()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var capturePath = Path.Combine(workspace.Path, "capture.png");
        using var capture = new Mat(new Size(120, 120), MatType.CV_8UC3, Scalar.Black);
        Cv2.ImWrite(capturePath, capture);
        var clickTraceRecorder = new ClickTraceRecorder();
        clickTraceRecorder.BeginCapture(capturePath, new DrawingRectangle(0, 0, 120, 120));

        // Act
        using (clickTraceRecorder.SuppressRecording())
        {
            clickTraceRecorder.RecordClick(new Point(60, 60), DateTime.UtcNow);
        }

        clickTraceRecorder.Flush();

        // Assert
        Assert.False(File.Exists(ClickTraceRecorder.BuildClickTracePath(capturePath)));
    }
}
