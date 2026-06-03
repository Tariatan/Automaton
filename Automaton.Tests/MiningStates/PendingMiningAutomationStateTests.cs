using Automaton.Helpers;
using Automaton.MiningStates;
using Automaton.Tests.Stubs;

namespace Automaton.Tests.MiningStates;

public sealed class PendingMiningAutomationStateTests
{
    [Fact]
    public void Kind_ProvidedInConstructor_ReturnsSameKind()
    {
        // Arrange
        var state = new PendingMiningAutomationState(MiningAutomationStateKind.Mining);

        // Act
        var kind = state.Kind;

        // Assert
        Assert.Equal(MiningAutomationStateKind.Mining, kind);
    }

    [Fact]
    public void Kind_DifferentKindProvided_ReturnsProvidedKind()
    {
        // Arrange
        var state = new PendingMiningAutomationState(MiningAutomationStateKind.Recovery);

        // Act
        var kind = state.Kind;

        // Assert
        Assert.Equal(MiningAutomationStateKind.Recovery, kind);
    }

    [Fact]
    public void Execute_Default_ReturnsSelfTransitionWithNoAction()
    {
        // Arrange
        var state = new PendingMiningAutomationState(MiningAutomationStateKind.Mining);
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() => new OpenCvSharp.Mat(1, 1, OpenCvSharp.MatType.CV_8UC3)),
            new SampleImageProcessor(),
            persistCaptures: false);
        var context = new MiningAutomationContext(screenCaptureService, new StubAutomationClock());

        // Act
        var transition = state.Execute(context, CancellationToken.None);

        // Assert
        Assert.Equal(MiningAutomationStateKind.Mining, transition.State);
        Assert.Equal(MiningAutomationStateKind.Mining, transition.NextState);
        Assert.Equal(MiningAutomationActionKind.None, transition.Action);
    }

    [Fact]
    public void Execute_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var state = new PendingMiningAutomationState(MiningAutomationStateKind.Mining);
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() => new OpenCvSharp.Mat(1, 1, OpenCvSharp.MatType.CV_8UC3)),
            new SampleImageProcessor(),
            persistCaptures: false);
        var context = new MiningAutomationContext(screenCaptureService, new StubAutomationClock());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        Assert.Throws<OperationCanceledException>(() => state.Execute(context, cts.Token));
    }
}