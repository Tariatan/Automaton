using System.Reflection;
using OpenCvSharp;

namespace Automaton.Core.Infrastructure;

internal static class EmbeddedResourceLoader
{
    public static Mat LoadMat(string resourceFileName, Assembly assembly, ImreadModes mode = ImreadModes.Color)
    {
        var bytes = LoadBytes(resourceFileName, assembly);
        var mat = Cv2.ImDecode(bytes, mode);
        return mat.Empty() ? throw new InvalidOperationException($"Failed to decode embedded resource '{resourceFileName}'.") : mat;
    }

    private static byte[] LoadBytes(string resourceFileName, Assembly assembly)
    {
        var resourceName = FindResourceName(resourceFileName, assembly);
        using var stream = assembly.GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException(
                               $"Embedded resource '{resourceFileName}' not found. Available: {string.Join(", ", assembly.GetManifestResourceNames())}");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static string FindResourceName(string resourceFileName, Assembly assembly)
    {
        var suffix = "." + resourceFileName.Replace('/', '.').Replace('\\', '.');
        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        throw new InvalidOperationException(
            $"Embedded resource ending with '{suffix}' not found. Available: {string.Join(", ", assembly.GetManifestResourceNames())}");
    }
}