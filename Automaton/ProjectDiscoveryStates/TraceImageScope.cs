using Automaton.Helpers;
using System.IO;
using Serilog;

namespace Automaton.ProjectDiscoveryStates;

internal sealed class TraceImageScope(bool keepImages) : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<TraceImageScope>();
    private readonly HashSet<string> m_ImagePaths = new(StringComparer.OrdinalIgnoreCase);

    public void Track(ScreenCaptureAnalysisSummary captureSummary)
    {
        Track(captureSummary.CapturePath);
        Track(captureSummary.Analysis.Result.OutputPath);
    }

    public void Track(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return;
        }

        m_ImagePaths.Add(Path.GetFullPath(imagePath));
    }

    public void Dispose()
    {
        if (keepImages)
        {
            return;
        }

        foreach (var imagePath in m_ImagePaths)
        {
            DeleteImageFile(imagePath);
        }
    }

    private static void DeleteImageFile(string imagePath)
    {
        try
        {
            if (!File.Exists(imagePath))
            {
                return;
            }

            File.Delete(imagePath);
            Logger.Debug("Deleted trace image. ImagePath={ImagePath}", imagePath);
        }
        catch (Exception exception)
        {
            Logger.Warning(exception, "Could not delete trace image. ImagePath={ImagePath}", imagePath);
        }
    }
}
