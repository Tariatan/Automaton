using Automaton.Helpers;
using Automaton.Infrastructure;
using OpenCvSharp;

namespace Automaton.Detectors;

internal sealed class PlayfieldDetector : IDisposable
{
    private const int EdgeLowThreshold = 60;
    private const int EdgeHighThreshold = 160;
    private const int StrictPassMaxMatches = 8;
    private const int AdaptivePassMaxMatches = 12;
    private const int MaxRetainedCandidates = 18;
    private const int MinimumMarkersForPlayfield = 3;
    private const double RawMatchThreshold = 0.68;
    private const double EqualizedMatchThreshold = 0.62;
    private const double EdgeMatchThreshold = 0.58;
    private const double AdaptiveRawMatchThreshold = 0.52;
    private const double AdaptiveEdgeMatchThreshold = 0.42;
    private const double RawMatchWeight = 1.00;
    private const double EqualizedMatchWeight = 0.97;
    private const double EdgeMatchWeight = 0.94;
    private const double AdaptiveRawMatchWeight = 0.90;
    private const double AdaptiveEdgeMatchWeight = 0.88;
    private const double CandidateOverlapThreshold = 0.35;
    private const double SuppressionSizeScale = 0.8;
    private const int MarkerScoreWeight = 1_000;
    private const int InferredCornerPenalty = 40_000;
    private const int MinimumPlayfieldDimension = 450;
    private const int MinimumMarkerDimension = 1;
    private const double MinimumPlayfieldAspectRatio = 0.65;
    private const double MaximumPlayfieldAspectRatio = 1.60;
    private const int MaximumAlignmentDelta = 65;
    private const int MaximumSpanDelta = 120;
    private const int ScoreAlignmentPenaltyWeight = 500;

    private readonly Mat m_TemplateGray;
    private readonly Mat m_TemplateEqualized;
    private readonly Mat m_TemplateEdges;
    private readonly Size m_TemplateSize;

    public PlayfieldDetector()
    {
        m_TemplateGray = LoadMarkerFromResources();
        if (m_TemplateGray.Empty())
        {
            throw new InvalidOperationException("Could not load playfield marker template.");
        }

        m_TemplateEqualized = new Mat();
        Cv2.EqualizeHist(m_TemplateGray, m_TemplateEqualized);
        m_TemplateEdges = BuildEdgeMap(m_TemplateEqualized);
        m_TemplateSize = m_TemplateGray.Size();
    }

    public void Dispose()
    {
        m_TemplateGray.Dispose();
        m_TemplateEqualized.Dispose();
        m_TemplateEdges.Dispose();
    }

    public PlayfieldDetectionResult Detect(Mat screenshot)
    {
        using var screenshotGray = new Mat();
        using var screenshotEqualized = new Mat();
        using var screenshotEdges = new Mat();

        Cv2.CvtColor(screenshot, screenshotGray, ColorConversionCodes.BGR2GRAY);
        Cv2.EqualizeHist(screenshotGray, screenshotEqualized);
        Cv2.Canny(screenshotEqualized, screenshotEdges, EdgeLowThreshold, EdgeHighThreshold);

        var markerCandidates = FindMarkerCandidates(screenshotGray, screenshotEqualized, screenshotEdges);
        var markerSet = SelectMarkerSet(markerCandidates, screenshot.Size());

        if (markerSet is null)
        {
            return PlayfieldDetectionResult.NotFound;
        }

        var bounds = BuildPlayfieldBounds(markerSet.Value);
        return new PlayfieldDetectionResult(bounds, markerSet.Value.AllMarkers);
    }

    private List<MarkerCandidate> FindMarkerCandidates(Mat screenshotGray, Mat screenshotEqualized, Mat screenshotEdges)
    {
        var candidates = new List<MarkerCandidate>();

        CollectMatchCandidates(screenshotGray, m_TemplateGray, RawMatchThreshold, RawMatchWeight, StrictPassMaxMatches, candidates);

        if (candidates.Count < 4)
        {
            CollectMatchCandidates(screenshotEqualized, m_TemplateEqualized, EqualizedMatchThreshold, EqualizedMatchWeight, StrictPassMaxMatches, candidates);
        }

        if (candidates.Count < 4)
        {
            CollectMatchCandidates(screenshotEdges, m_TemplateEdges, EdgeMatchThreshold, EdgeMatchWeight, StrictPassMaxMatches, candidates);
        }

        if (candidates.Count < 4)
        {
            CollectMatchCandidates(screenshotGray, m_TemplateGray, AdaptiveRawMatchThreshold, AdaptiveRawMatchWeight, AdaptivePassMaxMatches, candidates);
            CollectMatchCandidates(screenshotEdges, m_TemplateEdges, AdaptiveEdgeMatchThreshold, AdaptiveEdgeMatchWeight, AdaptivePassMaxMatches, candidates);
        }

        return candidates
            .OrderByDescending(candidate => candidate.Score)
            .Take(MaxRetainedCandidates)
            .ToList();
    }

    private void CollectMatchCandidates(
        Mat screenshot,
        Mat template,
        double threshold,
        double scoreWeight,
        int maxMatches,
        List<MarkerCandidate> candidates)
    {
        using var result = new Mat();
        Cv2.MatchTemplate(screenshot, template, result, TemplateMatchModes.CCoeffNormed);

        for (var matchIndex = 0; matchIndex < maxMatches; matchIndex++)
        {
            Cv2.MinMaxLoc(result, out _, out var maxValue, out _, out var maxLocation);
            if (maxValue < threshold)
            {
                break;
            }

            var candidate = new MarkerCandidate(
                new Rect(maxLocation.X, maxLocation.Y, m_TemplateSize.Width, m_TemplateSize.Height),
                maxValue * scoreWeight);

            AddOrMergeCandidate(candidates, candidate);
            SuppressNeighborhood(result, maxLocation, m_TemplateSize);
        }
    }

    private static void AddOrMergeCandidate(List<MarkerCandidate> candidates, MarkerCandidate candidate)
    {
        for (var index = 0; index < candidates.Count; index++)
        {
            if (!Overlaps(candidate.Bounds, candidates[index].Bounds))
            {
                continue;
            }

            if (candidate.Score > candidates[index].Score)
            {
                candidates[index] = candidate;
            }

            return;
        }

        candidates.Add(candidate);
    }

    private static bool Overlaps(Rect left, Rect right)
    {
        var intersection = left & right;
        if (intersection.Width <= 0 || intersection.Height <= 0)
        {
            return false;
        }

        var overlapArea = intersection.Width * intersection.Height;
        var minimumArea = Math.Min(left.Width * left.Height, right.Width * right.Height);
        return overlapArea >= minimumArea * CandidateOverlapThreshold;
    }

    private static void SuppressNeighborhood(Mat result, Point location, Size templateSize)
    {
        var suppressionWidth = (int)Math.Round(templateSize.Width * SuppressionSizeScale);
        var suppressionHeight = (int)Math.Round(templateSize.Height * SuppressionSizeScale);

        var x = Math.Max(0, location.X - suppressionWidth / 2);
        var y = Math.Max(0, location.Y - suppressionHeight / 2);
        var width = Math.Min(result.Width - x, suppressionWidth);
        var height = Math.Min(result.Height - y, suppressionHeight);

        using var roi = new Mat(result, new Rect(x, y, width, height));
        roi.SetTo(new Scalar(0));
    }

    private static MarkerSet? SelectMarkerSet(IReadOnlyList<MarkerCandidate> candidates, Size imageSize)
    {
        if (candidates.Count < MinimumMarkersForPlayfield)
        {
            return null;
        }

        var count = candidates.Count;
        var bounds = new Rect[count];
        var scores = new double[count];
        for (var i = 0; i < count; i++)
        {
            bounds[i] = candidates[i].Bounds;
            scores[i] = candidates[i].Score;
        }

        MarkerSet? best = null;
        var bestScore = double.NegativeInfinity;

        for (var i = 0; i < count; i++)
        {
            for (var j = i + 1; j < count; j++)
            {
                for (var k = j + 1; k < count; k++)
                {
                    var scoreSum3 = (scores[i] + scores[j] + scores[k]) * MarkerScoreWeight;
                    EvaluateThreeMarkers(bounds[i], bounds[j], bounds[k], scoreSum3, imageSize, ref best, ref bestScore);

                    for (var m = k + 1; m < count; m++)
                    {
                        var scoreSum4 = scoreSum3 + scores[m] * MarkerScoreWeight;
                        EvaluateFourMarkers(bounds[i], bounds[j], bounds[k], bounds[m], scoreSum4, imageSize, ref best, ref bestScore);
                    }
                }
            }
        }

        return best;
    }

    private static void EvaluateThreeMarkers(
        Rect b0, Rect b1, Rect b2,
        double weightedScoreSum, Size imageSize,
        ref MarkerSet? best, ref double bestScore)
    {
        if (!TryBuildMarkerSetFromThree(b0, b1, b2, out var set) || !IsValid(set, imageSize))
        {
            return;
        }

        var score = Score(set) + weightedScoreSum - InferredCornerPenalty;
        if (score > bestScore)
        {
            best = set;
            bestScore = score;
        }
    }

    private static void EvaluateFourMarkers(
        Rect b0, Rect b1, Rect b2, Rect b3,
        double weightedScoreSum, Size imageSize,
        ref MarkerSet? best, ref double bestScore)
    {
        var set = BuildMarkerSetFromFour(b0, b1, b2, b3);
        if (!IsValid(set, imageSize))
        {
            return;
        }

        var score = Score(set) + weightedScoreSum;
        if (score > bestScore)
        {
            best = set;
            bestScore = score;
        }
    }

    private static MarkerSet BuildMarkerSetFromFour(Rect r0, Rect r1, Rect r2, Rect r3)
    {
        if (GeometryHelper.CenterY(r0) > GeometryHelper.CenterY(r1)) (r0, r1) = (r1, r0);
        if (GeometryHelper.CenterY(r2) > GeometryHelper.CenterY(r3)) (r2, r3) = (r3, r2);
        if (GeometryHelper.CenterY(r0) > GeometryHelper.CenterY(r2)) (r0, r2) = (r2, r0);
        if (GeometryHelper.CenterY(r1) > GeometryHelper.CenterY(r3)) (r1, r3) = (r3, r1);
        if (GeometryHelper.CenterY(r1) > GeometryHelper.CenterY(r2)) (r1, r2) = (r2, r1);

        if (r0.X > r1.X) (r0, r1) = (r1, r0);
        if (r2.X > r3.X) (r2, r3) = (r3, r2);

        return new MarkerSet(r0, r1, r2, r3, null);
    }

    private static bool TryBuildMarkerSetFromThree(Rect r0, Rect r1, Rect r2, out MarkerSet set)
    {
        var leftX = Math.Min(r0.X, Math.Min(r1.X, r2.X));
        var rightX = Math.Max(r0.X, Math.Max(r1.X, r2.X));
        var topY = Math.Min(r0.Y, Math.Min(r1.Y, r2.Y));
        var bottomY = Math.Max(r0.Y, Math.Max(r1.Y, r2.Y));
        var averageWidth = (int)Math.Round((r0.Width + r1.Width + r2.Width) / 3.0);
        var averageHeight = (int)Math.Round((r0.Height + r1.Height + r2.Height) / 3.0);

        var c0 = ClassifyCorner(r0, leftX, rightX, topY, bottomY);
        var c1 = ClassifyCorner(r1, leftX, rightX, topY, bottomY);
        var c2 = ClassifyCorner(r2, leftX, rightX, topY, bottomY);

        if (c0 == c1 || c0 == c2 || c1 == c2)
        {
            set = default;
            return false;
        }

        var inferredCorner = (Corner)(6 - (int)c0 - (int)c1 - (int)c2);
        var inferredRect = inferredCorner switch
        {
            Corner.TopLeft => new Rect(leftX, topY, averageWidth, averageHeight),
            Corner.TopRight => new Rect(rightX, topY, averageWidth, averageHeight),
            Corner.BottomLeft => new Rect(leftX, bottomY, averageWidth, averageHeight),
            Corner.BottomRight => new Rect(rightX, bottomY, averageWidth, averageHeight),
            _ => throw new ArgumentOutOfRangeException(nameof(inferredCorner), inferredCorner, null)
        };

        set = new MarkerSet(
            CornerRect(Corner.TopLeft),
            CornerRect(Corner.TopRight),
            CornerRect(Corner.BottomLeft),
            CornerRect(Corner.BottomRight),
            inferredCorner);
        return true;

        Rect CornerRect(Corner target) =>
            c0 == target ? r0 : c1 == target ? r1 : c2 == target ? r2 : inferredRect;
    }

    private static Corner ClassifyCorner(Rect marker, int leftX, int rightX, int topY, int bottomY)
    {
        var isLeft = Math.Abs(marker.X - leftX) <= Math.Abs(marker.X - rightX);
        var isTop = Math.Abs(marker.Y - topY) <= Math.Abs(marker.Y - bottomY);

        return (isLeft, isTop) switch
        {
            (true, true) => Corner.TopLeft,
            (false, true) => Corner.TopRight,
            (true, false) => Corner.BottomLeft,
            _ => Corner.BottomRight
        };
    }

    private static bool IsValid(MarkerSet set, Size imageSize)
    {
        var playfield = BuildPlayfieldBounds(set);
        if (playfield.Width < MinimumPlayfieldDimension || playfield.Height < MinimumPlayfieldDimension)
        {
            return false;
        }

        if (playfield.X < 0 || playfield.Y < 0 || playfield.Right > imageSize.Width || playfield.Bottom > imageSize.Height)
        {
            return false;
        }

        var aspectRatio = playfield.Width / (double)playfield.Height;
        if (aspectRatio is < MinimumPlayfieldAspectRatio or > MaximumPlayfieldAspectRatio)
        {
            return false;
        }

        var topAlignment = Math.Abs(GeometryHelper.CenterY(set.TopLeft) - GeometryHelper.CenterY(set.TopRight));
        var bottomAlignment = Math.Abs(GeometryHelper.CenterY(set.BottomLeft) - GeometryHelper.CenterY(set.BottomRight));
        var leftAlignment = Math.Abs(GeometryHelper.CenterX(set.TopLeft) - GeometryHelper.CenterX(set.BottomLeft));
        var rightAlignment = Math.Abs(GeometryHelper.CenterX(set.TopRight) - GeometryHelper.CenterX(set.BottomRight));

        if (topAlignment > MaximumAlignmentDelta || bottomAlignment > MaximumAlignmentDelta || leftAlignment > MaximumAlignmentDelta || rightAlignment > MaximumAlignmentDelta)
        {
            return false;
        }

        var topSpan = (set.TopRight.X + set.TopRight.Width) - set.TopLeft.X;
        var bottomSpan = (set.BottomRight.X + set.BottomRight.Width) - set.BottomLeft.X;
        var leftSpan = (set.BottomLeft.Y + set.BottomLeft.Height) - set.TopLeft.Y;
        var rightSpan = (set.BottomRight.Y + set.BottomRight.Height) - set.TopRight.Y;

        return Math.Abs(topSpan - bottomSpan) <= MaximumSpanDelta && Math.Abs(leftSpan - rightSpan) <= MaximumSpanDelta;
    }

    private static Rect BuildPlayfieldBounds(MarkerSet set)
    {
        var left = set.TopLeft.X;
        var top = set.TopLeft.Y;
        var right = set.TopRight.X + set.TopRight.Width;
        var bottom = set.BottomLeft.Y + set.BottomLeft.Height;
        return new Rect(left, top, Math.Max(MinimumMarkerDimension, right - left), Math.Max(MinimumMarkerDimension, bottom - top));
    }

    private static double Score(MarkerSet set)
    {
        var bounds = BuildPlayfieldBounds(set);
        var alignmentPenalty =
            Math.Abs(GeometryHelper.CenterY(set.TopLeft) - GeometryHelper.CenterY(set.TopRight)) +
            Math.Abs(GeometryHelper.CenterY(set.BottomLeft) - GeometryHelper.CenterY(set.BottomRight)) +
            Math.Abs(GeometryHelper.CenterX(set.TopLeft) - GeometryHelper.CenterX(set.BottomLeft)) +
            Math.Abs(GeometryHelper.CenterX(set.TopRight) - GeometryHelper.CenterX(set.BottomRight));

        return (bounds.Width * bounds.Height) - (alignmentPenalty * ScoreAlignmentPenaltyWeight);
    }

    private static Mat LoadMarkerFromResources()
    {
        return EmbeddedResourceLoader.LoadMat("discovery.marker.png", ImreadModes.Grayscale);
    }

    private static Mat BuildEdgeMap(Mat input)
    {
        var edges = new Mat();
        Cv2.Canny(input, edges, EdgeLowThreshold, EdgeHighThreshold);
        return edges;
    }

    private readonly record struct MarkerCandidate(Rect Bounds, double Score);

    private readonly record struct MarkerSet(
        Rect TopLeft,
        Rect TopRight,
        Rect BottomLeft,
        Rect BottomRight,
        Corner? InferredCorner)
    {
        public Rect[] AllMarkers => [TopLeft, TopRight, BottomLeft, BottomRight];
    }

    private enum Corner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }
}

internal sealed record PlayfieldDetectionResult(Rect Bounds, IReadOnlyList<Rect> MarkerBounds)
{
    public static PlayfieldDetectionResult NotFound { get; } = new(new Rect(), []);

    public bool IsFound => Bounds is { Width: > 0, Height: > 0 };
}
