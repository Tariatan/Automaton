using Automaton.Infrastructure;
using OpenCvSharp;
using Serilog;

namespace Automaton.Detectors;

internal static class ConnectionLostPopupDetectionEngine
{
    private const int ExpectedPopupLeft = 960;
    private const int ExpectedPopupTop = 830;
    private const int ExpectedPopupWidth = 630;
    private const int ExpectedPopupHeight = 390;
    private const int PopupSearchMarginX = 190;
    private const int PopupSearchMarginY = 130;
    private const double ButtonThreshold = 0.55;
    private const double IconThreshold = 0.55;
    private const double TitleThreshold = 0.40;
    private const double WeakSignalThreshold = 0.45;
    private const double StrongTitleThreshold = 0.62;
    private const double TitleAnchorThreshold = 0.52;
    private static readonly ILogger Logger = Log.ForContext("SourceContext", "ConnectionLostPopupDetectionEngine");
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

        using var searchRegion = new Mat(image, searchBounds);
        using var searchGray = new Mat();
        Cv2.CvtColor(searchRegion, searchGray, ColorConversionCodes.BGR2GRAY);

        var score = ScorePopup(searchGray, searchBounds, popupBounds);
        Logger.Debug(
            "Connection lost scores => button_quit: {ButtonQuit:F4}, icon_warning: {IconWarning:F4}, title_connection_lost: {TitleConnectionLost:F4}",
            score.ButtonQuit,
            score.IconWarning,
            score.TitleConnectionLost);

        var state = Classify(score);
        if (state != PopupState.None)
        {
            return new PopupDetection(state, popupBounds);
        }

        if (!TryFindPopupBoundsByTitleAnchor(searchGray, searchBounds, image.Size(), out var anchoredPopupBounds))
        {
            return new PopupDetection(PopupState.None, popupBounds);
        }

        var anchoredScore = ScorePopup(searchGray, searchBounds, anchoredPopupBounds);
        var anchoredState = Classify(anchoredScore);
        return new PopupDetection(anchoredState, anchoredPopupBounds);
    }

    private static PopupState Classify(PopupScore score)
    {
        var popupExists = score.ButtonQuit >= ButtonThreshold &&
                          score.IconWarning >= IconThreshold;
        var titleLedPopupExists = score.TitleConnectionLost >= StrongTitleThreshold &&
                                  (score.ButtonQuit >= WeakSignalThreshold ||
                                   score.IconWarning >= WeakSignalThreshold);
        if (!popupExists && !titleLedPopupExists)
        {
            return PopupState.None;
        }

        return score.TitleConnectionLost >= TitleThreshold
            ? PopupState.ConnectionLost
            : PopupState.None;
    }

    private static PopupScore ScorePopup(Mat searchGray, Rect searchBounds, Rect popupBounds)
    {
        var localPopupBounds = new Rect(
            popupBounds.X - searchBounds.X,
            popupBounds.Y - searchBounds.Y,
            popupBounds.Width,
            popupBounds.Height);
        var iconBounds = BuildRelativeBounds(localPopupBounds, 0.02, 0.02, 0.20, 0.28);
        var buttonBounds = BuildRelativeBounds(localPopupBounds, 0.03, 0.74, 0.94, 0.20);
        var titleBounds = BuildRelativeBounds(localPopupBounds, 0.16, 0.02, 0.80, 0.34);

        using var iconRoi = new Mat(searchGray, iconBounds);
        using var buttonRoi = new Mat(searchGray, buttonBounds);
        using var titleRoi = new Mat(searchGray, titleBounds);
        using var titleRoiBinary = ToBinaryMask(titleRoi);

        var templates = SPopupTemplates.Value;
        return new PopupScore(
            ButtonQuit: MatchTemplateScore(buttonRoi, templates.ButtonQuitGray),
            IconWarning: MatchTemplateScore(iconRoi, templates.IconWarningGray),
            TitleConnectionLost: Math.Max(
                MatchTemplateScore(titleRoi, templates.TitleConnectionLostGray),
                MatchTemplateScore(titleRoiBinary, templates.TitleConnectionLostBinary)));
    }

    private static Mat ToBinaryMask(Mat gray)
    {
        var binary = new Mat();
        Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        return binary;
    }

    private static double MatchTemplateScore(Mat searchRoi, Mat template)
    {
        return MatchTemplate(searchRoi, template).Score;
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

    private static bool TryFindPopupBoundsByTitleAnchor(Mat searchGray, Rect searchBounds, Size imageSize, out Rect popupBounds)
    {
        using var searchBinary = ToBinaryMask(searchGray);

        var templates = SPopupTemplates.Value;
        var titleGrayMatch = MatchTemplate(searchGray, templates.TitleConnectionLostGray);
        var titleBinaryMatch = MatchTemplate(searchBinary, templates.TitleConnectionLostBinary);
        var best = titleGrayMatch.Score >= titleBinaryMatch.Score ? titleGrayMatch : titleBinaryMatch;
        if (best.Score < TitleAnchorThreshold)
        {
            popupBounds = default;
            return false;
        }

        var estimatedPopupX = searchBounds.X + best.Location.X - (int)Math.Round(ExpectedPopupWidth * 0.16);
        var estimatedPopupY = searchBounds.Y + best.Location.Y - (int)Math.Round(ExpectedPopupHeight * 0.02);
        popupBounds = BuildClampedBounds(
            estimatedPopupX,
            estimatedPopupY,
            ExpectedPopupWidth,
            ExpectedPopupHeight,
            imageSize);
        return true;
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

    private readonly record struct TemplateMatchResult(double Score, Point Location);

    private readonly record struct PopupScore(
        double ButtonQuit,
        double IconWarning,
        double TitleConnectionLost);

    private sealed class PopupTemplates : IDisposable
    {
        private PopupTemplates(
            Mat buttonQuitGray,
            Mat iconWarningGray,
            Mat titleConnectionLostGray,
            Mat titleConnectionLostBinary)
        {
            ButtonQuitGray = buttonQuitGray;
            IconWarningGray = iconWarningGray;
            TitleConnectionLostGray = titleConnectionLostGray;
            TitleConnectionLostBinary = titleConnectionLostBinary;
        }

        public Mat ButtonQuitGray { get; }
        public Mat IconWarningGray { get; }
        public Mat TitleConnectionLostGray { get; }
        public Mat TitleConnectionLostBinary { get; }

        public static PopupTemplates Load()
        {
            return new PopupTemplates(
                LoadGrayTemplate("popups.button_quit.png"),
                LoadGrayTemplate("popups.icon_warning.png"),
                LoadGrayTemplate("popups.title_connection_lost.png"),
                LoadBinaryTemplate("popups.title_connection_lost.png"));
        }

        public void Dispose()
        {
            ButtonQuitGray.Dispose();
            IconWarningGray.Dispose();
            TitleConnectionLostGray.Dispose();
            TitleConnectionLostBinary.Dispose();
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
