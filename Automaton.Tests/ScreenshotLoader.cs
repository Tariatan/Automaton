using OpenCvSharp;

namespace Automaton.Tests;

internal static class ScreenshotLoader
{
    private static readonly string ScreenshotsRoot = FindScreenshotsRoot();

    public static Mat LoadOrSkip(string relativePath, ImreadModes mode = ImreadModes.Color)
    {
        var fullPath = Path.Combine(ScreenshotsRoot, relativePath);
        SkipIfMissing(fullPath, relativePath);

        var mat = Cv2.ImRead(fullPath, mode);
        if (mat.Empty())
            throw new InvalidOperationException($"Failed to decode screenshot: {fullPath}");

        return mat;
    }

    public static string GetPathOrSkip(string relativePath)
    {
        var fullPath = Path.Combine(ScreenshotsRoot, relativePath);
        SkipIfMissing(fullPath, relativePath);
        return fullPath;
    }

    public static void CopyOrSkip(string relativePath, string destinationPath)
    {
        var fullPath = Path.Combine(ScreenshotsRoot, relativePath);
        SkipIfMissing(fullPath, relativePath);

        File.Copy(fullPath, destinationPath, overwrite: true);
    }

    private static void SkipIfMissing(string fullPath, string relativePath)
    {
        if (!File.Exists(fullPath))
            throw Xunit.Sdk.SkipException.ForSkip($"Screenshot not found: {relativePath}. Capture it and place at: {fullPath}");
    }

    private static string FindScreenshotsRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            var candidate = Path.Combine(directory, "Screenshots");
            if (Directory.Exists(candidate))
                return candidate;

            var projectCandidate = Path.Combine(directory, "Automaton.Tests", "Screenshots");
            if (Directory.Exists(projectCandidate))
                return projectCandidate;

            directory = Path.GetDirectoryName(directory);
        }

        return Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Screenshots");
    }
}
