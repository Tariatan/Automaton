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
        var expectedPath = Path.Combine(expectedDirectory, "25.sample.expected.png");
        File.Copy(SyntheticDiscoveryImageFactory.GetTwoClusterImagePath(), samplePath);
        WriteExpectedOverlay(
            samplePath,
            expectedPath,
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

    private static void WriteExpectedOverlay(string samplePath, string expectedPath, IReadOnlyList<Point[]> localPolygons)
    {
        using var sampleImage = Cv2.ImRead(samplePath);
        var detector = new PlayfieldDetector();
        var playfieldDetection = detector.Detect(sampleImage);
        if (!playfieldDetection.IsFound)
        {
            throw new InvalidOperationException("Default fallback sample image must contain a detectable playfield.");
        }

        using var expectedImage = sampleImage.Clone();
        using var playfield = new Mat(expectedImage, playfieldDetection.Bounds);
        using var overlay = playfield.Clone();

        foreach (var polygon in localPolygons)
        {
            Cv2.FillPoly(overlay, [polygon], new Scalar(60, 95, 150));
            Cv2.Polylines(overlay, [polygon], true, Scalar.White, 2, LineTypes.AntiAlias);
        }

        Cv2.AddWeighted(overlay, 0.55, playfield, 0.45, 0, playfield);
        Cv2.ImWrite(expectedPath, expectedImage);
    }
}
