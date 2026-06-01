using OpenCvSharp;

namespace Automaton.Helpers;

internal static class GeometryHelper
{
    public static Point Center(Rect bounds) => new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);

    public static double CenterX(Rect bounds) => bounds.X + bounds.Width / 2.0;

    public static double CenterY(Rect bounds) => bounds.Y + bounds.Height / 2.0;

    public static bool IsUnscaled(double scale) => Math.Abs(scale - 1.0) < double.Epsilon;

    public static Rect BuildRelativeBounds(
        Size imageSize,
        double leftRatio,
        double topRatio,
        double widthRatio,
        double heightRatio)
    {
        return BuildRelativeBounds(
            new Rect(0, 0, imageSize.Width, imageSize.Height),
            leftRatio, topRatio, widthRatio, heightRatio);
    }

    public static Rect BuildClampedBounds(int x, int y, int width, int height, Size containingSize)
    {
        var clampedX = Math.Clamp(x, 0, Math.Max(0, containingSize.Width - 1));
        var clampedY = Math.Clamp(y, 0, Math.Max(0, containingSize.Height - 1));
        var clampedWidth = Math.Clamp(width, 1, containingSize.Width - clampedX);
        var clampedHeight = Math.Clamp(height, 1, containingSize.Height - clampedY);
        return new Rect(clampedX, clampedY, clampedWidth, clampedHeight);
    }

    public static Rect BuildRelativeBounds(
        Rect bounds,
        double leftRatio,
        double topRatio,
        double widthRatio,
        double heightRatio)
    {
        var x = bounds.X + (int)Math.Round(bounds.Width * leftRatio);
        var y = bounds.Y + (int)Math.Round(bounds.Height * topRatio);
        var width = Math.Max(1, (int)Math.Round(bounds.Width * widthRatio));
        var height = Math.Max(1, (int)Math.Round(bounds.Height * heightRatio));

        var maxX = bounds.X + bounds.Width;
        var maxY = bounds.Y + bounds.Height;

        x = Math.Clamp(x, 0, Math.Max(0, maxX - 1));
        y = Math.Clamp(y, 0, Math.Max(0, maxY - 1));
        width = Math.Clamp(width, 1, maxX - x);
        height = Math.Clamp(height, 1, maxY - y);
        return new Rect(x, y, width, height);
    }
}
