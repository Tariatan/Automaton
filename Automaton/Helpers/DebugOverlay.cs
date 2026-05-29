using OpenCvSharp;

namespace Automaton.Helpers;

internal readonly record struct OverlayColor(byte R, byte G, byte B)
{
    public static OverlayColor Lime { get; } = new(0, 255, 0);
    public static OverlayColor Yellow { get; } = new(255, 255, 0);
    public static OverlayColor Cyan { get; } = new(0, 255, 255);
    public static OverlayColor LightBlue { get; } = new(120, 200, 255);
    public static OverlayColor Amber { get; } = new(255, 220, 120);
    public static OverlayColor Green { get; } = new(120, 255, 120);
    public static OverlayColor RedOrange { get; } = new(255, 120, 80);

    internal Scalar ToScalar() => new(B, G, R);
}

internal static class DebugOverlay
{
    private const double TextScale = 0.8;
    private const int TextThickness = 2;
    private static readonly Point TextOrigin = new(30, 40);

    public static void Annotate(Mat image, params (Rect Bounds, OverlayColor Color)[] items)
    {
        foreach (var (bounds, color) in items)
        {
            Cv2.Rectangle(image, bounds, color.ToScalar(), 2);
        }
    }

    public static void Label(Mat image, string text, OverlayColor color)
    {
        Cv2.PutText(
            image,
            text,
            TextOrigin,
            HersheyFonts.HersheySimplex,
            TextScale,
            color.ToScalar(),
            TextThickness,
            LineTypes.AntiAlias);
    }
}
