using System.IO;
using OpenCvSharp;

namespace Automaton.Helpers;

internal static class ImageFileWriter
{
    private const int PublishAttemptCount = 3;
    private const int PublishRetryDelayMilliseconds = 150;
    private static readonly Lock PublishLock = new();

    public static void WriteImage(string outputPath, Mat image)
    {
        var fullOutputPath = Path.GetFullPath(outputPath);
        var outputDirectory = Path.GetDirectoryName(fullOutputPath)
                              ?? throw new InvalidOperationException($"Output path has no directory: {outputPath}");
        var stagedPath = BuildStagedPath(fullOutputPath);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(stagedPath)!);
            if (!Cv2.ImWrite(stagedPath, image))
            {
                throw new IOException($"Failed to encode image: {stagedPath}");
            }

            PublishStagedImage(stagedPath, fullOutputPath, outputDirectory);
        }
        finally
        {
            TryDelete(stagedPath);
        }
    }

    private static string BuildStagedPath(string outputPath)
    {
        var extension = Path.GetExtension(outputPath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".png";
        }

        return Path.Combine(
            GetStagingDirectory(),
            $"{Path.GetFileNameWithoutExtension(outputPath)}.{Guid.NewGuid():N}{extension}");
    }

    private static string GetStagingDirectory()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var rootDirectory = string.IsNullOrWhiteSpace(localApplicationData)
            ? Path.GetTempPath()
            : localApplicationData;
        return Path.Combine(rootDirectory, "Automaton", "TelemetryStaging");
    }

    private static void PublishStagedImage(string stagedPath, string outputPath, string outputDirectory)
    {
        var publishTempPath = Path.Combine(
            outputDirectory,
            $"{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            PublishWithRetry(() =>
            {
                Directory.CreateDirectory(outputDirectory);
                TryDelete(publishTempPath);
                File.Copy(stagedPath, publishTempPath, overwrite: false);
                File.Move(publishTempPath, outputPath, overwrite: true);
            });
        }
        finally
        {
            TryDelete(publishTempPath);
        }
    }

    private static void PublishWithRetry(Action publish)
    {
        for (var attempt = 1; attempt <= PublishAttemptCount; attempt++)
        {
            try
            {
                lock (PublishLock)
                {
                    publish();
                }

                return;
            }
            catch (Exception exception) when (IsTransientPublishFailure(exception) && attempt < PublishAttemptCount)
            {
                Thread.Sleep(PublishRetryDelayMilliseconds);
            }
        }
    }

    private static bool IsTransientPublishFailure(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
