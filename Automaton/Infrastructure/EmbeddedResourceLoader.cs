using System.IO;
using System.Reflection;
using OpenCvSharp;

namespace Automaton.Infrastructure;

internal static class EmbeddedResourceLoader
{
    private static readonly Assembly ResourceAssembly = typeof(EmbeddedResourceLoader).Assembly;

    public static Mat LoadMat(string resourceFileName, ImreadModes mode = ImreadModes.Color)
    {
        var bytes = LoadBytes(resourceFileName);
        var mat = Cv2.ImDecode(bytes, mode);
        return mat.Empty() ? throw new InvalidOperationException($"Failed to decode embedded resource '{resourceFileName}'.") : mat;
    }

    private static byte[] LoadBytes(string resourceFileName)
    {
        var resourceName = FindResourceName(resourceFileName);
        using var stream = ResourceAssembly.GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException(
                               $"Embedded resource '{resourceFileName}' not found. Available: {string.Join(", ", ResourceAssembly.GetManifestResourceNames())}");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static string FindResourceName(string resourceFileName)
    {
        var suffix = "." + resourceFileName.Replace('/', '.').Replace('\\', '.');
        foreach (var name in ResourceAssembly.GetManifestResourceNames())
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        throw new InvalidOperationException(
            $"Embedded resource ending with '{suffix}' not found. Available: {string.Join(", ", ResourceAssembly.GetManifestResourceNames())}");
    }
}
