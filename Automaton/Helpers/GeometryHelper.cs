using OpenCvSharp;

namespace Automaton.Helpers;

internal static class GeometryHelper
{
    public static Point Center(Rect bounds) => new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
}
