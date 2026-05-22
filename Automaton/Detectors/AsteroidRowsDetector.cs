using OpenCvSharp;

namespace Automaton.Detectors;

internal static class AsteroidRowsDetector
{
    private const int MinimumAsteroidIconArea = 2;
    private const int MaximumAsteroidIconWidth = 20;
    private const int MaximumAsteroidIconHeight = 20;
    private const int AsteroidIconGroupMaximumDistance = 18;
    private const int MinimumAsteroidRowCenterOffsetFromMineOverviewTop = 90;
    private const int MinimumDistanceColumnBrightPixelCount = 10;

    public static IReadOnlyList<AsteroidOverviewEntry> Locate(Mat screen, Rect mineOverviewBounds)
    {
        if (screen.Empty())
        {
            return [];
        }

        var iconColumnBounds = BuildMineAsteroidIconColumnBounds(screen.Size(), mineOverviewBounds);
        using var iconColumn = new Mat(screen, iconColumnBounds);
        using var gray = new Mat();
        using var mask = new Mat();
        Cv2.CvtColor(iconColumn, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.InRange(gray, new Scalar(65), new Scalar(190), mask);
        Cv2.FindContours(
            mask,
            out var contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        var iconCenters = (from contour
            in contours
            let bounds = Cv2.BoundingRect(contour)
            where !(Cv2.ContourArea(contour) < MinimumAsteroidIconArea)
                  && bounds is { Width: <= MaximumAsteroidIconWidth, Height: <= MaximumAsteroidIconHeight }
            select iconColumnBounds.Y + bounds.Y + bounds.Height / 2
            into iconCenterY
            where iconCenterY - mineOverviewBounds.Y >= MinimumAsteroidRowCenterOffsetFromMineOverviewTop
            select iconCenterY)
            .ToList();

        var rowLeft = Math.Clamp(mineOverviewBounds.X + 28, 0, Math.Max(0, screen.Width - 1));
        var rowWidth = Math.Clamp(mineOverviewBounds.Width - 55, 1, screen.Width - rowLeft);
        return GroupIconCenters(iconCenters)
            .Select(group => (int)Math.Round(group.Average()))
            .Select(centerY =>
            {
                var rowTop = Math.Clamp(centerY - 17, 0, Math.Max(0, screen.Height - 1));
                var rowHeight = Math.Clamp(34, 1, screen.Height - rowTop);
                return new AsteroidOverviewEntry(new Rect(rowLeft, rowTop, rowWidth, rowHeight));
            })
            .Where(row => RowLooksSelectable(screen, row.Bounds))
            .OrderBy(row => row.Bounds.Y)
            .ToArray();
    }

    private static IReadOnlyList<IReadOnlyList<int>> GroupIconCenters(IReadOnlyList<int> iconCenters)
    {
        var groups = new List<List<int>>();
        foreach (var center in iconCenters.Order())
        {
            var currentGroup = groups.LastOrDefault();
            if (currentGroup is null ||
                Math.Abs(center - currentGroup.Average()) > AsteroidIconGroupMaximumDistance)
            {
                groups.Add([center]);
                continue;
            }

            currentGroup.Add(center);
        }

        return groups;
    }

    private static Rect BuildMineAsteroidIconColumnBounds(Size imageSize, Rect mineOverviewBounds)
    {
        var left = Math.Clamp(mineOverviewBounds.X + 30, 0, Math.Max(0, imageSize.Width - 1));
        var top = Math.Clamp(mineOverviewBounds.Y + 96, 0, Math.Max(0, imageSize.Height - 1));
        var bottom = Math.Min(imageSize.Height, mineOverviewBounds.Bottom);
        var width = Math.Clamp(70, 1, imageSize.Width - left);
        var height = Math.Clamp(bottom - top, 1, imageSize.Height - top);
        return new Rect(left, top, width, height);
    }

    private static bool RowLooksSelectable(Mat screen, Rect rowBounds)
    {
        var probeLeft = Math.Clamp(rowBounds.Right - 84, 0, Math.Max(0, screen.Width - 1));
        var probeTop = Math.Clamp(rowBounds.Top + 3, 0, Math.Max(0, screen.Height - 1));
        var probeRight = Math.Clamp(rowBounds.Right - 6, probeLeft + 1, screen.Width);
        var probeBottom = Math.Clamp(rowBounds.Bottom - 3, probeTop + 1, screen.Height);
        var probeBounds = new Rect(probeLeft, probeTop, probeRight - probeLeft, probeBottom - probeTop);

        using var probe = new Mat(screen, probeBounds);
        using var gray = new Mat();
        using var mask = new Mat();
        Cv2.CvtColor(probe, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.InRange(gray, new Scalar(120), new Scalar(255), mask);
        return Cv2.CountNonZero(mask) >= MinimumDistanceColumnBrightPixelCount;
    }
}

internal sealed record AsteroidOverviewEntry(Rect Bounds);
