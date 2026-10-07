using Automaton.Core.Helpers;
using Automaton.Infrastructure;
using OpenCvSharp;

namespace Automaton.Detectors;

internal sealed class DailyGiftDetector : IDisposable
{
    private const double MinimumMatchScore = 0.88;
    private const double EarlyExitScore = 0.98;
    private const int WindowLeft = 440;
    private const int WindowTop = 450;
    private const int WindowWidth = 1664;
    private const int WindowHeight = 1082;
    private const int WindowSearchMarginX = 50;
    private const int WindowSearchMarginY = 200;
    private const int PositionTolerance = 4;
    private static readonly int[] SearchPaddings = [0, PositionTolerance];
    private static readonly Rect TitleBounds = new(24, 17, 323, 39);
    private static readonly Rect ControlsBounds = new(1545, 23, 92, 27);
    private static readonly Rect LoginOptionBounds = new(54, 1014, 288, 31);
    private static readonly Rect ClaimBounds = new(1319, 920, 156, 48);
    private static readonly Rect CloseBounds = new(1350, 920, 94, 48);
    private readonly Mat m_Title = ResourceLoader.LoadMat("daily_login_title.png", ImreadModes.Grayscale);
    private readonly Mat m_Controls = ResourceLoader.LoadMat("daily_login_controls.png", ImreadModes.Grayscale);
    private readonly Mat m_LoginOption = ResourceLoader.LoadMat("daily_login_option.png", ImreadModes.Grayscale);
    private readonly Mat m_Claim = ResourceLoader.LoadMat("daily_login_claim.png", ImreadModes.Grayscale);
    private readonly Mat m_Close = ResourceLoader.LoadMat("daily_login_close.png", ImreadModes.Grayscale);

    public bool Detect(Mat screen, out DailyGiftDetectorDetection detection)
    {
        detection = default;
        if (screen.Empty() || !IsRegionAvailable(screen.Size()))
        {
            return false;
        }

        var searchBounds = GeometryHelper.BuildClampedBounds(
            WindowLeft - WindowSearchMarginX,
            WindowTop - WindowSearchMarginY,
            WindowWidth + (WindowSearchMarginX * 2),
            WindowHeight + (WindowSearchMarginY * 2),
            screen.Size());

        using var searchRegion = new Mat(screen, searchBounds);
        using var gray = ToGrayscale(searchRegion);
        var title = FindMatch(gray, m_Title, new Rect(0, 0, gray.Width, gray.Height));
        if (title is not { } titleBounds)
        {
            return false;
        }

        var windowBounds = new Rect(
            titleBounds.X - TitleBounds.X,
            titleBounds.Y - TitleBounds.Y,
            WindowWidth,
            WindowHeight);
        if (!IsWithin(windowBounds, gray.Size()))
        {
            return false;
        }

        using var window = new Mat(gray, windowBounds);
        if (Match(window, m_Controls, ControlsBounds) is null
            || Match(window, m_LoginOption, LoginOptionBounds) is null)
        {
            return false;
        }

        detection = new DailyGiftDetectorDetection(
            true,
            new Rect(searchBounds.X + windowBounds.X, searchBounds.Y + windowBounds.Y, WindowWidth, WindowHeight),
            ToScreenBounds(Match(window, m_Claim, ClaimBounds), searchBounds.Location + windowBounds.Location),
            ToScreenBounds(Match(window, m_Close, CloseBounds), searchBounds.Location + windowBounds.Location));
        return true;
    }

    public void Dispose()
    {
        m_Title.Dispose();
        m_Controls.Dispose();
        m_LoginOption.Dispose();
        m_Claim.Dispose();
        m_Close.Dispose();
    }

    private static bool IsRegionAvailable(Size size)
        => size.Width >= WindowWidth && size.Height >= WindowHeight;

    private static bool IsWithin(Rect bounds, Size size)
        => bounds.X >= 0 && bounds.Y >= 0 && bounds.Right <= size.Width && bounds.Bottom <= size.Height;

    private static Rect? FindMatch(Mat image, Mat template, Rect searchBounds)
    {
        if (!IsWithin(searchBounds, image.Size())
            || searchBounds.Width < template.Width || searchBounds.Height < template.Height)
        {
            return null;
        }

        using var region = new Mat(image, searchBounds);
        using var result = new Mat();
        Cv2.MatchTemplate(region, template, result, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out var score, out _, out var location);
        return score >= MinimumMatchScore
            ? new Rect(searchBounds.X + location.X, searchBounds.Y + location.Y, template.Width, template.Height)
            : null;
    }

    private static Mat ToGrayscale(Mat image)
    {
        var gray = new Mat();
        switch (image.Channels())
        {
            case 1:
                image.CopyTo(gray);
                break;
            case 3:
                Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
                break;
            case 4:
                Cv2.CvtColor(image, gray, ColorConversionCodes.BGRA2GRAY);
                break;
            default:
                gray.Dispose();
                throw new ArgumentException("Expected a grayscale, BGR, or BGRA screenshot.", nameof(image));
        }

        return gray;
    }

    private static Rect? Match(Mat window, Mat template, Rect expectedBounds)
    {
        var bestScore = double.NegativeInfinity;
        Rect? bestBounds = null;
        // Check the fixed position first; only search nearby when confidence is lower.
        foreach (var padding in SearchPaddings)
        {
            var searchBounds = new Rect(
                expectedBounds.X - padding,
                expectedBounds.Y - padding,
                expectedBounds.Width + padding * 2,
                expectedBounds.Height + padding * 2);
            using var region = new Mat(window, searchBounds);
            using var result = new Mat();
            Cv2.MatchTemplate(region, template, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out var score, out _, out var location);
            if (score > bestScore)
            {
                bestScore = score;
                bestBounds = new Rect(searchBounds.X + location.X, searchBounds.Y + location.Y, template.Width, template.Height);
            }

            if (bestScore >= EarlyExitScore)
            {
                break;
            }
        }

        return bestScore >= MinimumMatchScore ? bestBounds : null;
    }

    private static Rect? ToScreenBounds(Rect? bounds, Point offset)
        => bounds is { } found
            ? new Rect(found.X + offset.X, found.Y + offset.Y, found.Width, found.Height)
            : null;
}

internal readonly record struct DailyGiftDetectorDetection(
    bool IsFound,
    Rect WindowBounds,
    Rect? ClaimButtonBounds,
    Rect? CloseButtonBounds);