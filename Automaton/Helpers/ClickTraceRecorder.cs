using System.IO;
using OpenCvSharp;
using DrawingRectangle = System.Drawing.Rectangle;
using Point = OpenCvSharp.Point;

namespace Automaton.Helpers;

internal sealed class ClickTraceRecorder
{
    private const string ClickTraceSuffix = ".clicks.png";

    private readonly Lock m_Lock = new();
    private int m_SuppressionDepth;
    private ActiveClickTraceCapture? m_ActiveCapture;

    public IDisposable SuppressRecording()
    {
        lock (m_Lock)
        {
            m_SuppressionDepth++;
        }

        return new RecordingSuppressionScope(this);
    }

    public void RecordClick(Point screenPoint, DateTime timestampUtc)
    {
        lock (m_Lock)
        {
            if (m_SuppressionDepth > 0 || m_ActiveCapture is null)
            {
                return;
            }

            m_ActiveCapture.Clicks.Add(new ClickTrace(timestampUtc, screenPoint));
        }
    }

    public void BeginCapture(string imagePath, DrawingRectangle captureBounds)
    {
        var completedCapture = CompleteActiveCapture();

        lock (m_Lock)
        {
            m_ActiveCapture = new ActiveClickTraceCapture(imagePath, captureBounds);
        }

        WriteClickTrace(completedCapture);
    }

    public void Flush()
    {
        WriteClickTrace(CompleteActiveCapture());
    }

    internal static string BuildClickTracePath(string imagePath)
    {
        return Path.Combine(
            Path.GetDirectoryName(imagePath)!,
            Path.GetFileNameWithoutExtension(imagePath) + ClickTraceSuffix);
    }

    private ClickTraceCapture? CompleteActiveCapture()
    {
        lock (m_Lock)
        {
            if (m_ActiveCapture is null)
            {
                return null;
            }

            var completedCapture = new ClickTraceCapture(
                m_ActiveCapture.ImagePath,
                m_ActiveCapture.CaptureBounds,
                m_ActiveCapture.Clicks.ToArray());
            m_ActiveCapture = null;
            return completedCapture;
        }
    }

    private static void WriteClickTrace(ClickTraceCapture? capture)
    {
        if (capture is null || capture.Clicks.Count == 0 || !File.Exists(capture.ImagePath))
        {
            return;
        }

        using var image = Cv2.ImRead(capture.ImagePath);
        if (image.Empty())
        {
            return;
        }

        DebugOverlay.DrawClickTrace(image, capture.Clicks, capture.CaptureBounds);
        Cv2.ImWrite(BuildClickTracePath(capture.ImagePath), image);
    }

    private void EndSuppression()
    {
        lock (m_Lock)
        {
            m_SuppressionDepth = Math.Max(0, m_SuppressionDepth - 1);
        }
    }

    private sealed class RecordingSuppressionScope(ClickTraceRecorder recorder) : IDisposable
    {
        private bool m_Disposed;

        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            recorder.EndSuppression();
            m_Disposed = true;
        }
    }

    private sealed class ActiveClickTraceCapture(string imagePath, DrawingRectangle captureBounds)
    {
        public string ImagePath { get; } = imagePath;
        public DrawingRectangle CaptureBounds { get; } = captureBounds;
        public List<ClickTrace> Clicks { get; } = [];
    }
}

internal sealed record ClickTrace(DateTime TimestampUtc, Point ScreenPoint);

internal sealed record ClickTraceCapture(
    string ImagePath,
    DrawingRectangle CaptureBounds,
    IReadOnlyList<ClickTrace> Clicks);
