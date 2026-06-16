using Automaton.Detectors;
using OpenCvSharp;
using DrawingRectangle = System.Drawing.Rectangle;

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
    private const int StrokeThickness = 2;
    private const int PointRadius = 4;
    private const double TextScale = 0.8;
    private const int TextThickness = 2;
    private const int LabelYOffset = 14;
    private const int MinimumLabelY = 30;
    private const int ClickRadius = 10;
    private const int ClickCrosshairLength = 9;
    private const int ClickStrokeThickness = 1;
    private static readonly Point TextOrigin = new(30, 40);
    private static readonly Scalar ClickColor = new(0, 0, 255);

    private static readonly Scalar[] Palette =
    [
        new Scalar(0, 255, 255),
        new Scalar(255, 180, 0),
        new Scalar(0, 220, 120),
        new Scalar(220, 120, 255),
        new Scalar(80, 180, 255),
        new Scalar(255, 120, 120)
    ];

    public static void Annotate(Mat image, params (Rect Bounds, OverlayColor Color)[] items)
    {
        foreach (var (bounds, color) in items)
        {
            Cv2.Rectangle(image, bounds, color.ToScalar(), StrokeThickness);
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

    public static void DrawPlayfieldOverlay(Mat image, PlayfieldDetectionResult playfieldDetection, IReadOnlyList<Point[]> polygons)
    {
        if (playfieldDetection.IsFound)
        {
            Cv2.Rectangle(image, playfieldDetection.Bounds, new Scalar(70, 150, 255), StrokeThickness);

            foreach (var marker in playfieldDetection.MarkerBounds)
            {
                Cv2.Rectangle(image, marker, new Scalar(255, 120, 80), StrokeThickness);
            }
        }

        for (var index = 0; index < polygons.Count; index++)
        {
            var color = Palette[index % Palette.Length];
            Cv2.Polylines(image, [polygons[index]], true, color, StrokeThickness, LineTypes.AntiAlias);

            foreach (var point in polygons[index])
            {
                Cv2.Circle(image, point, PointRadius, color, -1, LineTypes.AntiAlias);
            }
        }

        Cv2.PutText(
            image,
            playfieldDetection.IsFound
                ? $"Playfield found, clusters: {polygons.Count}"
                : polygons.Count > 0
                    ? $"Playfield not found, using fallback: {polygons.Count}"
                    : "Playfield not found",
            new Point(
                playfieldDetection.IsFound ? playfieldDetection.Bounds.X : TextOrigin.X,
                playfieldDetection.IsFound ? Math.Max(MinimumLabelY, playfieldDetection.Bounds.Y - LabelYOffset) : TextOrigin.Y),
            HersheyFonts.HersheySimplex,
            TextScale,
            playfieldDetection.IsFound ? new Scalar(80, 220, 120) : new Scalar(80, 120, 255),
            TextThickness,
            LineTypes.AntiAlias);
    }

    public static void DrawClickTrace(Mat image, IReadOnlyList<ClickTrace> clicks, DrawingRectangle captureBounds)
    {
        foreach (var click in clicks)
        {
            var point = new Point(
                click.ScreenPoint.X - captureBounds.Left,
                click.ScreenPoint.Y - captureBounds.Top);

            if (!IsInsideImage(image, point))
            {
                continue;
            }

            Cv2.Circle(image, point, ClickRadius, ClickColor, ClickStrokeThickness, LineTypes.AntiAlias);
            Cv2.Line(
                image,
                new Point(point.X - ClickCrosshairLength, point.Y),
                new Point(point.X + ClickCrosshairLength, point.Y),
                ClickColor,
                ClickStrokeThickness,
                LineTypes.AntiAlias);
            Cv2.Line(
                image,
                new Point(point.X, point.Y - ClickCrosshairLength),
                new Point(point.X, point.Y + ClickCrosshairLength),
                ClickColor,
                ClickStrokeThickness,
                LineTypes.AntiAlias);
        }
    }

    private static bool IsInsideImage(Mat image, Point point)
    {
        return point is { X: >= 0, Y: >= 0 } &&
               point.X < image.Width &&
               point.Y < image.Height;
    }
}
