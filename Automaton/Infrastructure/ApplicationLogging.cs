using System.Globalization;
using System.Diagnostics;
using System.IO;
using Serilog;
using Serilog.Events;

namespace Automaton.Infrastructure;

internal static class ApplicationLogging
{
    private const int PublishAttemptCount = 3;
    private const int PublishRetryDelayMilliseconds = 150;
    private const string LogFileTimestampFormat = "yyyy-MM-dd-HH-mm-ss";
    private const string LogFileExtension = ".log";
    private const string OutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}][{SourceContext}]{Message:lj}{NewLine}{Exception}";

    public static ApplicationLogFiles Configure()
    {
        var logFileName = $"{DateTime.Now.ToString(LogFileTimestampFormat, CultureInfo.InvariantCulture)}{LogFileExtension}";
        var activeLogsDirectory = GetActiveLogsDirectory();
        var telemetryLogsDirectory = TelemetryRootDirectory.GetLogsDirectory();
        Directory.CreateDirectory(activeLogsDirectory);

        var logFiles = new ApplicationLogFiles(
            Path.Combine(activeLogsDirectory, logFileName),
            Path.Combine(telemetryLogsDirectory, logFileName));

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(LogEventLevel.Information)
            .WriteTo.File(
                logFiles.ActiveLogFilePath,
                outputTemplate: OutputTemplate)
            .CreateLogger();

        Log.Information(
            "Logging started. ActiveLogFilePath={ActiveLogFilePath}, TelemetryLogFilePath={TelemetryLogFilePath}",
            logFiles.ActiveLogFilePath,
            logFiles.TelemetryLogFilePath);
        return logFiles;
    }

    public static bool TryPublish(ApplicationLogFiles? logFiles)
    {
        if (logFiles is null || !File.Exists(logFiles.ActiveLogFilePath))
        {
            return false;
        }

        try
        {
            Publish(logFiles);
            return true;
        }
        catch (Exception exception) when (IsTransientPublishFailure(exception))
        {
            Trace.TraceError(
                "Failed to publish Automaton log file from '{0}' to '{1}': {2}",
                logFiles.ActiveLogFilePath,
                logFiles.TelemetryLogFilePath,
                exception);
            return false;
        }
    }

    private static string GetActiveLogsDirectory()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var rootDirectory = string.IsNullOrWhiteSpace(localApplicationData)
            ? Path.GetTempPath()
            : localApplicationData;
        return Path.Combine(rootDirectory, "Automaton", "Logs");
    }

    private static void Publish(ApplicationLogFiles logFiles)
    {
        var outputDirectory = Path.GetDirectoryName(logFiles.TelemetryLogFilePath)
                              ?? throw new InvalidOperationException($"Log path has no directory: {logFiles.TelemetryLogFilePath}");
        var publishTempPath = Path.Combine(
            outputDirectory,
            $"{Path.GetFileName(logFiles.TelemetryLogFilePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            PublishWithRetry(() =>
            {
                Directory.CreateDirectory(outputDirectory);
                TryDelete(publishTempPath);
                File.Copy(logFiles.ActiveLogFilePath, publishTempPath, overwrite: false);
                File.Move(publishTempPath, logFiles.TelemetryLogFilePath, overwrite: true);
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
                publish();
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

internal sealed record ApplicationLogFiles(string ActiveLogFilePath, string TelemetryLogFilePath);
