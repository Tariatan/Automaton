using Automaton.Infrastructure;

namespace Automaton.Tests.Infrastructure;

public sealed class ApplicationStartupOptionsTests
{
    [Fact]
    public void Parse_NoArguments_DoesNotAutoStart()
    {
        // Arrange
        var arguments = Array.Empty<string>();

        // Act
        var options = ApplicationStartupOptions.Parse(arguments);

        // Assert
        Assert.False(options.ProcessSamples);
        Assert.False(options.AutoStartAutomation);
        Assert.Null(options.SettingsFilePath);
    }

    [Fact]
    public void Parse_ProcessSamplesArgument_EnablesSampleProcessing()
    {
        // Arrange
        var arguments = new[] { "--process-samples" };

        // Act
        var options = ApplicationStartupOptions.Parse(arguments);

        // Assert
        Assert.True(options.ProcessSamples);
        Assert.False(options.AutoStartAutomation);
    }

    [Fact]
    public void Parse_DiscoveryArgument_AutoStartsAutomation()
    {
        // Arrange
        var arguments = new[] { "-discovery" };

        // Act
        var options = ApplicationStartupOptions.Parse(arguments);

        // Assert
        Assert.False(options.ProcessSamples);
        Assert.True(options.AutoStartAutomation);
    }

    [Fact]
    public void Parse_SettingsFileArgument_CapturesPath()
    {
        // Arrange
        var arguments = new[] { "--settings-file", @"C:\users\alice\automaton.json" };

        // Act
        var options = ApplicationStartupOptions.Parse(arguments);

        // Assert
        Assert.Equal(@"C:\users\alice\automaton.json", options.SettingsFilePath);
    }

    [Fact]
    public void Parse_SettingsFileWithoutValue_ReturnsNull()
    {
        // Arrange
        var arguments = new[] { "--settings-file" };

        // Act
        var options = ApplicationStartupOptions.Parse(arguments);

        // Assert
        Assert.Null(options.SettingsFilePath);
    }
}