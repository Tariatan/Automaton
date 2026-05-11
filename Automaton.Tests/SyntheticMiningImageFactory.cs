using OpenCvSharp;
using System.Drawing.Imaging;

namespace Automaton.Tests;

internal static class SyntheticMiningImageFactory
{
    private const int ImageWidth = 2551;
    private const int ImageHeight = 2008;
    private static readonly Rect UndockButtonBounds = new(1995, 461, 520, 60);
    private static readonly Point LocationChangeTimerLocation = new(123, 46);
    private static readonly Rect OverviewBounds = new(1974, 261, 577, 900);
    private static readonly Rect OverviewBeltButtonBounds = new(2274, 333, 37, 26);
    private static readonly Rect AsteroidBeltLabelBounds = new(1040, 1532, 410, 72);
    private static readonly Rect MineOverviewBounds = new(1700, 1226, 270, 336);
    private static readonly Rect MiningHoldEntryBounds = new(64, 1654, 242, 52);
    private static readonly Rect ItemHangarEntryBounds = new(64, 1741, 242, 52);
    private static readonly Rect MiningHoldContentBounds = new(319, 1617, 178, 231);
    private static readonly Scalar BackgroundColor = new(0, 0, 0);
    private static readonly Scalar PanelColor = new(13, 13, 13);
    private static readonly Scalar FocusedColor = new(60, 52, 31);
    private static readonly Scalar BorderColor = new(88, 112, 120);
    private static readonly Scalar TextColor = new(170, 170, 170);

    public static Mat CreateDockedItemHangarFocusedImage()
    {
        var image = CreateDockedBaseImage();
        DrawInventory(image, focusedEntryBounds: ItemHangarEntryBounds, miningHoldContainsOre: false);
        return image;
    }

    public static Mat CreateDockedMiningHoldFocusedEmptyImage()
    {
        var image = CreateDockedBaseImage();
        DrawInventory(image, focusedEntryBounds: MiningHoldEntryBounds, miningHoldContainsOre: false);
        DrawNothingFound(image);
        return image;
    }

    public static Mat CreateDockedMiningHoldFocusedNotEmptyImage()
    {
        var image = CreateDockedBaseImage();
        DrawInventory(image, focusedEntryBounds: MiningHoldEntryBounds, miningHoldContainsOre: true);
        DrawOreItem(image);
        return image;
    }

    public static Mat CreateUndockedImage()
    {
        return new Mat(new Size(ImageWidth, ImageHeight), MatType.CV_8UC3, BackgroundColor);
    }

    public static Mat CreateUndockedCompleteImage()
    {
        var image = CreateUndockedImage();
        using var locationChangeTimer = LoadLocationChangeTimer();
        PasteTemplate(image, locationChangeTimer, LocationChangeTimerLocation);
        return image;
    }

    public static Mat CreateWarpToAsteroidFieldImage()
    {
        var image = CreateUndockedCompleteImage();
        DrawAsteroidBeltOverview(image);
        return image;
    }

    public static Mat CreateWarpDriveActiveImage()
    {
        var image = CreateWarpToAsteroidFieldImage();
        DrawWarpDriveActiveText(image);
        return image;
    }

    public static Mat CreateLandedOnAsteroidBeltImage()
    {
        var image = CreateWarpToAsteroidFieldImage();
        DrawAsteroidBeltLabel(image);
        DrawMineOverview(image);
        return image;
    }

    public static Mat CreateLandedOnEmptyAsteroidBeltImage()
    {
        var image = CreateWarpToAsteroidFieldImage();
        DrawAsteroidBeltLabel(image);
        DrawMineOverview(image, asteroidRowCount: 0);
        return image;
    }

    public static Mat CreateLandedOnAsteroidBeltImageWithMineHeaderLikeIcon()
    {
        var image = CreateLandedOnAsteroidBeltImage();
        DrawMineHeaderLikeIcon(image);
        return image;
    }

    public static void WriteDockedItemHangarFocusedImage(string outputPath)
    {
        using var image = CreateDockedItemHangarFocusedImage();
        Cv2.ImWrite(outputPath, image);
    }

    public static void WriteDockedMiningHoldFocusedEmptyImage(string outputPath)
    {
        using var image = CreateDockedMiningHoldFocusedEmptyImage();
        Cv2.ImWrite(outputPath, image);
    }

    public static void WriteDockedMiningHoldFocusedNotEmptyImage(string outputPath)
    {
        using var image = CreateDockedMiningHoldFocusedNotEmptyImage();
        Cv2.ImWrite(outputPath, image);
    }

    public static void WriteUndockedCompleteImage(string outputPath)
    {
        using var image = CreateUndockedCompleteImage();
        Cv2.ImWrite(outputPath, image);
    }

    public static void WriteWarpToAsteroidFieldImage(string outputPath)
    {
        using var image = CreateWarpToAsteroidFieldImage();
        Cv2.ImWrite(outputPath, image);
    }

    public static void WriteLandedOnAsteroidBeltImage(string outputPath)
    {
        using var image = CreateLandedOnAsteroidBeltImage();
        Cv2.ImWrite(outputPath, image);
    }

    public static void WriteLandedOnEmptyAsteroidBeltImage(string outputPath)
    {
        using var image = CreateLandedOnEmptyAsteroidBeltImage();
        Cv2.ImWrite(outputPath, image);
    }

    public static void WriteLandedOnAsteroidBeltImageWithMineHeaderLikeIcon(string outputPath)
    {
        using var image = CreateLandedOnAsteroidBeltImageWithMineHeaderLikeIcon();
        Cv2.ImWrite(outputPath, image);
    }

    private static Mat CreateDockedBaseImage()
    {
        var image = new Mat(new Size(ImageWidth, ImageHeight), MatType.CV_8UC3, BackgroundColor);
        Cv2.Rectangle(image, new Rect(1975, 40, 560, 1545), new Scalar(8, 8, 8), -1);
        Cv2.Rectangle(image, UndockButtonBounds, FocusedColor, -1);
        Cv2.Rectangle(image, UndockButtonBounds, BorderColor, 2);
        Cv2.PutText(
            image,
            "Undock",
            new Point(UndockButtonBounds.X + 230, UndockButtonBounds.Y + 38),
            HersheyFonts.HersheySimplex,
            0.8,
            TextColor,
            2,
            LineTypes.AntiAlias);
        return image;
    }

    private static void DrawInventory(Mat image, Rect focusedEntryBounds, bool miningHoldContainsOre)
    {
        Cv2.Rectangle(image, new Rect(50, 1497, 454, 510), PanelColor, -1);
        Cv2.Rectangle(image, focusedEntryBounds, FocusedColor, -1);
        Cv2.PutText(
            image,
            "Mining Hold",
            new Point(MiningHoldEntryBounds.X + 45, MiningHoldEntryBounds.Y + 34),
            HersheyFonts.HersheySimplex,
            0.65,
            TextColor,
            1,
            LineTypes.AntiAlias);
        Cv2.PutText(
            image,
            "Item hangar",
            new Point(ItemHangarEntryBounds.X + 45, ItemHangarEntryBounds.Y + 34),
            HersheyFonts.HersheySimplex,
            0.65,
            TextColor,
            1,
            LineTypes.AntiAlias);

        if (!miningHoldContainsOre)
        {
            Cv2.Rectangle(image, MiningHoldContentBounds, new Scalar(10, 10, 10), -1);
        }
    }

    private static void DrawNothingFound(Mat image)
    {
        Cv2.PutText(
            image,
            "Nothing",
            new Point(352, 1685),
            HersheyFonts.HersheySimplex,
            1.05,
            new Scalar(120, 120, 120),
            2,
            LineTypes.AntiAlias);
        Cv2.PutText(
            image,
            "Found",
            new Point(362, 1735),
            HersheyFonts.HersheySimplex,
            1.05,
            new Scalar(120, 120, 120),
            2,
            LineTypes.AntiAlias);
    }

    private static void DrawOreItem(Mat image)
    {
        var oreBounds = new Rect(335, 1630, 82, 80);
        Cv2.Rectangle(image, oreBounds, new Scalar(20, 20, 20), -1);
        Cv2.Ellipse(image, new Point(376, 1670), new Size(43, 31), 0, 0, 360, new Scalar(210, 220, 230), -1, LineTypes.AntiAlias);
        Cv2.Ellipse(image, new Point(389, 1660), new Size(21, 15), 20, 0, 360, new Scalar(130, 150, 170), -1, LineTypes.AntiAlias);
        Cv2.PutText(
            image,
            "157",
            new Point(365, 1702),
            HersheyFonts.HersheySimplex,
            0.55,
            Scalar.Black,
            2,
            LineTypes.AntiAlias);
    }

    private static void DrawAsteroidBeltOverview(Mat image)
    {
        Cv2.Rectangle(image, OverviewBounds, new Scalar(8, 8, 8), -1);
        using var overviewBelt = LoadOverviewBelt();
        PasteTemplate(image, overviewBelt, OverviewBeltButtonBounds.Location);

        for (var rowIndex = 0; rowIndex < 4; rowIndex++)
        {
            DrawAsteroidBeltRow(image, 471 + rowIndex * 36);
        }
    }

    private static void DrawAsteroidBeltLabel(Mat image)
    {
        Cv2.PutText(
            image,
            "ASTEROID BELT",
            new Point(AsteroidBeltLabelBounds.X + 12, AsteroidBeltLabelBounds.Y + 56),
            HersheyFonts.HersheySimplex,
            1.85,
            new Scalar(235, 235, 235),
            3,
            LineTypes.AntiAlias);
    }

    private static void DrawWarpDriveActiveText(Mat image)
    {
        Cv2.PutText(
            image,
            "Warp Drive Active",
            new Point(1110, 1568),
            HersheyFonts.HersheySimplex,
            1.2,
            new Scalar(235, 235, 235),
            2,
            LineTypes.AntiAlias);
        Cv2.PutText(
            image,
            "Destination: Syrikos VII - Asteroid Belt 1",
            new Point(1088, 1605),
            HersheyFonts.HersheySimplex,
            0.68,
            new Scalar(205, 205, 205),
            1,
            LineTypes.AntiAlias);
        Cv2.PutText(
            image,
            "Distance: 0.95 AU",
            new Point(1202, 1634),
            HersheyFonts.HersheySimplex,
            0.68,
            new Scalar(205, 205, 205),
            1,
            LineTypes.AntiAlias);
    }

    private static void DrawMineOverview(Mat image, int asteroidRowCount = 5)
    {
        Cv2.Rectangle(image, MineOverviewBounds, new Scalar(8, 8, 8), -1);
        Cv2.Line(
            image,
            MineOverviewBounds.TopLeft,
            new Point(MineOverviewBounds.Right, MineOverviewBounds.Top),
            new Scalar(34, 34, 34),
            2);
        Cv2.PutText(
            image,
            "Overview (MINE)",
            new Point(MineOverviewBounds.X + 25, MineOverviewBounds.Y + 34),
            HersheyFonts.HersheySimplex,
            0.55,
            TextColor,
            1,
            LineTypes.AntiAlias);

        for (var rowIndex = 0; rowIndex < asteroidRowCount; rowIndex++)
        {
            DrawAsteroidRow(image, 1375 + rowIndex * 36);
        }
    }

    private static void DrawAsteroidBeltRow(Mat image, int centerY)
    {
        var rowBounds = new Rect(1999, centerY - 17, 515, 34);
        Cv2.Rectangle(image, rowBounds, new Scalar(10, 10, 10), -1);
        Cv2.Rectangle(image, new Rect(2003, centerY - 6, 7, 6), new Scalar(130, 130, 130), -1);
        Cv2.Rectangle(image, new Rect(2012, centerY - 6, 7, 6), new Scalar(130, 130, 130), -1);
        Cv2.Rectangle(image, new Rect(2008, centerY + 3, 7, 6), new Scalar(130, 130, 130), -1);
        Cv2.PutText(
            image,
            "Asteroid Belt",
            new Point(2358, centerY + 8),
            HersheyFonts.HersheySimplex,
            0.7,
            TextColor,
            1,
            LineTypes.AntiAlias);
    }

    private static void DrawAsteroidRow(Mat image, int centerY)
    {
        var rowBounds = new Rect(MineOverviewBounds.X + 27, centerY - 17, MineOverviewBounds.Width - 55, 34);
        Cv2.Rectangle(image, rowBounds, new Scalar(9, 9, 9), -1);
        var iconCenter = new Point(MineOverviewBounds.X + 41, centerY);
        var iconPoints = new[]
        {
            new Point(iconCenter.X, iconCenter.Y - 6),
            new Point(iconCenter.X + 7, iconCenter.Y),
            new Point(iconCenter.X, iconCenter.Y + 6),
            new Point(iconCenter.X - 7, iconCenter.Y)
        };
        Cv2.FillConvexPoly(image, iconPoints, new Scalar(120, 120, 120), LineTypes.AntiAlias);
        Cv2.PutText(
            image,
            "20 km",
            new Point(MineOverviewBounds.Right - 92, centerY + 7),
            HersheyFonts.HersheySimplex,
            0.7,
            TextColor,
            1,
            LineTypes.AntiAlias);
    }

    private static void DrawMineHeaderLikeIcon(Mat image)
    {
        var iconCenter = new Point(MineOverviewBounds.X + 41, MineOverviewBounds.Y + 88);
        var iconPoints = new[]
        {
            new Point(iconCenter.X, iconCenter.Y - 5),
            new Point(iconCenter.X + 6, iconCenter.Y),
            new Point(iconCenter.X, iconCenter.Y + 5),
            new Point(iconCenter.X - 6, iconCenter.Y)
        };
        Cv2.FillConvexPoly(image, iconPoints, new Scalar(120, 120, 120), LineTypes.AntiAlias);
    }

    private static Mat LoadLocationChangeTimer()
    {
        using var bitmap = Automaton.Properties.Resources.location_change_timer;
        using var memoryStream = new MemoryStream();
        bitmap.Save(memoryStream, ImageFormat.Png);
        return Cv2.ImDecode(memoryStream.ToArray(), ImreadModes.Color);
    }

    private static void PasteTemplate(Mat image, Mat template, Point location)
    {
        using var region = new Mat(image, new Rect(location.X, location.Y, template.Width, template.Height));
        template.CopyTo(region);
    }

    private static Mat LoadOverviewBelt()
    {
        using var bitmap = Automaton.Properties.Resources.overview_belt;
        using var memoryStream = new MemoryStream();
        bitmap.Save(memoryStream, ImageFormat.Png);
        return Cv2.ImDecode(memoryStream.ToArray(), ImreadModes.Color);
    }

}
