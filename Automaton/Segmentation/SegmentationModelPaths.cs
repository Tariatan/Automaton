using System.IO;

namespace Automaton.Segmentation;

internal static class SegmentationModelPaths
{
    private const string ModelFileName = "discovery-segmentation.onnx";
    private const string ModelsFolderName = "Models";

    public static string GetModelPath()
    {
        return Path.Combine(ModelsFolderName, ModelFileName);
    }
}
