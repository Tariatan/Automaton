using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using OpenCvSharp;

namespace Automaton.Helpers;

internal sealed class ScreenCaptureProvider : IScreenCaptureProvider
{
    public Mat CaptureScreen()
    {
        var bounds = ScreenCaptureService.GetCurrentCaptureBounds();

        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return Cv2.ImDecode(ms.ToArray(), ImreadModes.Color);
    }
}
