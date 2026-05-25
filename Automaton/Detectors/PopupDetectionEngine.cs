using Automaton.Infrastructure;
using OpenCvSharp;
using Serilog;

namespace Automaton.Detectors;

internal static class PopupDetectionEngine
{
    private const int ExpectedPopupLeft = 960;
    private const int ExpectedPopupTop = 830;
    private const int ExpectedPopupWidth = 630;
    private const int ExpectedPopupHeight = 390;
    private const int PopupSearchMarginX = 190;
    private const int PopupSearchMarginY = 130;
    private static readonly Lazy<PopupTemplates> SPopupTemplates = new(PopupTemplates.Load);

    public static PopupDetection DetectPopup(Mat image)
    {
        if (image.Empty())
        {
            return new PopupDetection(PopupState.None, new Rect());
        }

        var searchBounds = BuildClampedBounds(
            ExpectedPopupLeft - PopupSearchMarginX,
            ExpectedPopupTop - PopupSearchMarginY,
            ExpectedPopupWidth + (PopupSearchMarginX * 2),
            ExpectedPopupHeight + (PopupSearchMarginY * 2),
            image.Size());

        var popupBounds = BuildClampedBounds(
            ExpectedPopupLeft,
            ExpectedPopupTop,
            ExpectedPopupWidth,
            ExpectedPopupHeight,
            image.Size());

        var score = ScorePopup(image, searchBounds, popupBounds);
        var state = Classify(score);
        if (state != PopupState.None)
        {
            return new PopupDetection(state, popupBounds);
        }

        if (!TryFindPopupBoundsByTitleAnchor(image, searchBounds, out var anchoredPopupBounds))
        {
            return new PopupDetection(PopupState.None, popupBounds);
        }

        var anchoredScore = ScorePopup(image, searchBounds, anchoredPopupBounds);
        var anchoredState = Classify(anchoredScore);
        return new PopupDetection(anchoredState, anchoredPopupBounds);
    }

    private static PopupState Classify(PopupScore score)
    {
        var bestTitle = Math.Max(score.TitleSlowDown, score.TitleMaxSubmissions);
        var popupExists = (score.ButtonOk >= PopupDetectorOptions.ButtonThreshold &&
                           score.IconInfo >= PopupDetectorOptions.IconThreshold) ||
                          score.ButtonOk >= PopupDetectorOptions.StrongThreshold ||
                          score.IconInfo >= PopupDetectorOptions.StrongThreshold ||
                          bestTitle >= PopupDetectorOptions.StrongTitleThreshold;
        if (!popupExists)
        {
            return PopupState.None;
        }

        var titleKind = ResolveTitleSignal(score);

        var titleResult = titleKind switch
        {
            TitleSignalKind.SlowDown => PopupState.SlowDown,
            TitleSignalKind.MaximumSubmissions => PopupState.MaxSubmissions,
            _ => PopupState.Unknown
        };

        if (titleResult != PopupState.Unknown)
        {
            return titleResult;
        }

        var bestOkTitle = Math.Max(score.TitleSlowDown, score.TitleMaxSubmissions);
        if (bestOkTitle >= PopupDetectorOptions.TitleThreshold * 0.85)
        {
            return score.TitleSlowDown >= score.TitleMaxSubmissions
                ? PopupState.SlowDown
                : PopupState.MaxSubmissions;
        }

        return PopupState.Unknown;
    }

    private static TitleSignalKind ResolveTitleSignal(PopupScore score)
    {
        var candidates = new[]
        {
            (Kind: TitleSignalKind.SlowDown, Score: score.TitleSlowDown),
            (Kind: TitleSignalKind.MaximumSubmissions, Score: score.TitleMaxSubmissions)
        };
        var best = candidates.MaxBy(candidate => candidate.Score);
        var second = candidates.OrderByDescending(candidate => candidate.Score).Skip(1).First();
        if (best.Score < PopupDetectorOptions.TitleThreshold)
        {
            return TitleSignalKind.None;
        }

        return (best.Score - second.Score) < PopupDetectorOptions.MinimumTitleScoreGap
            ? TitleSignalKind.Ambiguous
            : best.Kind;
    }

    private static PopupScore ScorePopup(Mat image, Rect searchBounds, Rect popupBounds)
    {
        using var searchRegion = new Mat(image, searchBounds);
        using var searchGray = new Mat();
        Cv2.CvtColor(searchRegion, searchGray, ColorConversionCodes.BGR2GRAY);

        var localPopupBounds = new Rect(
            popupBounds.X - searchBounds.X,
            popupBounds.Y - searchBounds.Y,
            popupBounds.Width,
            popupBounds.Height);
        var templates = SPopupTemplates.Value;
        var iconBounds = BuildRelativeBounds(localPopupBounds, 0.02, 0.02, 0.20, 0.28);
        var buttonBounds = BuildRelativeBounds(localPopupBounds, 0.03, 0.74, 0.94, 0.20);
        var titleBounds = BuildRelativeBounds(localPopupBounds, 0.16, 0.02, 0.80, 0.34);

        using var iconRoi = new Mat(searchGray, iconBounds);
        using var buttonRoi = new Mat(searchGray, buttonBounds);
        using var titleRoi = new Mat(searchGray, titleBounds);

        using var titleRoiBinary = ToBinaryMask(titleRoi);

        return new PopupScore(
            ButtonOk: MatchTemplateScore(buttonRoi, templates.ButtonOkGray),
            IconInfo: MatchTemplateScore(iconRoi, templates.IconInfoGray),
            TitleSlowDown: Math.Max(
                MatchTemplateScore(titleRoi, templates.TitleSlowDownGray),
                MatchTemplateScore(titleRoiBinary, templates.TitleSlowDownBinary)),
            TitleMaxSubmissions: Math.Max(
                MatchTemplateScore(titleRoi, templates.TitleMaxSubmissionsGray),
                MatchTemplateScore(titleRoiBinary, templates.TitleMaxSubmissionsBinary)),
            PopupBounds: popupBounds);
    }

    private static bool TryFindPopupBoundsByTitleAnchor(Mat image, Rect searchBounds, out Rect popupBounds)
    {
        using var searchRegion = new Mat(image, searchBounds);
        using var searchGray = new Mat();
        Cv2.CvtColor(searchRegion, searchGray, ColorConversionCodes.BGR2GRAY);
        using var searchBinary = ToBinaryMask(searchGray);

        var templates = SPopupTemplates.Value;
        var maxGray = MatchTemplate(searchGray, templates.TitleMaxSubmissionsGray);
        var maxBinary = MatchTemplate(searchBinary, templates.TitleMaxSubmissionsBinary);
        var slowGray = MatchTemplate(searchGray, templates.TitleSlowDownGray);
        var slowBinary = MatchTemplate(searchBinary, templates.TitleSlowDownBinary);

        var candidates = new[]
        {
            maxGray,
            maxBinary,
            slowGray,
            slowBinary
        };
        var best = candidates.MaxBy(candidate => candidate.Score);
        if (best.Score < PopupDetectorOptions.TitleAnchorThreshold)
        {
            popupBounds = default;
            return false;
        }

        var estimatedPopupX = searchBounds.X + best.Location.X - (int)Math.Round(ExpectedPopupWidth * 0.20);
        var estimatedPopupY = searchBounds.Y + best.Location.Y - (int)Math.Round(ExpectedPopupHeight * 0.06);
        popupBounds = BuildClampedBounds(
            estimatedPopupX,
            estimatedPopupY,
            ExpectedPopupWidth,
            ExpectedPopupHeight,
            image.Size());
        return true;
    }

    private static Mat ToBinaryMask(Mat gray)
    {
        var binary = new Mat();
        Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        return binary;
    }

    private static double MatchTemplateScore(Mat searchRoi, Mat template)
    {
        if (searchRoi.Empty() || template.Empty())
        {
            return 0.0;
        }

        if (template.Width > searchRoi.Width || template.Height > searchRoi.Height)
        {
            return 0.0;
        }

        var resultWidth = searchRoi.Width - template.Width + 1;
        var resultHeight = searchRoi.Height - template.Height + 1;
        using var result = new Mat(new Size(resultWidth, resultHeight), MatType.CV_32FC1);
        Cv2.MatchTemplate(searchRoi, template, result, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out var maxValue, out _, out _);
        return maxValue;
    }

    private static TemplateMatchResult MatchTemplate(Mat searchRoi, Mat template)
    {
        if (searchRoi.Empty() || template.Empty())
        {
            return new TemplateMatchResult(0.0, new Point(0, 0));
        }

        if (template.Width > searchRoi.Width || template.Height > searchRoi.Height)
        {
            return new TemplateMatchResult(0.0, new Point(0, 0));
        }

        var resultWidth = searchRoi.Width - template.Width + 1;
        var resultHeight = searchRoi.Height - template.Height + 1;
        using var result = new Mat(new Size(resultWidth, resultHeight), MatType.CV_32FC1);
        Cv2.MatchTemplate(searchRoi, template, result, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out var maxValue, out _, out var maxLocation);
        return new TemplateMatchResult(maxValue, maxLocation);
    }

    private static Rect BuildRelativeBounds(Rect bounds, double leftRatio, double topRatio, double widthRatio, double heightRatio)
    {
        return BuildClampedBounds(
            bounds.X + (int)Math.Round(bounds.Width * leftRatio),
            bounds.Y + (int)Math.Round(bounds.Height * topRatio),
            Math.Max(1, (int)Math.Round(bounds.Width * widthRatio)),
            Math.Max(1, (int)Math.Round(bounds.Height * heightRatio)),
            new Size(bounds.Right, bounds.Bottom));
    }

    private static Rect BuildClampedBounds(int x, int y, int width, int height, Size containingSize)
    {
        var clampedX = Math.Clamp(x, 0, Math.Max(0, containingSize.Width - 1));
        var clampedY = Math.Clamp(y, 0, Math.Max(0, containingSize.Height - 1));
        var clampedWidth = Math.Clamp(width, 1, containingSize.Width - clampedX);
        var clampedHeight = Math.Clamp(height, 1, containingSize.Height - clampedY);
        return new Rect(clampedX, clampedY, clampedWidth, clampedHeight);
    }

    private sealed class PopupDetectorOptions
    {
        public static double ButtonThreshold => 0.58;
        public static double IconThreshold => 0.58;
        public static double TitleThreshold => 0.50;
        public static double StrongThreshold => 0.78;
        public static double StrongTitleThreshold => 0.62;
        public static double TitleAnchorThreshold => 0.52;
        public static double MinimumTitleScoreGap => 0.005;
    }

    private readonly record struct TemplateMatchResult(double Score, Point Location);

    private enum TitleSignalKind
    {
        None,
        Ambiguous,
        SlowDown,
        MaximumSubmissions
    }

    private readonly record struct PopupScore(
        double ButtonOk,
        double IconInfo,
        double TitleSlowDown,
        double TitleMaxSubmissions,
        Rect PopupBounds);

    private sealed class PopupTemplates : IDisposable
    {
        private PopupTemplates(
            Mat buttonOkGray,
            Mat iconInfoGray,
            Mat titleSlowDownGray,
            Mat titleMaxSubmissionsGray,
            Mat titleSlowDownBinary,
            Mat titleMaxSubmissionsBinary)
        {
            ButtonOkGray = buttonOkGray;
            IconInfoGray = iconInfoGray;
            TitleSlowDownGray = titleSlowDownGray;
            TitleMaxSubmissionsGray = titleMaxSubmissionsGray;
            TitleSlowDownBinary = titleSlowDownBinary;
            TitleMaxSubmissionsBinary = titleMaxSubmissionsBinary;
        }

        public Mat ButtonOkGray { get; }
        public Mat IconInfoGray { get; }
        public Mat TitleSlowDownGray { get; }
        public Mat TitleMaxSubmissionsGray { get; }
        public Mat TitleSlowDownBinary { get; }
        public Mat TitleMaxSubmissionsBinary { get; }

        public static PopupTemplates Load()
        {
            return new PopupTemplates(
                LoadGrayTemplate("popups.button_ok.png"),
                LoadGrayTemplate("popups.icon_info.png"),
                LoadGrayTemplate("popups.title_slow_down.png"),
                LoadGrayTemplate("popups.title_max_submissions.png"),
                LoadBinaryTemplate("popups.title_slow_down.png"),
                LoadBinaryTemplate("popups.title_max_submissions.png"));
        }

        public void Dispose()
        {
            ButtonOkGray.Dispose();
            IconInfoGray.Dispose();
            TitleSlowDownGray.Dispose();
            TitleMaxSubmissionsGray.Dispose();
            TitleSlowDownBinary.Dispose();
            TitleMaxSubmissionsBinary.Dispose();
        }

        private static Mat LoadGrayTemplate(string resourceFile)
        {
            using var template = EmbeddedResourceLoader.LoadMat(resourceFile);
            if (template.Empty())
            {
                throw new InvalidOperationException("Popup template resource could not be decoded.");
            }

            var gray = new Mat();
            Cv2.CvtColor(template, gray, ColorConversionCodes.BGR2GRAY);
            return gray;
        }

        private static Mat LoadBinaryTemplate(string resourceFile)
        {
            using var gray = LoadGrayTemplate(resourceFile);
            return ToBinaryMask(gray);
        }
    }
}
