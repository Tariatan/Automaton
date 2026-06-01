using Automaton.Helpers;
using Automaton.Infrastructure;
using OpenCvSharp;

namespace Automaton.Detectors;

internal sealed class MiningLaserDetector : IDisposable
{
    private const double MinimumLaserMatchScore = 0.74;
    private static readonly double[] TemplateScales = [1.0, 0.90, 1.10];
    private static readonly Rect LaserSearchBounds = new(1800, 190, 200, 100);

    private readonly Mat[] m_TemplateVariants;

    public MiningLaserDetector()
    {
        var original = EmbeddedResourceLoader.LoadMat("mining.mining_laser.png");
        m_TemplateVariants = BuildScaledVariants(original);
    }

    public void Dispose()
    {
        foreach (var mat in m_TemplateVariants)
        {
            mat.Dispose();
        }
    }

    public bool Detect(Mat screen)
    {
        if (screen.Empty())
        {
            return false;
        }

        if (!TryCreateRegion(screen, LaserSearchBounds, out var region))
        {
            return false;
        }

        using (region)
        {
            foreach (var template in m_TemplateVariants)
            {
                if (template.Width > region.Width || template.Height > region.Height)
                {
                    continue;
                }

                using var result = new Mat();
                Cv2.MatchTemplate(region, template, result, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(result, out _, out var maxScore, out _, out _);
                if (maxScore >= MinimumLaserMatchScore)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private static Mat[] BuildScaledVariants(Mat original)
    {
        var variants = new Mat[TemplateScales.Length];
        for (var i = 0; i < TemplateScales.Length; i++)
        {
            var scale = TemplateScales[i];
            if (GeometryHelper.IsUnscaled(scale))
            {
                variants[i] = original;
            }
            else
            {
                var width = Math.Max(1, (int)Math.Round(original.Width * scale));
                var height = Math.Max(1, (int)Math.Round(original.Height * scale));
                var scaled = new Mat();
                Cv2.Resize(original, scaled, new Size(width, height));
                variants[i] = scaled;
            }
        }

        return variants;
    }

    private static bool TryCreateRegion(Mat screen, Rect bounds, out Mat region)
    {
        var x = Math.Clamp(bounds.X, 0, Math.Max(0, screen.Width - 1));
        var y = Math.Clamp(bounds.Y, 0, Math.Max(0, screen.Height - 1));
        var right = Math.Clamp(bounds.Right, x + 1, screen.Width);
        var bottom = Math.Clamp(bounds.Bottom, y + 1, screen.Height);
        if (right <= x || bottom <= y)
        {
            region = null!;
            return false;
        }

        region = new Mat(screen, new Rect(x, y, right - x, bottom - y));
        return true;
    }
}
