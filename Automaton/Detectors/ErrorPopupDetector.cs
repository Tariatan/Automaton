using Automaton.Infrastructure;
using OpenCvSharp;
using Serilog;

namespace Automaton.Detectors;

internal sealed class ErrorPopupDetector
{
    internal readonly record struct PopupDetection(PopupState State, Rect Bounds);

    internal enum PopupState
    {
        None,
        MaximumSubmissions,
        SlowDown,
        ConnectionLost,
        Unknown
    }

    private const string MaxSubmissionsOverlayText = "Maximum submissions popup detected";
    private const string SlowDownDebugOverlayText = "Slow down popup detected";
    private const string ConnectionLostDebugOverlayText = "Connection lost popup detected";
    private const string AmbiguousPopupDebugOverlayText = "Popup detected but ambiguous";
    private const int ExpectedPopupLeft = 960;
    private const int ExpectedPopupTop = 830;
    private const int ExpectedPopupWidth = 630;
    private const int ExpectedPopupHeight = 390;
    private const int PopupSearchMarginX = 190;
    private const int PopupSearchMarginY = 130;
    private const double DebugOverlayTextScale = 0.8;
    private const int DebugOverlayTextThickness = 2;
    private const int DebugOverlayLeftPadding = 30;
    private const int DebugOverlayTopPadding = 40;
    private static readonly Scalar DebugOverlayTextColor = new(80, 120, 255);
    private static readonly ILogger Logger = Log.ForContext<ErrorPopupDetector>();
    private static readonly PopupDetectorOptions SOptions = new();
    private static readonly Lazy<PopupTemplates> SPopupTemplates = new(PopupTemplates.Load);

    public PopupState DetectPopupStateAndDrawDebugOverlay(string imagePath)
    {
        using var image = Cv2.ImRead(imagePath);
        var detection = DetectPopup(image);
        var overlayText = detection.State switch
        {
            PopupState.MaximumSubmissions => MaxSubmissionsOverlayText,
            PopupState.SlowDown => SlowDownDebugOverlayText,
            PopupState.ConnectionLost => ConnectionLostDebugOverlayText,
            PopupState.Unknown => AmbiguousPopupDebugOverlayText,
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(overlayText))
        {
            Cv2.Rectangle(image, detection.Bounds, DebugOverlayTextColor, 2);
            DrawDebugOverlay(image, overlayText);
            Cv2.ImWrite(imagePath, image);
        }

        return detection.State;
    }

    public static PopupState DetectPopupState(Mat image)
    {
        return DetectPopup(image).State;
    }

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
        LogScores(score);

        var state = Classify(score);
        return new PopupDetection(state, popupBounds);
    }

    private static PopupState Classify(PopupScore score)
    {
        var bestButton = Math.Max(score.ButtonOk, score.ButtonQuit);
        var bestIcon = Math.Max(score.IconInfo, score.IconWarning);
        var popupExists = (bestButton >= SOptions.ButtonThreshold &&
                           bestIcon >= SOptions.IconThreshold) ||
                          bestButton >= SOptions.StrongThreshold ||
                          bestIcon >= SOptions.StrongThreshold;
        if (!popupExists)
        {
            return PopupState.None;
        }

        var buttonKind = ResolveBinarySignal(
            score.ButtonOk,
            score.ButtonQuit,
            SOptions.ButtonThreshold,
            SOptions.MinimumSignalGap,
            BinarySignalKind.Ok,
            BinarySignalKind.Quit);
        var iconKind = ResolveBinarySignal(
            score.IconInfo,
            score.IconWarning,
            SOptions.IconThreshold,
            SOptions.MinimumSignalGap,
            BinarySignalKind.Info,
            BinarySignalKind.Warning);
        var titleKind = ResolveTitleSignal(score);

        var prefersConnection = buttonKind == BinarySignalKind.Quit ||
                                iconKind == BinarySignalKind.Warning;
        var prefersOk = buttonKind == BinarySignalKind.Ok ||
                        iconKind == BinarySignalKind.Info;
        if (prefersConnection && prefersOk)
        {
            var okAggregate = score.ButtonOk + score.IconInfo + Math.Max(score.TitleSlowDown, score.TitleMaxSubmissions);
            var connectionAggregate = score.ButtonQuit + score.IconWarning + score.TitleConnectionLost;
            if (Math.Abs(okAggregate - connectionAggregate) < SOptions.MinimumAggregateGap)
            {
                return PopupState.Unknown;
            }

            prefersConnection = connectionAggregate > okAggregate;
            prefersOk = okAggregate > connectionAggregate;
        }

        if (!prefersConnection && !prefersOk)
        {
            var okAggregate = score.ButtonOk + score.IconInfo + Math.Max(score.TitleSlowDown, score.TitleMaxSubmissions);
            var connectionAggregate = score.ButtonQuit + score.IconWarning + score.TitleConnectionLost;
            if (Math.Abs(okAggregate - connectionAggregate) < SOptions.MinimumAggregateGap)
            {
                return PopupState.Unknown;
            }

            prefersConnection = connectionAggregate > okAggregate;
            prefersOk = okAggregate > connectionAggregate;
        }

        if (prefersConnection)
        {
            return titleKind switch
            {
                TitleSignalKind.ConnectionLost => PopupState.ConnectionLost,
                TitleSignalKind.None => PopupState.ConnectionLost,
                _ => PopupState.Unknown
            };
        }

        if (!prefersOk)
        {
            return PopupState.Unknown;
        }

        var titleResult = titleKind switch
        {
            TitleSignalKind.SlowDown => PopupState.SlowDown,
            TitleSignalKind.MaximumSubmissions => PopupState.MaximumSubmissions,
            _ => PopupState.Unknown
        };

        if (titleResult != PopupState.Unknown)
        {
            return titleResult;
        }

        var bestOkTitle = Math.Max(score.TitleSlowDown, score.TitleMaxSubmissions);
        if (bestOkTitle >= SOptions.TitleThreshold * 0.85)
        {
            return score.TitleSlowDown >= score.TitleMaxSubmissions
                ? PopupState.SlowDown
                : PopupState.MaximumSubmissions;
        }

        return PopupState.Unknown;
    }

    private static BinarySignalKind ResolveBinarySignal(
        double primaryScore,
        double secondaryScore,
        double threshold,
        double minimumGap,
        BinarySignalKind primaryKind,
        BinarySignalKind secondaryKind)
    {
        var primaryMatched = primaryScore >= threshold;
        var secondaryMatched = secondaryScore >= threshold;
        switch (primaryMatched)
        {
            case false when !secondaryMatched:
                return BinarySignalKind.None;
            case true when !secondaryMatched:
                return primaryKind;
            case false when secondaryMatched:
                return secondaryKind;
        }

        if (Math.Abs(primaryScore - secondaryScore) < minimumGap)
        {
            return BinarySignalKind.Ambiguous;
        }

        return primaryScore > secondaryScore ? primaryKind : secondaryKind;
    }

    private static TitleSignalKind ResolveTitleSignal(PopupScore score)
    {
        var candidates = new[]
        {
            (Kind: TitleSignalKind.ConnectionLost, Score: score.TitleConnectionLost),
            (Kind: TitleSignalKind.SlowDown, Score: score.TitleSlowDown),
            (Kind: TitleSignalKind.MaximumSubmissions, Score: score.TitleMaxSubmissions)
        };
        var best = candidates.MaxBy(candidate => candidate.Score);
        var second = candidates.OrderByDescending(candidate => candidate.Score).Skip(1).First();
        if (best.Score < SOptions.TitleThreshold)
        {
            return TitleSignalKind.None;
        }

        return (best.Score - second.Score) < SOptions.MinimumTitleScoreGap
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

        var score = new PopupScore(
            ButtonOk: MatchTemplateScore(buttonRoi, templates.ButtonOkGray),
            ButtonQuit: MatchTemplateScore(buttonRoi, templates.ButtonQuitGray),
            IconInfo: MatchTemplateScore(iconRoi, templates.IconInfoGray),
            IconWarning: MatchTemplateScore(iconRoi, templates.IconWarningGray),
            TitleConnectionLost: Math.Max(
                MatchTemplateScore(titleRoi, templates.TitleConnectionLostGray),
                MatchTemplateScore(titleRoiBinary, templates.TitleConnectionLostBinary)),
            TitleSlowDown: Math.Max(
                MatchTemplateScore(titleRoi, templates.TitleSlowDownGray),
                MatchTemplateScore(titleRoiBinary, templates.TitleSlowDownBinary)),
            TitleMaxSubmissions: Math.Max(
                MatchTemplateScore(titleRoi, templates.TitleMaxSubmissionsGray),
                MatchTemplateScore(titleRoiBinary, templates.TitleMaxSubmissionsBinary)),
            PopupBounds: popupBounds);
        return score;
    }

    private static void LogScores(PopupScore score)
    {
        Logger.Debug(
            "Popup scores => button_ok: {ButtonOk:F4}, button_quit: {ButtonQuit:F4}, icon_info: {IconInfo:F4}, icon_warning: {IconWarning:F4}, title_connection_lost: {TitleConnectionLost:F4}, title_slow_down: {TitleSlowDown:F4}, title_max_submissions: {TitleMaxSubmissions:F4}, popup_bounds: {PopupBounds}",
            score.ButtonOk,
            score.ButtonQuit,
            score.IconInfo,
            score.IconWarning,
            score.TitleConnectionLost,
            score.TitleSlowDown,
            score.TitleMaxSubmissions,
            score.PopupBounds);
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

    private static Rect BuildRelativeBounds(
        Rect bounds,
        double leftRatio,
        double topRatio,
        double widthRatio,
        double heightRatio)
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

    private static void DrawDebugOverlay(Mat image, string text)
    {
        Cv2.PutText(
            image,
            text,
            new Point(DebugOverlayLeftPadding, DebugOverlayTopPadding),
            HersheyFonts.HersheySimplex,
            DebugOverlayTextScale,
            DebugOverlayTextColor,
            DebugOverlayTextThickness,
            LineTypes.AntiAlias);
    }

    private sealed class PopupDetectorOptions
    {
        public double ButtonThreshold { get; init; } = 0.58;
        public double IconThreshold { get; init; } = 0.58;
        public double TitleThreshold { get; init; } = 0.50;
        public double StrongThreshold { get; init; } = 0.78;
        public double MinimumTitleScoreGap { get; init; } = 0.005;
        public double MinimumSignalGap { get; init; } = 0.02;
        public double MinimumAggregateGap { get; init; } = 0.08;
    }

    private enum BinarySignalKind
    {
        None,
        Ambiguous,
        Ok,
        Quit,
        Info,
        Warning
    }

    private enum TitleSignalKind
    {
        None,
        Ambiguous,
        ConnectionLost,
        SlowDown,
        MaximumSubmissions
    }

    private readonly record struct PopupScore(
        double ButtonOk,
        double ButtonQuit,
        double IconInfo,
        double IconWarning,
        double TitleConnectionLost,
        double TitleSlowDown,
        double TitleMaxSubmissions,
        Rect PopupBounds);

    private sealed class PopupTemplates : IDisposable
    {
        private PopupTemplates(
            Mat buttonOkGray,
            Mat buttonQuitGray,
            Mat iconInfoGray,
            Mat iconWarningGray,
            Mat titleConnectionLostGray,
            Mat titleSlowDownGray,
            Mat titleMaxSubmissionsGray,
            Mat titleConnectionLostBinary,
            Mat titleSlowDownBinary,
            Mat titleMaxSubmissionsBinary)
        {
            ButtonOkGray = buttonOkGray;
            ButtonQuitGray = buttonQuitGray;
            IconInfoGray = iconInfoGray;
            IconWarningGray = iconWarningGray;
            TitleConnectionLostGray = titleConnectionLostGray;
            TitleSlowDownGray = titleSlowDownGray;
            TitleMaxSubmissionsGray = titleMaxSubmissionsGray;
            TitleConnectionLostBinary = titleConnectionLostBinary;
            TitleSlowDownBinary = titleSlowDownBinary;
            TitleMaxSubmissionsBinary = titleMaxSubmissionsBinary;
        }

        public Mat ButtonOkGray { get; }
        public Mat ButtonQuitGray { get; }
        public Mat IconInfoGray { get; }
        public Mat IconWarningGray { get; }
        public Mat TitleConnectionLostGray { get; }
        public Mat TitleSlowDownGray { get; }
        public Mat TitleMaxSubmissionsGray { get; }
        public Mat TitleConnectionLostBinary { get; }
        public Mat TitleSlowDownBinary { get; }
        public Mat TitleMaxSubmissionsBinary { get; }

        public static PopupTemplates Load()
        {
            return new PopupTemplates(
                LoadGrayTemplate("popups.button_ok.png"),
                LoadGrayTemplate("popups.button_quit.png"),
                LoadGrayTemplate("popups.icon_info.png"),
                LoadGrayTemplate("popups.icon_warning.png"),
                LoadGrayTemplate("popups.title_connection_lost.png"),
                LoadGrayTemplate("popups.title_slow_down.png"),
                LoadGrayTemplate("popups.title_max_submissions.png"),
                LoadBinaryTemplate("popups.title_connection_lost.png"),
                LoadBinaryTemplate("popups.title_slow_down.png"),
                LoadBinaryTemplate("popups.title_max_submissions.png"));
        }

        public void Dispose()
        {
            ButtonOkGray.Dispose();
            ButtonQuitGray.Dispose();
            IconInfoGray.Dispose();
            IconWarningGray.Dispose();
            TitleConnectionLostGray.Dispose();
            TitleSlowDownGray.Dispose();
            TitleMaxSubmissionsGray.Dispose();
            TitleConnectionLostBinary.Dispose();
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
