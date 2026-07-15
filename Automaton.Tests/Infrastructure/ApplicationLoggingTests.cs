using Automaton.Infrastructure;

namespace Automaton.Tests.Infrastructure;

public sealed class ApplicationLoggingTests
{
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
