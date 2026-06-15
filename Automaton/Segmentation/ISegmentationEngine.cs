using OpenCvSharp;

namespace Automaton.Segmentation;

internal interface ISegmentationEngine
{
    bool IsAvailable { get; }

    SegmentationResult Segment(Mat playfieldImage);
}
