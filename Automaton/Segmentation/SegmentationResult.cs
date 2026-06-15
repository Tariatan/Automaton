using OpenCvSharp;

namespace Automaton.Segmentation;

internal sealed class SegmentationResult(Mat mask, IReadOnlyList<Point[]> polygons, float confidence)
    : IDisposable
{
    public static SegmentationResult Empty { get; } = new(new Mat(), [], 0f);

    public Mat Mask { get; } = mask;
    public IReadOnlyList<Point[]> Polygons { get; } = polygons;
    public float Confidence { get; } = confidence;
    public bool IsEmpty => Polygons.Count == 0;

    public void Dispose() => Mask.Dispose();
}
