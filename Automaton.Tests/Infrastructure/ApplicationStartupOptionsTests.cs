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
}