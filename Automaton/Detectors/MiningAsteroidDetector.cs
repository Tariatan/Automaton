using Automaton.Infrastructure;
using OpenCvSharp;

namespace Automaton.Detectors;

internal sealed class MiningAsteroidDetector : IDisposable
{
    private const double MinimumAsteroidMatchScore = 0.74;
    private static readonly double[] TemplateScales = [1.0, 0.90, 1.10, 0.80, 1.20];
    private static readonly Rect AsteroidBounds = new(1800, 20, 120, 120);

    private readonly Mat[][] m_TemplateVariants;

    public MiningAsteroidDetector()
    {
        Mat[] originals =
        [
            EmbeddedResourceLoader.LoadMat("mining.asteroid_pyroxeres.png"),
            EmbeddedResourceLoader.LoadMat("mining.asteroid_scordite.png"),
            EmbeddedResourceLoader.LoadMat("mining.asteroid_veldspar.png")
        ];

        m_TemplateVariants = new Mat[originals.Length][];
        for (var i = 0; i < originals.Length; i++)
        {
            m_TemplateVariants[i] = BuildScaledVariants(originals[i]);
        }
    }

    public void Dispose()
    {
        foreach (var variants in m_TemplateVariants)
        {
            foreach (var mat in variants)
            {
                mat.Dispose();
            }
        }
    }

    public bool Detect(Mat screen)
    {
        if (screen.Empty())
        {
            return false;
        }

        if (!TryCreateRegion(screen, BuildAsteroidSearchBounds(screen.Size()), out var region))
        {
            return false;
        }

        using (region)
        {
            foreach (var variants in m_TemplateVariants)
            {
                foreach (var template in variants)
                {
                    if (template.Width > region.Width || template.Height > region.Height)
                    {
                        continue;
                    }

                    using var result = new Mat();
                    Cv2.MatchTemplate(region, template, result, TemplateMatchModes.CCoeffNormed);
                    Cv2.MinMaxLoc(result, out _, out var maxScore, out _, out _);
                    if (maxScore >= MinimumAsteroidMatchScore)
                    {
                        return true;
                    }
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
            if (Math.Abs(scale - 1.0) < 1e-10)
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

    private static Rect BuildAsteroidSearchBounds(Size imageSize)
    {
        var left = Math.Clamp(AsteroidBounds.X - 420, 0, Math.Max(0, imageSize.Width - 1));
        var top = Math.Clamp(AsteroidBounds.Y - 30, 0, Math.Max(0, imageSize.Height - 1));
        var right = Math.Clamp(AsteroidBounds.Right + 80, left + 1, imageSize.Width);
        var bottom = Math.Clamp(AsteroidBounds.Bottom + 90, top + 1, imageSize.Height);
        return new Rect(left, top, right - left, bottom - top);
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
