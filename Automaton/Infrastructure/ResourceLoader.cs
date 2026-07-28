using System.Reflection;
using Automaton.Core.Infrastructure;
using OpenCvSharp;

namespace Automaton.Infrastructure;

internal static class ResourceLoader
{
    internal static readonly Assembly Assembly = typeof(ResourceLoader).Assembly;

    internal static Mat LoadMat(string resourceFileName, ImreadModes mode = ImreadModes.Color)
        => EmbeddedResourceLoader.LoadMat(resourceFileName, Assembly, mode);
}