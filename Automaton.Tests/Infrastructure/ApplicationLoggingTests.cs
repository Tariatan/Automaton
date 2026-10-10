using Automaton.Core.Infrastructure;
using Serilog;

namespace Automaton.Tests.Infrastructure;

public sealed class ApplicationLoggingTests
{
    [Fact]
    public void Configure_RestartOnSameDay_AppendsAndPublishesCombinedLog()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var activeDirectory = Path.Combine(workspace.Path, "local");
        var telemetryDirectory = Path.Combine(workspace.Path, "telemetry");
        var firstStart = new DateTime(2026, 10, 10, 8, 0, 0);

        // Act
        ApplicationLogFiles first;
        ApplicationLogFiles second;
        try
        {
            first = ApplicationLogging.Configure(activeDirectory, telemetryDirectory, firstStart);
            Log.Information("First session marker");
            Log.CloseAndFlush();
            Assert.True(ApplicationLogging.TryPublish(first));

            second = ApplicationLogging.Configure(activeDirectory, telemetryDirectory, firstStart.AddHours(2));
            Log.Information("Second session marker");
            Log.CloseAndFlush();
            Assert.True(ApplicationLogging.TryPublish(second));
        }
        finally
        {
            Log.CloseAndFlush();
        }

        // Assert
        Assert.Equal(first, second);
        Assert.Equal("2026-10-10.log", Path.GetFileName(second.ActiveLogFilePath));
        Assert.Single(Directory.GetFiles(activeDirectory, "*.log"));
        Assert.Single(Directory.GetFiles(telemetryDirectory, "*.log"));
        var content = File.ReadAllText(second.ActiveLogFilePath);
        Assert.Contains("First session marker", content);
        Assert.Contains("Second session marker", content);
        Assert.Equal(content, File.ReadAllText(second.TelemetryLogFilePath));
    }

    [Fact]
    public void Configure_StartOnNextDay_CreatesSeparateLog()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var activeDirectory = Path.Combine(workspace.Path, "local");
        var telemetryDirectory = Path.Combine(workspace.Path, "telemetry");
        var firstStart = new DateTime(2026, 10, 10);

        // Act
        ApplicationLogFiles first;
        ApplicationLogFiles second;
        try
        {
            first = ApplicationLogging.Configure(activeDirectory, telemetryDirectory, firstStart);
            Log.CloseAndFlush();
            second = ApplicationLogging.Configure(activeDirectory, telemetryDirectory, firstStart.AddDays(1));
        }
        finally
        {
            Log.CloseAndFlush();
        }

        // Assert
        Assert.NotEqual(first.ActiveLogFilePath, second.ActiveLogFilePath);
        Assert.Equal("2026-10-11.log", Path.GetFileName(second.ActiveLogFilePath));
        Assert.Equal(2, Directory.GetFiles(activeDirectory, "*.log").Length);
    }

    [Fact]
    public void TryPublish_LogFileExists_CopiesLogFileToTelemetryPath()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var activeLogFilePath = Path.Combine(workspace.Path, "local", "active.log");
        var telemetryLogFilePath = Path.Combine(workspace.Path, "telemetry", "published.log");
        Directory.CreateDirectory(Path.GetDirectoryName(activeLogFilePath)!);
        File.WriteAllText(activeLogFilePath, "finished log");
        var logFiles = new ApplicationLogFiles(activeLogFilePath, telemetryLogFilePath);

        // Act
        var published = ApplicationLogging.TryPublish(logFiles);

        // Assert
        Assert.True(published);
        Assert.True(File.Exists(telemetryLogFilePath));
        Assert.Equal("finished log", File.ReadAllText(telemetryLogFilePath));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(telemetryLogFilePath)!, "*.tmp"));
    }

    [Fact]
    public void TryPublish_LogFileMissing_ReturnsFalse()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var logFiles = new ApplicationLogFiles(
            Path.Combine(workspace.Path, "local", "missing.log"),
            Path.Combine(workspace.Path, "telemetry", "published.log"));

        // Act
        var published = ApplicationLogging.TryPublish(logFiles);

        // Assert
        Assert.False(published);
        Assert.False(File.Exists(logFiles.TelemetryLogFilePath));
    }
}
