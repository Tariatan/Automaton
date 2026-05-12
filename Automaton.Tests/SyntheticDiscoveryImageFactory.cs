using System.Drawing.Imaging;
using OpenCvSharp;

namespace Automaton.Tests;

internal static class SyntheticDiscoveryImageFactory
{
    private const int ImageWidth = 1200;
    private const int ImageHeight = 900;
    private const int PlayfieldLeft = 100;
    private const int PlayfieldTop = 150;
    private const int PlayfieldRight = 800;
    private const int PlayfieldBottom = 800;

    public static Mat CreateSingleClusterImage()
    {
        return CreateImage(includeSecondCluster: false);
    }

    public static Mat CreateTwoClusterImage()
    {
        return CreateImage(includeSecondCluster: true);
    }

    public static Mat CreateFourClusterImage()
    {
        return CreateImage(
            new ClusterDefinition(new Point(280, 300), new Size(85, 55), new Scalar(0, 120, 255)),
            new ClusterDefinition(new Point(415, 325), new Size(78, 52), new Scalar(0, 190, 255)),
            new ClusterDefinition(new Point(320, 505), new Size(76, 54), new Scalar(255, 180, 0)),
            new ClusterDefinition(new Point(515, 520), new Size(72, 52), new Scalar(255, 220, 0)));
    }

    public static Mat CreateSparseLowerClusterImage()
    {
        var image = CreateImage(includeSecondCluster: false);
        DrawSparseCluster(image, new Point(360, 650), new Size(155, 58), new Scalar(255, 185, 0), 340);
        return image;
    }

    public static Mat CreateMultiSizeClusterImage()
    {
        return CreateImage(
            new ClusterDefinition(new Point(250, 245), new Size(95, 55), new Scalar(0, 180, 255)),
            new ClusterDefinition(new Point(520, 250), new Size(42, 36), new Scalar(0, 210, 255)),
            new ClusterDefinition(new Point(310, 470), new Size(115, 82), new Scalar(255, 170, 0)),
            new ClusterDefinition(new Point(445, 420), new Size(36, 34), new Scalar(255, 220, 0)),
            new ClusterDefinition(new Point(560, 540), new Size(58, 90), new Scalar(255, 200, 40)));
    }

    public static Mat CreateMaximumSubmissionsPopupImage()
    {
        var image = new Mat(new Size(1701, 1345), MatType.CV_8UC3, new Scalar(22, 28, 30));
        DrawMaximumSubmissionsPopup(image);
        return image;
    }

    public static Mat CreateMaximumSubmissionsPopupImageWithPlayfield()
    {
        var image = CreateMaximumSubmissionsPopupImage();
        using var marker = LoadMarkerImage();
        PasteMarker(image, marker, new Point(PlayfieldLeft, PlayfieldTop));
        PasteMarker(image, marker, new Point(PlayfieldRight - marker.Width, PlayfieldTop));
        PasteMarker(image, marker, new Point(PlayfieldLeft, PlayfieldBottom - marker.Height));
        PasteMarker(image, marker, new Point(PlayfieldRight - marker.Width, PlayfieldBottom - marker.Height));
        DrawCluster(image, new Point(330, 430), new Size(110, 70), new Scalar(0, 120, 255));
        DrawCluster(image, new Point(315, 420), new Size(55, 35), new Scalar(0, 200, 255));
        return image;
    }

    public static Mat CreateSlowDownPopupImage()
    {
        var image = new Mat(new Size(2551, 2008), MatType.CV_8UC3, new Scalar(18, 24, 26));
        DrawSlowDownPopup(image);
        return image;
    }

    public static Mat CreateConnectionLostPopupImage()
    {
        var image = new Mat(new Size(2551, 2008), MatType.CV_8UC3, new Scalar(18, 24, 26));
        DrawConnectionLostPopup(image);
        return image;
    }

    public static Mat CreateWideScreenMaximumSubmissionsPopupImage()
    {
        var image = new Mat(new Size(3000, 1600), MatType.CV_8UC3, new Scalar(12, 14, 16));
        using var popupImage = CreateMaximumSubmissionsPopupImage();
        using var region = new Mat(image, new Rect(120, 20, popupImage.Width, popupImage.Height));
        popupImage.CopyTo(region);
        return image;
    }

    public static void WriteSingleClusterImage(string outputPath)
    {
        using var image = CreateSingleClusterImage();
        Cv2.ImWrite(outputPath, image);
    }

    public static void WriteTwoClusterImage(string outputPath)
    {
        using var image = CreateTwoClusterImage();
        Cv2.ImWrite(outputPath, image);
    }

    public static void WriteFourClusterImage(string outputPath)
    {
        using var image = CreateFourClusterImage();
        Cv2.ImWrite(outputPath, image);
    }

    public static void WriteSparseLowerClusterImage(string outputPath)
    {
        using var image = CreateSparseLowerClusterImage();
        Cv2.ImWrite(outputPath, image);
    }

    public static void WriteMultiSizeClusterImage(string outputPath)
    {
        using var image = CreateMultiSizeClusterImage();
        Cv2.ImWrite(outputPath, image);
    }

    public static void WriteMaximumSubmissionsPopupImage(string outputPath)
    {
        using var image = CreateMaximumSubmissionsPopupImage();
        Cv2.ImWrite(outputPath, image);
    }

    public static void WriteMaximumSubmissionsPopupImageWithPlayfield(string outputPath)
    {
        using var image = CreateMaximumSubmissionsPopupImageWithPlayfield();
        Cv2.ImWrite(outputPath, image);
    }

    public static void WriteSlowDownPopupImage(string outputPath)
    {
        using var image = CreateSlowDownPopupImage();
        Cv2.ImWrite(outputPath, image);
    }

    public static void WriteConnectionLostPopupImage(string outputPath)
    {
        using var image = CreateConnectionLostPopupImage();
        Cv2.ImWrite(outputPath, image);
    }

    private static Mat CreateImage(bool includeSecondCluster)
    {
        var clusters = new List<ClusterDefinition>
        {
            new(new Point(330, 430), new Size(110, 70), new Scalar(0, 120, 255)),
            new(new Point(315, 420), new Size(55, 35), new Scalar(0, 200, 255))
        };

        if (includeSecondCluster)
        {
            clusters.Add(new ClusterDefinition(new Point(520, 580), new Size(70, 50), new Scalar(255, 180, 0)));
        }

        return CreateImage(clusters.ToArray());
    }

    private static Mat CreateImage(params ClusterDefinition[] clusters)
    {
        var image = new Mat(new Size(ImageWidth, ImageHeight), MatType.CV_8UC3, Scalar.All(0));

        using var marker = LoadMarkerImage();
        PasteMarker(image, marker, new Point(PlayfieldLeft, PlayfieldTop));
        PasteMarker(image, marker, new Point(PlayfieldRight - marker.Width, PlayfieldTop));
        PasteMarker(image, marker, new Point(PlayfieldLeft, PlayfieldBottom - marker.Height));
        PasteMarker(image, marker, new Point(PlayfieldRight - marker.Width, PlayfieldBottom - marker.Height));

        foreach (var cluster in clusters)
        {
            DrawCluster(image, cluster.Center, cluster.Size, cluster.Color);
        }

        return image;
    }

    private static Mat LoadMarkerImage()
    {
        using var bitmap = Properties.Resources.marker;
        using var memoryStream = new MemoryStream();
        bitmap.Save(memoryStream, ImageFormat.Png);
        return Cv2.ImDecode(memoryStream.ToArray(), ImreadModes.Color);
    }

    private static void PasteMarker(Mat image, Mat marker, Point location)
    {
        using var roi = new Mat(image, new Rect(location.X, location.Y, marker.Width, marker.Height));
        marker.CopyTo(roi);
    }

    private static void DrawCluster(Mat image, Point center, Size size, Scalar color)
    {
        Cv2.Ellipse(image, center, size, 0, 0, 360, color, -1, LineTypes.AntiAlias);
    }

    private static void DrawSparseCluster(Mat image, Point center, Size spread, Scalar color, int count)
    {
        var random = new Random(12345);
        for (var index = 0; index < count; index++)
        {
            var radiusX = (random.NextDouble() * 2.0) - 1.0;
            var radiusY = (random.NextDouble() * 2.0) - 1.0;
            var x = center.X + (int)Math.Round(radiusX * spread.Width);
            var y = center.Y + (int)Math.Round(radiusY * spread.Height);
            var point = new Point(
                Math.Clamp(x, 0, image.Width - 1),
                Math.Clamp(y, 0, image.Height - 1));
            Cv2.Circle(image, point, 1, color, -1, LineTypes.AntiAlias);
        }
    }

    private static void DrawMaximumSubmissionsPopup(Mat image)
    {
        var popup = new Rect(
            (int)Math.Round(image.Width * 0.56),
            (int)Math.Round(image.Height * 0.62),
            (int)Math.Round(image.Width * 0.36),
            (int)Math.Round(image.Height * 0.29));
        Cv2.Rectangle(image, popup, new Scalar(7, 7, 7), -1);
        Cv2.Rectangle(image, popup, new Scalar(75, 65, 45));
        PastePopupTemplate(image, popup, Properties.Resources.icon_info, 0.06, 0.06);
        PastePopupTemplate(image, popup, Properties.Resources.title_max_submissions, 0.20, 0.05);

        var bodyLeft = popup.X + (int)Math.Round(popup.Width * 0.04);
        var bodyTop = popup.Y + (int)Math.Round(popup.Height * 0.36);
        Cv2.PutText(image, "While we appreciate your enthusiasm, our team can only", new Point(bodyLeft, bodyTop), HersheyFonts.HersheySimplex, 0.68, Scalar.All(220), 2, LineTypes.AntiAlias);
        Cv2.PutText(image, "process but so many submissions a day. Return in 23 hours,", new Point(bodyLeft, bodyTop + 30), HersheyFonts.HersheySimplex, 0.68, Scalar.All(220), 2, LineTypes.AntiAlias);
        Cv2.PutText(image, "12 minutes and 4 seconds to continue contributing to the", new Point(bodyLeft, bodyTop + 60), HersheyFonts.HersheySimplex, 0.68, Scalar.All(220), 2, LineTypes.AntiAlias);
        Cv2.PutText(image, "project.", new Point(bodyLeft, bodyTop + 90), HersheyFonts.HersheySimplex, 0.68, Scalar.All(220), 2, LineTypes.AntiAlias);
        PastePopupTemplate(image, popup, Properties.Resources.button_ok, 0.04, 0.80);
    }

    private static void DrawSlowDownPopup(Mat image)
    {
        var popup = new Rect(
            (int)Math.Round(image.Width * 0.56),
            (int)Math.Round(image.Height * 0.62),
            (int)Math.Round(image.Width * 0.36),
            (int)Math.Round(image.Height * 0.29));
        Cv2.Rectangle(image, popup, new Scalar(7, 7, 7), -1);
        Cv2.Rectangle(image, popup, new Scalar(60, 55, 42));
        PastePopupTemplate(image, popup, Properties.Resources.icon_info, 0.06, 0.06);
        PastePopupTemplate(image, popup, Properties.Resources.title_slow_down, 0.20, 0.06);

        var bodyLeft = popup.X + (int)Math.Round(popup.Width * 0.04);
        var bodyTop = popup.Y + (int)Math.Round(popup.Height * 0.36);
        Cv2.PutText(image, "We cannot produce more than five submissions a minute, nor", new Point(bodyLeft, bodyTop), HersheyFonts.HersheySimplex, 0.68, Scalar.All(220), 2, LineTypes.AntiAlias);
        Cv2.PutText(image, "should we. Make sure you're taking time to carefully analyze", new Point(bodyLeft, bodyTop + 30), HersheyFonts.HersheySimplex, 0.68, Scalar.All(220), 2, LineTypes.AntiAlias);
        Cv2.PutText(image, "each sample. A prolific researcher is a good researcher, but", new Point(bodyLeft, bodyTop + 60), HersheyFonts.HersheySimplex, 0.68, Scalar.All(220), 2, LineTypes.AntiAlias);
        Cv2.PutText(image, "only if they produce quality work.", new Point(bodyLeft, bodyTop + 90), HersheyFonts.HersheySimplex, 0.68, Scalar.All(220), 2, LineTypes.AntiAlias);

        PastePopupTemplate(image, popup, Properties.Resources.button_ok, 0.04, 0.80);
    }

    private static void DrawConnectionLostPopup(Mat image)
    {
        var popup = new Rect(
            (int)Math.Round(image.Width * 0.42),
            (int)Math.Round(image.Height * 0.62),
            (int)Math.Round(image.Width * 0.30),
            (int)Math.Round(image.Height * 0.29));
        Cv2.Rectangle(image, popup, new Scalar(7, 7, 7), -1);
        Cv2.Rectangle(image, popup, new Scalar(65, 60, 45));
        PastePopupTemplate(image, popup, Properties.Resources.icon_warning, 0.06, 0.05);
        PastePopupTemplate(image, popup, Properties.Resources.title_connection_lost, 0.20, 0.06);

        var bodyLeft = popup.X + (int)Math.Round(popup.Width * 0.04);
        var bodyTop = popup.Y + (int)Math.Round(popup.Height * 0.34);
        Cv2.PutText(image, "Connection to server was lost.", new Point(bodyLeft, bodyTop), HersheyFonts.HersheySimplex, 0.68, Scalar.All(220), 2, LineTypes.AntiAlias);
        Cv2.PutText(image, "For advice on possible troubleshooting please see", new Point(bodyLeft, bodyTop + 52), HersheyFonts.HersheySimplex, 0.62, Scalar.All(220), 2, LineTypes.AntiAlias);
        Cv2.PutText(image, "here.", new Point(bodyLeft + 430, bodyTop + 52), HersheyFonts.HersheySimplex, 0.62, new Scalar(30, 220, 255), 2, LineTypes.AntiAlias);

        PastePopupTemplate(image, popup, Properties.Resources.button_quit, 0.04, 0.84);
    }

    private static void PastePopupTemplate(Mat image, Rect popup, System.Drawing.Bitmap bitmap, double leftRatio, double topRatio)
    {
        using var template = LoadTemplateImage(bitmap);
        var left = popup.X + (int)Math.Round(popup.Width * leftRatio);
        var top = popup.Y + (int)Math.Round(popup.Height * topRatio);
        var clampedLeft = Math.Clamp(left, 0, image.Width - 1);
        var clampedTop = Math.Clamp(top, 0, image.Height - 1);
        var width = Math.Min(template.Width, image.Width - clampedLeft);
        var height = Math.Min(template.Height, image.Height - clampedTop);
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var targetBounds = new Rect(clampedLeft, clampedTop, width, height);
        using var source = new Mat(template, new Rect(0, 0, width, height));
        using var roi = new Mat(image, targetBounds);
        source.CopyTo(roi);
    }

    private static Mat LoadTemplateImage(System.Drawing.Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return Cv2.ImDecode(stream.ToArray(), ImreadModes.Color);
    }

    private readonly record struct ClusterDefinition(Point Center, Size Size, Scalar Color);
}
