using Automaton.Detectors;
using OpenCvSharp;

namespace Automaton.Tests;

internal static class DefaultFallbackExampleFactory
{
    public static void Create(string workspacePath)
    {
        var expectedDirectory = Path.Combine(workspacePath, "expected");
        Directory.CreateDirectory(expectedDirectory);

        var samplePath = Path.Combine(expectedDirectory, "25.sample.png");
        var maskedExpectedPath = Path.Combine(expectedDirectory, "25.sample.expected.masked.png");
        File.Copy(SyntheticDiscoveryImageFactory.GetTwoClusterImagePath(), samplePath);
        WriteMaskedExpectedOverlay(
            samplePath,
            maskedExpectedPath,
            [
                [
                    new Point(30, 70),
                    new Point(250, 70),
                    new Point(250, 230),
                    new Point(30, 230)
                ],
                [
                    new Point(330, 120),
                    new Point(620, 120),
                    new Point(620, 300),
                    new Point(330, 300)
                ],
                [
                    new Point(150, 400),
                    new Point(520, 400),
                    new Point(520, 600),
                    new Point(150, 600)
                ]
            ]);
    }

    private static void WriteMaskedExpectedOverlay(string samplePath, string maskedExpectedPath, IReadOnlyList<Point[]> localPolygons)
    {
        using var sampleImage = Cv2.ImRead(samplePath);
        var detector = new PlayfieldDetector();
        var playfieldDetection = detector.Detect(sampleImage);
        if (!playfieldDetection.IsFound)
        {
            throw new InvalidOperationException("Default fallback sample image must contain a detectable playfield.");
        }

        using var maskedImage = sampleImage.Clone();
        using var grayscale = new Mat();
        Cv2.CvtColor(maskedImage, grayscale, ColorConversionCodes.BGR2GRAY);
        Cv2.CvtColor(grayscale, maskedImage, ColorConversionCodes.GRAY2BGR);

        using var playfield = new Mat(maskedImage, playfieldDetection.Bounds);
        foreach (var polygon in localPolygons)
        {
            Cv2.FillPoly(playfield, [polygon], Scalar.White);
            Cv2.Polylines(playfield, [polygon], true, Scalar.White, 2, LineTypes.AntiAlias);
        }

        Cv2.ImWrite(maskedExpectedPath, maskedImage);
    }
}
