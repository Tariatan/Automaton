using Automaton.Infrastructure;
using OpenCvSharp;

namespace Automaton.Detectors;

internal sealed class AsteroidBeltOverviewDetector
{
    private const double MinimumButtonMatchScore = 0.90;
    private const double MinimumHomeStationMatchScore = 0.76;
    private const int MinimumAsteroidIconPartArea = 3;
    private const int MaximumAsteroidIconPartWidth = 24;
    private const int MaximumAsteroidIconPartHeight = 24;
    private const int MinimumAsteroidIconPartCount = 1;
    private const int AsteroidIconGroupMaximumDistance = 18;
    private const int MinimumDistanceColumnBrightPixelCount = 10;
    private const int MaximumBeltListHeightFromHomeStationTop = 960;
    private const int OverviewRegionLeft = 1960;
    private const int OverviewRegionTop = 280;
    private const int OverviewRegionWidth = 580;
    private const int OverviewRegionHeight = 1330;
    private static readonly double[] TemplateScales = [1.0, 0.95, 1.05];
    private static readonly double[] HomeStationTemplateScales = [0.80, 0.90, 1.0, 1.10, 1.20, 1.30];

    private readonly Mat m_OverviewBeltTemplate = EmbeddedResourceLoader.LoadMat("overview.overview_belt.png");
    private readonly Mat m_HomeStationTemplate = EmbeddedResourceLoader.LoadMat("overview.home_station.png");

    public AsteroidBeltOverviewAnalysis Analyze(Mat screen)
    {
        if (screen.Empty())
        {
            return AsteroidBeltOverviewAnalysis.NotFound;
        }

        using var searchableScreen = BuildSearchableScreen(screen);
        var overviewSearchBounds = BuildOverviewSearchBounds(searchableScreen.Size());
        var overviewBeltButtonBounds = TryLocateTemplate(
            searchableScreen,
            m_OverviewBeltTemplate,
            overviewSearchBounds,
            TemplateScales,
            MinimumButtonMatchScore,
            out var overviewBeltButtonLocation)
            ? overviewBeltButtonLocation.Bounds
            : (Rect?)null;
        Rect? overviewBounds = overviewBeltButtonBounds is null
            ? null
            : BuildOverviewBounds(searchableScreen.Size());
        var homeStationBounds = overviewBounds is null || overviewBeltButtonBounds is null
            ? null
            : TryLocateTemplate(
                searchableScreen,
                m_HomeStationTemplate,
                BuildHomeStationSearchBounds(searchableScreen.Size(), overviewBounds.Value, overviewBeltButtonBounds.Value),
                HomeStationTemplateScales,
                MinimumHomeStationMatchScore,
                out var homeStationLocation)
                ? homeStationLocation.Bounds
                : (Rect?)null;
        var asteroidBelts = overviewBounds is null ||
                            homeStationBounds is null
            ? []
            : LocateAsteroidBelts(searchableScreen, overviewBounds.Value, homeStationBounds.Value);

        return new AsteroidBeltOverviewAnalysis(
            overviewBounds is not null,
            overviewBounds,
            overviewBeltButtonBounds,
            homeStationBounds,
            asteroidBelts);
    }

    private static IReadOnlyList<AsteroidBeltOverviewEntry> LocateAsteroidBelts(
        Mat screen,
        Rect overviewBounds,
        Rect homeStationBounds)
    {
        var iconColumnBounds = BuildAsteroidIconColumnBounds(screen.Size(), overviewBounds, homeStationBounds);
        using var iconColumn = new Mat(screen, iconColumnBounds);
        using var gray = new Mat();
        using var mask = new Mat();
        Cv2.CvtColor(iconColumn, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.InRange(gray, new Scalar(60), new Scalar(220), mask);
        Cv2.FindContours(
            mask,
            out var contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        var iconPartCenters = (from contour
            in contours
            let bounds = Cv2.BoundingRect(contour)
            where !(Cv2.ContourArea(contour) < MinimumAsteroidIconPartArea)
                  && bounds is { Width: <= MaximumAsteroidIconPartWidth, Height: <= MaximumAsteroidIconPartHeight }
            select iconColumnBounds.Y + bounds.Y + bounds.Height / 2)
            .ToList();

        var groups = GroupIconPartCenters(iconPartCenters);
        var rowLeft = Math.Clamp(overviewBounds.X + 25, 0, screen.Width - 1);
        var rowWidth = Math.Clamp(overviewBounds.Width - 55, 1, screen.Width - rowLeft);
        var rows = (from @group
            in groups
            where @group.Count >= MinimumAsteroidIconPartCount
            select (int)Math.Round(@group.Average())
            into centerY
            select Math.Clamp(centerY - 17, 0, Math.Max(0, screen.Height - 1))
            into rowTop
            let rowHeight = Math.Clamp(34, 1, screen.Height - rowTop)
            select new AsteroidBeltOverviewEntry(new Rect(rowLeft, rowTop, rowWidth, rowHeight)))
            .Where(row => RowLooksSelectable(screen, row.Bounds))
            .ToList();

        return rows
            .OrderBy(row => row.Bounds.Y)
            .ToArray();
    }

    private static IReadOnlyList<IReadOnlyList<int>> GroupIconPartCenters(IReadOnlyList<int> iconPartCenters)
    {
        var groups = new List<List<int>>();
        foreach (var center in iconPartCenters.Order())
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

    private static bool TryLocateTemplate(
        Mat screen,
        Mat template,
        Rect searchBounds,
        IReadOnlyList<double> templateScales,
        double minimumMatchScore,
        out TemplateLocation location)
    {
        location = default;
        using var searchRegion = new Mat(screen, searchBounds);
        TemplateLocation? bestLocation = null;
        foreach (var scale in templateScales)
        {
            using var scaledTemplate = BuildScaledTemplate(template, scale);
            if (scaledTemplate.Width > searchRegion.Width ||
                scaledTemplate.Height > searchRegion.Height)
            {
                continue;
            }

            using var result = new Mat();
            Cv2.MatchTemplate(searchRegion, scaledTemplate, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out var score, out _, out var locationPoint);
            var bounds = new Rect(
                searchBounds.X + locationPoint.X,
                searchBounds.Y + locationPoint.Y,
                scaledTemplate.Width,
                scaledTemplate.Height);
            if (bestLocation is null || score > bestLocation.Value.Score)
            {
                bestLocation = new TemplateLocation(bounds, score);
            }
        }

        if (bestLocation is null || bestLocation.Value.Score < minimumMatchScore)
        {
            return false;
        }

        location = bestLocation.Value;
        return true;
    }

    private static Rect BuildOverviewSearchBounds(Size imageSize)
    {
        var left = Math.Clamp(OverviewRegionLeft, 0, Math.Max(0, imageSize.Width - 1));
        var top = Math.Clamp(OverviewRegionTop, 0, Math.Max(0, imageSize.Height - 1));
        var right = Math.Clamp(OverviewRegionLeft + OverviewRegionWidth, left + 1, imageSize.Width);
        var bottom = Math.Clamp(OverviewRegionTop + OverviewRegionHeight, top + 1, imageSize.Height);
        return new Rect(left, top, right - left, bottom - top);
    }

    private static Rect BuildOverviewBounds(Size imageSize)
    {
        return BuildOverviewSearchBounds(imageSize);
    }

    private static Rect BuildHomeStationSearchBounds(
        Size imageSize,
        Rect overviewBounds,
        Rect overviewBeltButtonBounds)
    {
        var left = Math.Clamp(overviewBounds.X + 20, 0, Math.Max(0, imageSize.Width - 1));
        var top = Math.Clamp(overviewBeltButtonBounds.Bottom + 40, 0, Math.Max(0, imageSize.Height - 1));
        var right = Math.Clamp(overviewBounds.X + 220, left + 1, imageSize.Width);
        var bottom = Math.Clamp(overviewBounds.Y + 340, top + 1, imageSize.Height);
        return new Rect(left, top, right - left, bottom - top);
    }

    private static Rect BuildAsteroidIconColumnBounds(
        Size imageSize,
        Rect overviewBounds,
        Rect homeStationBounds)
    {
        var left = Math.Clamp(overviewBounds.X + 15, 0, Math.Max(0, imageSize.Width - 1));
        var top = Math.Clamp(homeStationBounds.Y + 45, 0, Math.Max(0, imageSize.Height - 1));
        var beltListBottom = Math.Clamp(
            homeStationBounds.Y + MaximumBeltListHeightFromHomeStationTop,
            top + 1,
            imageSize.Height);
        var bottom = Math.Min(overviewBounds.Bottom, beltListBottom);
        var width = Math.Clamp(60, 1, imageSize.Width - left);
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

    private static Mat BuildSearchableScreen(Mat screen)
    {
        if (screen.Channels() == 3)
        {
            return screen.Clone();
        }

        var colorScreen = new Mat();
        Cv2.CvtColor(screen, colorScreen, ColorConversionCodes.GRAY2BGR);
        return colorScreen;
    }

    private static Mat BuildScaledTemplate(Mat template, double scale)
    {
        if (Math.Abs(scale - 1.0) < double.Epsilon)
        {
            return template.Clone();
        }

        var width = Math.Max(1, (int)Math.Round(template.Width * scale));
        var height = Math.Max(1, (int)Math.Round(template.Height * scale));
        var scaledTemplate = new Mat();
        Cv2.Resize(template, scaledTemplate, new Size(width, height));
        return scaledTemplate;
    }

    private readonly record struct TemplateLocation(Rect Bounds, double Score);
}

internal sealed record AsteroidBeltOverviewAnalysis(
    bool OverviewLocated,
    Rect? OverviewBounds,
    Rect? OverviewBeltButtonBounds,
    Rect? HomeStationBounds,
    IReadOnlyList<AsteroidBeltOverviewEntry> AsteroidBelts)
{
    public static AsteroidBeltOverviewAnalysis NotFound { get; } = new(
        false,
        null,
        null,
        null,
        []);
}

internal sealed record AsteroidBeltOverviewEntry(Rect Bounds);
