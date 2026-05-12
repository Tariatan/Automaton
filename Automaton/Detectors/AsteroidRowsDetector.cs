using OpenCvSharp;

namespace Automaton;

internal sealed class AsteroidRowsDetector
{
    private const int MinimumAsteroidIconArea = 2;
    private const int MaximumAsteroidIconWidth = 20;
    private const int MaximumAsteroidIconHeight = 20;
    private const int AsteroidIconGroupMaximumDistance = 18;
    private const int MinimumAsteroidRowCenterOffsetFromMineOverviewTop = 90;

    public IReadOnlyList<AsteroidOverviewEntry> Locate(Mat screen, Rect mineOverviewBounds)
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

        var iconCenters = new List<int>();
        foreach (var contour in contours)
        {
            var bounds = Cv2.BoundingRect(contour);
            if (Cv2.ContourArea(contour) < MinimumAsteroidIconArea ||
                bounds.Width > MaximumAsteroidIconWidth ||
                bounds.Height > MaximumAsteroidIconHeight)
            {
                continue;
            }

            var iconCenterY = iconColumnBounds.Y + bounds.Y + bounds.Height / 2;
            if (iconCenterY - mineOverviewBounds.Y < MinimumAsteroidRowCenterOffsetFromMineOverviewTop)
            {
                continue;
            }

            iconCenters.Add(iconCenterY);
        }

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
        var top = Math.Clamp(mineOverviewBounds.Y + 80, 0, Math.Max(0, imageSize.Height - 1));
        var bottom = Math.Min(imageSize.Height, mineOverviewBounds.Bottom);
        var width = Math.Clamp(70, 1, imageSize.Width - left);
        var height = Math.Clamp(bottom - top, 1, imageSize.Height - top);
        return new Rect(left, top, width, height);
    }
}
