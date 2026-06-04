using Automaton.Helpers;
using OpenCvSharp;
using DrawingRectangle = System.Drawing.Rectangle;
using Point = OpenCvSharp.Point;

namespace Automaton.Tests.Helpers;

public sealed class ClickTraceRecorderTests
{
    [Fact]
    public void Flush_ClickRecorded_AnnotatesCaptureImage()
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
        using var annotatedCapture = Cv2.ImRead(capturePath);
        Assert.False(annotatedCapture.Empty());
        Assert.NotEqual(Scalar.Black, Cv2.Sum(annotatedCapture));
    }

    [Fact]
    public void Flush_ClickRecordingSuppressed_LeavesCaptureImageUnchanged()
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
        using var unchangedCapture = Cv2.ImRead(capturePath);
        Assert.Equal(Scalar.Black, Cv2.Sum(unchangedCapture));
    }
}
