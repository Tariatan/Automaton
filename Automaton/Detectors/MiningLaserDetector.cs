using System.Drawing.Imaging;
using System.IO;
using OpenCvSharp;

namespace Automaton.Detectors;

internal sealed class MiningLaserDetector
{
    private const double MinimumLaserMatchScore = 0.74;
    private static readonly double[] TemplateScales = [0.90, 1.0, 1.10];
    // One combined region in top-right selected-item panel: any laser match counts as active.
    private static readonly Rect LaserSearchBounds = new(1740, 170, 250, 160);

    private readonly Mat m_MiningLaserTemplate = LoadTemplate(Properties.Resources.mining_laser, "mining_laser");

    public bool TryLocate(Mat screen)
    {
        return !screen.Empty() && TryMatchTemplate(screen, m_MiningLaserTemplate, LaserSearchBounds, MinimumLaserMatchScore);
    }

    private static bool TryMatchTemplate(Mat screen, Mat template, Rect bounds, double minimumScore)
    {
        if (!TryCreateRegion(screen, bounds, out var region))
        {
            return false;
        }

        using (region)
        {
            foreach (var scale in TemplateScales)
            {
                using var scaledTemplate = BuildScaledTemplate(template, scale);
                if (scaledTemplate.Width > region.Width || scaledTemplate.Height > region.Height)
                {
                    continue;
                }

                using var result = new Mat();
                Cv2.MatchTemplate(region, scaledTemplate, result, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(result, out _, out var maxScore, out _, out _);
                if (maxScore >= minimumScore)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private static bool TryCreateRegion(Mat screen, Rect bounds, out Mat region)
    {
        region = new Mat();
        var x = Math.Clamp(bounds.X, 0, Math.Max(0, screen.Width - 1));
        var y = Math.Clamp(bounds.Y, 0, Math.Max(0, screen.Height - 1));
        var right = Math.Clamp(bounds.Right, x + 1, screen.Width);
        var bottom = Math.Clamp(bounds.Bottom, y + 1, screen.Height);
        if (right <= x || bottom <= y)
        {
            return false;
        }

        region = new Mat(screen, new Rect(x, y, right - x, bottom - y));
        return true;
    }

    private static Mat LoadTemplate(System.Drawing.Bitmap bitmap, string resourceName)
    {
        using var memoryStream = new MemoryStream();
        bitmap.Save(memoryStream, ImageFormat.Png);
        var template = Cv2.ImDecode(memoryStream.ToArray(), ImreadModes.Color);
        if (template.Empty())
        {
            throw new InvalidOperationException($"Could not load {resourceName} template from resources.");
        }

        return template;
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
}
