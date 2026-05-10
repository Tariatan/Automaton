using OpenCvSharp;
using Automaton.Properties;
using System.IO;
using System.Drawing.Imaging;

namespace Automaton;

internal sealed class ErrorPopupDetector
{
    internal readonly record struct PopupDetection(PopupState State, Rect Bounds, bool FromTemplate);
    internal enum PopupState
    {
        None,
        MaximumSubmissions,
        SlowDown,
        ConnectionLost,
        Unknown
    }

    private const double SearchLeftRatio = 0.56;
    private const double SearchTopRatio = 0.62;
    private const double SearchRightRatio = 0.92;
    private const double SearchBottomRatio = 0.91;
    private const double PopupHeightToWidthRatio = 0.64;
    private const double PopupCandidateWidthGrowth = 1.15;
    private const int TitleWhiteMinimum = 2_500;
    private const int BodyWhiteMinimum = 2_200;
    private const int IconWhiteMinimum = 600;
    private const int ButtonCyanMinimum = 700;
    private const int ButtonWhiteMinimum = 50;
    private const int ButtonWhiteMaximum = 800;
    private const int MinimumBodyTextBands = 3;
    private const int BodyTextBandRowWhiteMinimum = 8;
    private const int MinimumBodyTextBandHeight = 3;
    private const int TitleTextBandRowWhiteMinimum = 40;
    private const int MinimumTitleTextBandHeight = 12;
    private const int TitleLineWhiteMinimum = 700;
    private const int SlowDownTitleWhiteMinimum = 900;
    private const int SlowDownTitleWhiteMaximum = 2_400;
    private const int SlowDownTitleSecondHalfWhiteMaximum = 1_600;
    private const double MinimumInformationIconContourArea = 450.0;
    private const double MaximumInformationIconContourArea = 8_000.0;
    private const int MinimumInformationIconContourWidth = 32;
    private const int MinimumInformationIconContourHeight = 32;
    private const int MinimumInformationIconContourMargin = 2;
    private const double MinimumInformationIconAspectRatio = 0.75;
    private const double MaximumInformationIconAspectRatio = 1.30;
    private const double MaximumFilledSquareIconFillRatio = 0.88;
    private const double MaximumBodyWhiteContourArea = 1_200.0;
    private const double TitleWhiteMaximumDensity = 0.22;
    private const double BodyWhiteMaximumDensity = 0.16;
    private const double IconWhiteMaximumDensity = 0.25;
    private const int MinimumSearchDimension = 1;
    private const int BinaryMaskMaxValue = 255;
    private const int MinimumPopupCandidateWidth = 480;
    private const int MaximumPopupCandidateWidth = 1_600;
    private const int MinimumPopupCandidateStep = 32;
    private const string MaxSubmissionsOverlayText = "Maximum submissions popup detected";
    private const string SlowDownDebugOverlayText = "Slow down popup detected";
    private const string ConnectionLostDebugOverlayText = "Connection lost popup detected";
    private const string AmbiguousPopupDebugOverlayText = "Popup detected but ambiguous";
    private const int ConnectionLostTitleWhiteMinimum = 700;
    private const int ConnectionLostTitleWhiteMaximum = 3_300;
    private const int ConnectionLostTitleSecondHalfWhiteMaximum = 1_400;
    private const int ConnectionLostYellowMinimum = 10;
    private const int ConnectionLostYellowMaximum = 1_400;
    private const double DebugOverlayTextScale = 0.8;
    private const int DebugOverlayTextThickness = 2;
    private const int DebugOverlayLeftPadding = 30;
    private const int DebugOverlayTopPadding = 40;
    private static readonly Scalar WhiteMinimum = new(180, 180, 180);
    private static readonly Scalar WhiteMaximum = new(255, 255, 255);
    private static readonly Scalar IconMinimum = new(135, 135, 135);
    private static readonly Scalar CyanMinimum = new(80, 40, 45);
    private static readonly Scalar CyanMaximum = new(105, 255, 230);
    private static readonly Scalar YellowMinimum = new(20, 90, 150);
    private static readonly Scalar YellowMaximum = new(35, 255, 255);
    private static readonly Scalar DebugOverlayTextColor = new(80, 120, 255);
    private static readonly Lazy<PopupTemplates> s_PopupTemplates = new(PopupTemplates.Load);

    public bool Detect(string imagePath)
    {
        using var image = Cv2.ImRead(imagePath);
        return Detect(image);
    }

    public bool DetectAndDrawDebugOverlay(string imagePath)
    {
        using var image = Cv2.ImRead(imagePath);
        if (!Detect(image, PopupTitleKind.MaximumSubmissions))
        {
            return false;
        }

        DrawDebugOverlay(image, MaxSubmissionsOverlayText);
        Cv2.ImWrite(imagePath, image);
        return true;
    }

    public bool Detect(Mat image)
    {
        return Detect(image, PopupTitleKind.MaximumSubmissions);
    }

    public bool DetectSlowDownAndDrawDebugOverlay(string imagePath)
    {
        using var image = Cv2.ImRead(imagePath);
        if (!Detect(image, PopupTitleKind.SlowDown))
        {
            return false;
        }

        DrawDebugOverlay(image, SlowDownDebugOverlayText);
        Cv2.ImWrite(imagePath, image);
        return true;
    }

    public bool DetectSlowDown(Mat image)
    {
        return Detect(image, PopupTitleKind.SlowDown);
    }

    public bool DetectConnectionLostAndDrawDebugOverlay(string imagePath)
    {
        using var image = Cv2.ImRead(imagePath);
        if (!DetectConnectionLostPopup(image))
        {
            return false;
        }

        DrawDebugOverlay(image, ConnectionLostDebugOverlayText);
        Cv2.ImWrite(imagePath, image);
        return true;
    }

    public bool DetectConnectionLost(Mat image)
    {
        return DetectConnectionLostPopup(image);
    }

    public PopupState DetectPopupStateAndDrawDebugOverlay(string imagePath)
    {
        using var image = Cv2.ImRead(imagePath);
        var popupState = DetectPopupState(image);
        var overlayText = popupState switch
        {
            PopupState.MaximumSubmissions => MaxSubmissionsOverlayText,
            PopupState.SlowDown => SlowDownDebugOverlayText,
            PopupState.ConnectionLost => ConnectionLostDebugOverlayText,
            PopupState.Unknown => AmbiguousPopupDebugOverlayText,
            _ => null
        };
        if (!string.IsNullOrWhiteSpace(overlayText))
        {
            DrawDebugOverlay(image, overlayText);
            Cv2.ImWrite(imagePath, image);
        }

        return popupState;
    }

    public PopupState DetectPopupState(Mat image)
    {
        return DetectPopup(image).State;
    }

    public PopupDetection DetectPopup(Mat image)
    {
        if (TryDetectPopupStateByTemplate(image, out var templateState, out var templateBounds))
        {
            return new PopupDetection(templateState, templateBounds, true);
        }

        if (DetectConnectionLostPopup(image))
        {
            return new PopupDetection(PopupState.ConnectionLost, new Rect(), false);
        }

        if (image.Empty())
        {
            return new PopupDetection(PopupState.None, new Rect(), false);
        }

        using var masks = PopupEvidenceMasks.Create(image);
        var popupKind = DetectPopupKind(masks, image.Size());
        if (popupKind is null)
        {
            return new PopupDetection(PopupState.None, new Rect(), false);
        }

        var maxCandidate = FindBestPopupCandidate(masks, image.Size(), PopupTitleKind.MaximumSubmissions);
        var slowCandidate = FindBestPopupCandidate(masks, image.Size(), PopupTitleKind.SlowDown);
        if (maxCandidate is not null && slowCandidate is not null)
        {
            var scoreDelta = Math.Abs(maxCandidate.Value.Score - slowCandidate.Value.Score);
            if (scoreDelta < 700)
            {
                return new PopupDetection(PopupState.Unknown, new Rect(), false);
            }
        }

        var state = popupKind switch
        {
            PopupTitleKind.MaximumSubmissions => PopupState.MaximumSubmissions,
            PopupTitleKind.SlowDown => PopupState.SlowDown,
            PopupTitleKind.ConnectionLost => PopupState.ConnectionLost,
            _ => PopupState.None
        };
        return new PopupDetection(state, new Rect(), false);
    }

    private static bool TryDetectPopupStateByTemplate(Mat image, out PopupState popupState, out Rect popupBounds)
    {
        popupState = PopupState.None;
        popupBounds = new Rect();
        if (image.Empty())
        {
            return false;
        }

        using var gray = new Mat();
        Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        var searchBounds = BuildClampedBounds(
            (int)Math.Round(image.Width * 0.20),
            (int)Math.Round(image.Height * 0.35),
            (int)Math.Round(image.Width * 0.60),
            (int)Math.Round(image.Height * 0.55),
            image.Size());
        using var searchRegion = new Mat(gray, searchBounds);

        var matches = new List<PopupTemplateMatch>
        {
            GetBestTemplateMatch(searchRegion, s_PopupTemplates.Value.ConnectionLostGray, PopupState.ConnectionLost, 0.56),
            GetBestTemplateMatch(searchRegion, s_PopupTemplates.Value.MaximumSubmissionsGray, PopupState.MaximumSubmissions, 0.63),
            GetBestTemplateMatch(searchRegion, s_PopupTemplates.Value.SlowDownGray, PopupState.SlowDown, 0.63)
        };
        var best = matches.OrderByDescending(match => match.Score).First();
        if (best.Score < best.Threshold)
        {
            return false;
        }

        if (!HasTemplateContentEvidence(searchRegion, best))
        {
            return false;
        }

        popupBounds = new Rect(
            searchBounds.X + best.Bounds.X,
            searchBounds.Y + best.Bounds.Y,
            best.Bounds.Width,
            best.Bounds.Height);

        var secondBest = matches.OrderByDescending(match => match.Score).Skip(1).First();
        if (best.Score - secondBest.Score < 0.02)
        {
            popupState = PopupState.Unknown;
            return true;
        }

        popupState = best.PopupState;
        return true;
    }

    private static PopupTemplateMatch GetBestTemplateMatch(Mat searchRegionGray, Mat templateGray, PopupState popupState, double threshold)
    {
        var bestScore = 0.0;
        var bestBounds = new Rect(0, 0, 0, 0);
        var bestScale = 1.0;
        foreach (var scale in new[] { 0.92, 1.0, 1.08 })
        {
            using var scaledTemplate = ResizeTemplate(templateGray, scale);
            if (scaledTemplate.Width > searchRegionGray.Width || scaledTemplate.Height > searchRegionGray.Height)
            {
                continue;
            }

            var resultWidth = searchRegionGray.Width - scaledTemplate.Width + 1;
            var resultHeight = searchRegionGray.Height - scaledTemplate.Height + 1;
            using var result = new Mat(new Size(resultWidth, resultHeight), MatType.CV_32FC1);
            Cv2.MatchTemplate(searchRegionGray, scaledTemplate, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out var maxValue, out _, out var maxPoint);
            if (maxValue > bestScore)
            {
                bestScore = maxValue;
                bestBounds = new Rect(maxPoint.X, maxPoint.Y, scaledTemplate.Width, scaledTemplate.Height);
                bestScale = scale;
            }
        }

        return new PopupTemplateMatch(popupState, bestScore, threshold, bestBounds, bestScale);
    }

    private static Mat ResizeTemplate(Mat templateGray, double scale)
    {
        if (Math.Abs(scale - 1.0) < 0.001)
        {
            return templateGray.Clone();
        }

        var resized = new Mat();
        Cv2.Resize(
            templateGray,
            resized,
            new Size(),
            scale,
            scale,
            InterpolationFlags.Linear);
        return resized;
    }

    private static bool HasTemplateContentEvidence(Mat searchRegionGray, PopupTemplateMatch match)
    {
        if (match.Bounds.Width <= 0 || match.Bounds.Height <= 0)
        {
            return false;
        }

        var contentBands = new[] { BuildRelativeRect(match.Bounds, 0.18, 0.03, 0.70, 0.22), BuildRelativeRect(match.Bounds, 0.05, 0.32, 0.90, 0.34), BuildRelativeRect(match.Bounds, 0.05, 0.78, 0.90, 0.18) };
        return contentBands.All(contentBand =>
        {
            if (contentBand.Width <= 4 || contentBand.Height <= 4)
            {
                return false;
            }

            using var content = new Mat(searchRegionGray, contentBand);
            Cv2.MeanStdDev(content, out _, out var stdDev);
            var contrast = stdDev.Val0;
            return contrast >= 8.0;
        });
    }

    private static Rect BuildRelativeRect(Rect bounds, double leftRatio, double topRatio, double widthRatio, double heightRatio)
    {
        return new Rect(
            bounds.X + (int)Math.Round(bounds.Width * leftRatio),
            bounds.Y + (int)Math.Round(bounds.Height * topRatio),
            Math.Max(1, (int)Math.Round(bounds.Width * widthRatio)),
            Math.Max(1, (int)Math.Round(bounds.Height * heightRatio)));
    }

    private static bool Detect(Mat image, PopupTitleKind titleKind)
    {
        if (image.Empty())
        {
            return false;
        }

        using var masks = PopupEvidenceMasks.Create(image);
        return FindBestPopupCandidate(masks, image.Size(), titleKind) is not null;
    }

    private static bool DetectConnectionLostPopup(Mat image)
    {
        if (image.Empty())
        {
            return false;
        }

        using var hsv = new Mat();
        using var yellowMask = new Mat();
        using var whiteMask = new Mat();
        Cv2.CvtColor(image, hsv, ColorConversionCodes.BGR2HSV);
        Cv2.InRange(hsv, YellowMinimum, YellowMaximum, yellowMask);
        Cv2.InRange(image, WhiteMinimum, WhiteMaximum, whiteMask);

        Cv2.FindContours(yellowMask.Clone(), out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area < 8.0)
            {
                continue;
            }

            var bounds = Cv2.BoundingRect(contour);
            var centerX = bounds.X + (bounds.Width / 2);
            var centerY = bounds.Y + (bounds.Height / 2);
            if (centerX < image.Width * 0.30 || centerX > image.Width * 0.75 ||
                centerY < image.Height * 0.45 || centerY > image.Height * 0.88)
            {
                continue;
            }

            var contextBounds = BuildClampedBounds(
                bounds.X - 520,
                bounds.Y - 40,
                560,
                100,
                image.Size());
            var whitePixels = Cv2.CountNonZero(new Mat(whiteMask, contextBounds));
            if (whitePixels >= 650)
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<Rect> BuildCandidateBounds(Size imageSize)
    {
        yield return BuildLegacySearchBounds(imageSize);

        foreach (var candidateSize in BuildCandidateSizes(imageSize))
        {
            var step = Math.Max(MinimumPopupCandidateStep, candidateSize.Width / 10);
            for (var top = 0; top <= imageSize.Height - candidateSize.Height; top += step)
            {
                for (var left = 0; left <= imageSize.Width - candidateSize.Width; left += step)
                {
                    yield return new Rect(left, top, candidateSize.Width, candidateSize.Height);
                }
            }
        }
    }

    private static IEnumerable<Size> BuildCandidateSizes(Size imageSize)
    {
        if (imageSize.Width < MinimumPopupCandidateWidth ||
            imageSize.Height < (int)Math.Round(MinimumPopupCandidateWidth * PopupHeightToWidthRatio))
        {
            yield break;
        }

        var maximumWidth = Math.Min(MaximumPopupCandidateWidth, imageSize.Width);
        for (var width = MinimumPopupCandidateWidth; width <= maximumWidth; width = Math.Max(width + 1, (int)Math.Round(width * PopupCandidateWidthGrowth)))
        {
            var height = (int)Math.Round(width * PopupHeightToWidthRatio);
            if (height <= imageSize.Height)
            {
                yield return new Size(width, height);
            }
        }
    }

    private static Rect BuildLegacySearchBounds(Size imageSize)
    {
        var left = (int)Math.Round(imageSize.Width * SearchLeftRatio);
        var top = (int)Math.Round(imageSize.Height * SearchTopRatio);
        var right = (int)Math.Round(imageSize.Width * SearchRightRatio);
        var bottom = (int)Math.Round(imageSize.Height * SearchBottomRatio);
        return BuildClampedBounds(left, top, right - left, bottom - top, imageSize);
    }

    private static PopupTitleKind? DetectPopupKind(PopupEvidenceMasks masks, Size imageSize)
    {
        var maximumSubmissionsCandidate = FindBestPopupCandidate(masks, imageSize, PopupTitleKind.MaximumSubmissions);
        var slowDownCandidate = FindBestPopupCandidate(masks, imageSize, PopupTitleKind.SlowDown);
        var connectionLostCandidate = FindBestPopupCandidate(masks, imageSize, PopupTitleKind.ConnectionLost);
        if (maximumSubmissionsCandidate is null)
        {
            if (slowDownCandidate is null)
            {
                return connectionLostCandidate is null ? null : PopupTitleKind.ConnectionLost;
            }

            if (connectionLostCandidate is null)
            {
                return PopupTitleKind.SlowDown;
            }

            return connectionLostCandidate.Value.Score > slowDownCandidate.Value.Score
                ? PopupTitleKind.ConnectionLost
                : PopupTitleKind.SlowDown;
        }

        if (slowDownCandidate is null)
        {
            if (connectionLostCandidate is null)
            {
                return PopupTitleKind.MaximumSubmissions;
            }

            return connectionLostCandidate.Value.Score > maximumSubmissionsCandidate.Value.Score
                ? PopupTitleKind.ConnectionLost
                : PopupTitleKind.MaximumSubmissions;
        }

        if (connectionLostCandidate is null)
        {
            return slowDownCandidate.Value.Score > maximumSubmissionsCandidate.Value.Score
                ? PopupTitleKind.SlowDown
                : PopupTitleKind.MaximumSubmissions;
        }

        if (connectionLostCandidate.Value.Score > slowDownCandidate.Value.Score &&
            connectionLostCandidate.Value.Score > maximumSubmissionsCandidate.Value.Score)
        {
            return PopupTitleKind.ConnectionLost;
        }

        return slowDownCandidate.Value.Score > maximumSubmissionsCandidate.Value.Score
            ? PopupTitleKind.SlowDown
            : PopupTitleKind.MaximumSubmissions;
    }

    private static PopupCandidateEvidence? FindBestPopupCandidate(
        PopupEvidenceMasks masks,
        Size imageSize,
        PopupTitleKind titleKind)
    {
        PopupCandidateEvidence? bestCandidate = null;
        foreach (var candidate in BuildCandidateBounds(imageSize))
        {
            if (!TryBuildPopupCandidateEvidence(masks, candidate, titleKind, out var evidence) ||
                !HasTitleEvidence(masks, BuildTitleBounds(candidate), titleKind))
            {
                continue;
            }

            if (bestCandidate is null || evidence.Score > bestCandidate.Value.Score)
            {
                bestCandidate = evidence;
            }
        }

        return bestCandidate;
    }

    private static bool TryBuildPopupCandidateEvidence(
        PopupEvidenceMasks masks,
        Rect candidate,
        PopupTitleKind titleKind,
        out PopupCandidateEvidence evidence)
    {
        evidence = default;
        var buttonBounds = BuildButtonBounds(candidate);
        var buttonWhitePixels = masks.CountWhitePixels(buttonBounds);
        var buttonCyanPixels = masks.CountCyanPixels(buttonBounds);
        var buttonEvidenceFound = buttonCyanPixels >= ButtonCyanMinimum ||
                                  buttonWhitePixels is >= ButtonWhiteMinimum and <= ButtonWhiteMaximum;
        var titleBounds = BuildTitleBounds(candidate);
        var bodyBounds = BuildBodyBounds(candidate);
        var iconBounds = BuildIconBounds(candidate);
        var bodyWhitePixels = masks.CountWhitePixels(bodyBounds);
        var bodyTextBands = masks.CountWhiteTextBands(bodyBounds);
        var minimumBodyWhitePixels = titleKind == PopupTitleKind.ConnectionLost ? 1_200 : BodyWhiteMinimum;
        var minimumBodyTextBands = titleKind == PopupTitleKind.ConnectionLost ? 2 : MinimumBodyTextBands;
        if (!HasWhitePixelEvidence(bodyWhitePixels, bodyBounds, minimumBodyWhitePixels, BodyWhiteMaximumDensity) ||
            bodyTextBands < minimumBodyTextBands ||
            !HasInformationIconEvidence(masks, iconBounds) ||
            masks.GetLargestWhiteContourArea(bodyBounds) > MaximumBodyWhiteContourArea ||
            !buttonEvidenceFound)
        {
            return false;
        }

        if (titleKind == PopupTitleKind.ConnectionLost &&
            !HasConnectionLostBodyEvidence(masks, candidate))
        {
            return false;
        }

        var titleWhitePixels = masks.CountWhitePixels(titleBounds);
        var iconWhitePixels = masks.CountIconPixels(iconBounds);
        var score = titleWhitePixels +
                    bodyWhitePixels +
                    buttonCyanPixels +
                    buttonWhitePixels +
                    iconWhitePixels +
                    (bodyTextBands * 500);
        evidence = new PopupCandidateEvidence(candidate, score);
        return true;
    }

    private static bool HasTitleEvidence(PopupEvidenceMasks masks, Rect titleBounds, PopupTitleKind titleKind)
    {
        return titleKind switch
        {
            PopupTitleKind.MaximumSubmissions => HasMaximumSubmissionsTitleEvidence(masks, titleBounds),
            PopupTitleKind.SlowDown => HasSlowDownTitleEvidence(masks, titleBounds),
            PopupTitleKind.ConnectionLost => HasConnectionLostTitleEvidence(masks, titleBounds),
            _ => false
        };
    }

    private static bool HasMaximumSubmissionsTitleEvidence(PopupEvidenceMasks masks, Rect titleBounds)
    {
        var firstLineBounds = BuildClampedBounds(
            titleBounds.X,
            titleBounds.Y,
            titleBounds.Width,
            titleBounds.Height / 2,
            new Size(titleBounds.Right, titleBounds.Bottom));
        var secondLineBounds = BuildClampedBounds(
            titleBounds.X,
            titleBounds.Y + titleBounds.Height / 2,
            titleBounds.Width,
            titleBounds.Height - titleBounds.Height / 2,
            new Size(titleBounds.Right, titleBounds.Bottom));

        return HasWhitePixelEvidence(masks, titleBounds, TitleWhiteMinimum, TitleWhiteMaximumDensity) &&
               masks.CountWhiteTextBands(titleBounds, TitleTextBandRowWhiteMinimum, MinimumTitleTextBandHeight) == 2 &&
               masks.CountWhitePixels(firstLineBounds) >= TitleLineWhiteMinimum &&
               masks.CountWhitePixels(secondLineBounds) >= TitleLineWhiteMinimum;
    }

    private static bool HasSlowDownTitleEvidence(PopupEvidenceMasks masks, Rect titleBounds)
    {
        var secondLineBounds = BuildClampedBounds(
            titleBounds.X,
            titleBounds.Y + titleBounds.Height / 2,
            titleBounds.Width,
            titleBounds.Height - titleBounds.Height / 2,
            new Size(titleBounds.Right, titleBounds.Bottom));
        var titleWhitePixels = masks.CountWhitePixels(titleBounds);
        var titleBandCount = masks.CountWhiteTextBands(titleBounds, TitleTextBandRowWhiteMinimum, MinimumTitleTextBandHeight);
        return titleWhitePixels is >= SlowDownTitleWhiteMinimum and <= SlowDownTitleWhiteMaximum &&
               titleWhitePixels <= titleBounds.Width * titleBounds.Height * TitleWhiteMaximumDensity &&
               titleBandCount == 1 &&
               masks.CountWhitePixels(secondLineBounds) <= SlowDownTitleSecondHalfWhiteMaximum;
    }

    private static bool HasConnectionLostTitleEvidence(PopupEvidenceMasks masks, Rect titleBounds)
    {
        var secondLineBounds = BuildClampedBounds(
            titleBounds.X,
            titleBounds.Y + titleBounds.Height / 2,
            titleBounds.Width,
            titleBounds.Height - titleBounds.Height / 2,
            new Size(titleBounds.Right, titleBounds.Bottom));
        var titleWhitePixels = masks.CountWhitePixels(titleBounds);
        var titleBandCount = masks.CountWhiteTextBands(titleBounds, TitleTextBandRowWhiteMinimum, MinimumTitleTextBandHeight);
        return titleWhitePixels is >= ConnectionLostTitleWhiteMinimum and <= ConnectionLostTitleWhiteMaximum &&
               titleWhitePixels <= titleBounds.Width * titleBounds.Height * TitleWhiteMaximumDensity &&
               titleBandCount == 1 &&
               masks.CountWhitePixels(secondLineBounds) <= ConnectionLostTitleSecondHalfWhiteMaximum;
    }

    private static bool HasConnectionLostBodyEvidence(PopupEvidenceMasks masks, Rect candidate)
    {
        var hereBounds = BuildRelativeBounds(candidate, 0.60, 0.42, 0.35, 0.24);
        var yellowPixels = masks.CountYellowPixels(hereBounds);
        return yellowPixels is >= ConnectionLostYellowMinimum and <= ConnectionLostYellowMaximum;
    }

    private static bool HasInformationIconEvidence(PopupEvidenceMasks masks, Rect iconBounds)
    {
        var iconWhitePixels = masks.CountIconPixels(iconBounds);
        var iconArea = Math.Max(1, iconBounds.Width * iconBounds.Height);
        if (iconWhitePixels < IconWhiteMinimum ||
            iconWhitePixels > iconArea * IconWhiteMaximumDensity)
        {
            return false;
        }

        var icon = masks.GetLargestIconContour(iconBounds);
        if (icon is null)
        {
            return false;
        }

        var area = icon.Value.Area;
        if (area is < MinimumInformationIconContourArea or > MaximumInformationIconContourArea)
        {
            return false;
        }

        var bounds = icon.Value.Bounds;
        if (bounds.Width < MinimumInformationIconContourWidth ||
            bounds.Height < MinimumInformationIconContourHeight)
        {
            return false;
        }

        if (bounds.Left <= MinimumInformationIconContourMargin ||
            bounds.Top <= MinimumInformationIconContourMargin ||
            bounds.Right >= iconBounds.Width - MinimumInformationIconContourMargin ||
            bounds.Bottom >= iconBounds.Height - MinimumInformationIconContourMargin)
        {
            return false;
        }

        var aspectRatio = bounds.Width / (double)Math.Max(1, bounds.Height);
        if (aspectRatio is < MinimumInformationIconAspectRatio or > MaximumInformationIconAspectRatio)
        {
            return false;
        }

        var fillRatio = area / Math.Max(1, bounds.Width * bounds.Height);
        return fillRatio < MaximumFilledSquareIconFillRatio;
    }

    private static bool HasWhitePixelEvidence(
        PopupEvidenceMasks masks,
        Rect bounds,
        int minimumWhitePixels,
        double maximumWhiteDensity)
    {
        var whitePixels = masks.CountWhitePixels(bounds);
        return HasWhitePixelEvidence(whitePixels, bounds, minimumWhitePixels, maximumWhiteDensity);
    }

    private static bool HasWhitePixelEvidence(
        int whitePixels,
        Rect bounds,
        int minimumWhitePixels,
        double maximumWhiteDensity)
    {
        var area = Math.Max(1, bounds.Width * bounds.Height);
        return whitePixels >= minimumWhitePixels &&
               whitePixels <= area * maximumWhiteDensity;
    }

    private static Rect BuildTitleBounds(Rect candidate)
    {
        return BuildRelativeBounds(candidate, 0.18, 0, 0.73, 0.28);
    }

    private static Rect BuildBodyBounds(Rect candidate)
    {
        return BuildRelativeBounds(candidate, 0.04, 0.30, 0.90, 0.40);
    }

    private static Rect BuildIconBounds(Rect candidate)
    {
        return BuildRelativeBounds(candidate, 0, 0, 0.30, 0.45);
    }

    private static Rect BuildButtonBounds(Rect candidate)
    {
        return BuildRelativeBounds(candidate, 0.04, 0.68, 0.90, 0.24);
    }

    private static Rect BuildRelativeBounds(
        Rect candidate,
        double leftRatio,
        double topRatio,
        double widthRatio,
        double heightRatio)
    {
        return BuildClampedBounds(
            candidate.X + (int)Math.Round(candidate.Width * leftRatio),
            candidate.Y + (int)Math.Round(candidate.Height * topRatio),
            (int)Math.Round(candidate.Width * widthRatio),
            (int)Math.Round(candidate.Height * heightRatio),
            new Size(candidate.Right, candidate.Bottom));
    }

    private static Rect BuildClampedBounds(int x, int y, int width, int height, Size containingSize)
    {
        var clampedX = Math.Clamp(x, 0, Math.Max(0, containingSize.Width - MinimumSearchDimension));
        var clampedY = Math.Clamp(y, 0, Math.Max(0, containingSize.Height - MinimumSearchDimension));
        var clampedWidth = Math.Clamp(width, MinimumSearchDimension, containingSize.Width - clampedX);
        var clampedHeight = Math.Clamp(height, MinimumSearchDimension, containingSize.Height - clampedY);
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

    private sealed class PopupEvidenceMasks : IDisposable
    {
        private readonly Mat m_WhiteMask;
        private readonly Mat m_IconMask;
        private readonly Mat m_WhiteIntegral;
        private readonly Mat m_IconIntegral;
        private readonly Mat m_CyanIntegral;
        private readonly Mat m_YellowIntegral;

        private PopupEvidenceMasks(Mat whiteMask, Mat iconMask, Mat whiteIntegral, Mat iconIntegral, Mat cyanIntegral, Mat yellowIntegral)
        {
            m_WhiteMask = whiteMask;
            m_IconMask = iconMask;
            m_WhiteIntegral = whiteIntegral;
            m_IconIntegral = iconIntegral;
            m_CyanIntegral = cyanIntegral;
            m_YellowIntegral = yellowIntegral;
        }

        public static PopupEvidenceMasks Create(Mat image)
        {
            var whiteMask = new Mat();
            Cv2.InRange(image, WhiteMinimum, WhiteMaximum, whiteMask);
            var iconMask = new Mat();
            Cv2.InRange(image, IconMinimum, WhiteMaximum, iconMask);

            using var hsv = new Mat();
            using var cyanMask = new Mat();
            using var yellowMask = new Mat();
            Cv2.CvtColor(image, hsv, ColorConversionCodes.BGR2HSV);
            Cv2.InRange(hsv, CyanMinimum, CyanMaximum, cyanMask);
            Cv2.InRange(hsv, YellowMinimum, YellowMaximum, yellowMask);

            var whiteIntegral = new Mat();
            var iconIntegral = new Mat();
            var cyanIntegral = new Mat();
            var yellowIntegral = new Mat();
            Cv2.Integral(whiteMask, whiteIntegral, MatType.CV_64F);
            Cv2.Integral(iconMask, iconIntegral, MatType.CV_64F);
            Cv2.Integral(cyanMask, cyanIntegral, MatType.CV_64F);
            Cv2.Integral(yellowMask, yellowIntegral, MatType.CV_64F);
            return new PopupEvidenceMasks(whiteMask, iconMask, whiteIntegral, iconIntegral, cyanIntegral, yellowIntegral);
        }

        public int CountWhitePixels(Rect bounds)
        {
            return CountPixels(m_WhiteIntegral, bounds);
        }

        public int CountCyanPixels(Rect bounds)
        {
            return CountPixels(m_CyanIntegral, bounds);
        }

        public int CountIconPixels(Rect bounds)
        {
            return CountPixels(m_IconIntegral, bounds);
        }

        public int CountYellowPixels(Rect bounds)
        {
            return CountPixels(m_YellowIntegral, bounds);
        }

        public int CountWhiteTextBands(Rect bounds, int rowWhiteMinimum = BodyTextBandRowWhiteMinimum, int minimumBandHeight = MinimumBodyTextBandHeight)
        {
            var bands = 0;
            var currentBandHeight = 0;

            for (var row = bounds.Top; row < bounds.Bottom; row++)
            {
                var rowWhitePixels = CountWhitePixels(new Rect(bounds.Left, row, bounds.Width, 1));
                if (rowWhitePixels >= rowWhiteMinimum)
                {
                    currentBandHeight++;
                    continue;
                }

                if (currentBandHeight >= minimumBandHeight)
                {
                    bands++;
                }

                currentBandHeight = 0;
            }

            if (currentBandHeight >= minimumBandHeight)
            {
                bands++;
            }

            return bands;
        }

        public double GetLargestWhiteContourArea(Rect bounds)
        {
            return GetLargestWhiteContour(bounds)?.Area ?? 0.0;
        }

        private WhiteContour? GetLargestWhiteContour(Rect bounds)
        {
            return GetLargestContour(m_WhiteMask, bounds);
        }

        public WhiteContour? GetLargestIconContour(Rect bounds)
        {
            return GetLargestContour(m_IconMask, bounds);
        }

        public void Dispose()
        {
            m_WhiteMask.Dispose();
            m_IconMask.Dispose();
            m_WhiteIntegral.Dispose();
            m_IconIntegral.Dispose();
            m_CyanIntegral.Dispose();
            m_YellowIntegral.Dispose();
        }

        private static WhiteContour? GetLargestContour(Mat mask, Rect bounds)
        {
            using var region = new Mat(mask, bounds);
            using var regionCopy = region.Clone();
            Cv2.FindContours(
                regionCopy,
                out var contours,
                out _,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple);

            WhiteContour? largestContour = null;
            foreach (var contour in contours)
            {
                var area = Cv2.ContourArea(contour);
                if (largestContour is not null && area <= largestContour.Value.Area)
                {
                    continue;
                }

                largestContour = new WhiteContour(area, Cv2.BoundingRect(contour));
            }

            return largestContour;
        }

        private static int CountPixels(Mat integral, Rect bounds)
        {
            var topLeft = integral.At<double>(bounds.Y, bounds.X);
            var topRight = integral.At<double>(bounds.Y, bounds.Right);
            var bottomLeft = integral.At<double>(bounds.Bottom, bounds.X);
            var bottomRight = integral.At<double>(bounds.Bottom, bounds.Right);
            return (int)Math.Round((bottomRight - bottomLeft - topRight + topLeft) / BinaryMaskMaxValue);
        }

        public readonly record struct WhiteContour(double Area, Rect Bounds);
    }

    private enum PopupTitleKind
    {
        MaximumSubmissions,
        SlowDown,
        ConnectionLost
    }

    private readonly record struct PopupCandidateEvidence(Rect Bounds, int Score);
    private readonly record struct PopupTemplateMatch(PopupState PopupState, double Score, double Threshold, Rect Bounds, double Scale);

    private sealed class PopupTemplates : IDisposable
    {
        private PopupTemplates(Mat connectionLostGray, Mat maximumSubmissionsGray, Mat slowDownGray)
        {
            ConnectionLostGray = connectionLostGray;
            MaximumSubmissionsGray = maximumSubmissionsGray;
            SlowDownGray = slowDownGray;
        }

        public Mat ConnectionLostGray { get; }
        public Mat MaximumSubmissionsGray { get; }
        public Mat SlowDownGray { get; }

        public static PopupTemplates Load()
        {
            return new PopupTemplates(
                LoadGrayTemplate(Resources.connection_lost_popup),
                LoadGrayTemplate(Resources.max_submissions_popup),
                LoadGrayTemplate(Resources.slow_down_popup));
        }

        public void Dispose()
        {
            ConnectionLostGray.Dispose();
            MaximumSubmissionsGray.Dispose();
            SlowDownGray.Dispose();
        }

        private static Mat LoadGrayTemplate(System.Drawing.Bitmap bitmap)
        {
            using var memory = new MemoryStream();
            bitmap.Save(memory, ImageFormat.Png);
            using var template = Cv2.ImDecode(memory.ToArray(), ImreadModes.Color);
            if (template.Empty())
            {
                throw new InvalidOperationException("Popup template resource could not be decoded.");
            }

            var gray = new Mat();
            Cv2.CvtColor(template, gray, ColorConversionCodes.BGR2GRAY);
            return gray;
        }
    }
}
