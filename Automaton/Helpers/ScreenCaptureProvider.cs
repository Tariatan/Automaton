using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using OpenCvSharp;

namespace Automaton.Helpers;

internal sealed class ScreenCaptureProvider : IScreenCaptureProvider
{
    public Mat CaptureScreen()
    {
        var bounds = ScreenCaptureService.BuildGameCaptureBounds(GetPhysicalVirtualScreenBounds());

        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return Cv2.ImDecode(ms.ToArray(), ImreadModes.Color);
    }

    private static Rectangle GetPhysicalVirtualScreenBounds()
    {
        return new Rectangle(
            GetSystemMetrics(ScreenCaptureService.VirtualScreenLeftMetric),
            GetSystemMetrics(ScreenCaptureService.VirtualScreenTopMetric),
            Math.Max(ScreenCaptureService.MinimumCaptureDimension, GetSystemMetrics(ScreenCaptureService.VirtualScreenWidthMetric)),
            Math.Max(ScreenCaptureService.MinimumCaptureDimension, GetSystemMetrics(ScreenCaptureService.VirtualScreenHeightMetric)));
    }

    [DllImport("user32.dll", EntryPoint = "GetSystemMetrics")]
    private static extern int GetSystemMetrics(int nIndex);
}
