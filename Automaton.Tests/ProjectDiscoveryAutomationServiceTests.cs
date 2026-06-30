using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.ProjectDiscoveryStates;
using Automaton.Tests.Stubs;
using OpenCvSharp;

namespace Automaton.Tests;

[Collection(CurrentDirectorySensitiveCollection.Name)]
public sealed class ProjectDiscoveryAutomationServiceTests
{
    [Fact]
    public void Automate_StateReturnsShutdown_ReturnsShutdownSummary()
    {
        // Arrange
        var shutdownState = new ShutdownDiscoveryState();
        using var serviceHarness = new ProjectDiscoveryAutomationServiceHarness(
            new FixedDiscoveryAutomationStateFactory(shutdownState));

        // Act
        var summary = serviceHarness.Service.Automate(
            CancellationToken.None,
            DiscoveryAutomationStateKind.Discover);

        // Assert
        Assert.Equal(DiscoveryAutomationStateKind.Discover, summary.State);
        Assert.Equal(DiscoveryAutomationStateKind.Recovery, summary.NextState);
        Assert.Equal(DiscoveryAutomationActionKind.Shutdown, summary.Action);
        Assert.Equal(1, shutdownState.ExecuteCallCount);
    }

    [Fact]
    public void ExtractTrainingPlayfields_MaskedCompanionStartsWithBaseName_ExtractsMaskedCompanion()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        CreateTrainingWorkspace(workspace.Path);
        CopyTrainingImage(workspace.Path, "17.sample.png");
        WriteSolidImage(Path.Combine(workspace.Path, "Training", "17.sample.expected.masked.png"), Scalar.White);

        using var serviceHarness = new ProjectDiscoveryAutomationServiceHarness();
        var currentDirectory = Directory.GetCurrentDirectory();

        TrainingExtractionSummary summary;

        // Act
        Directory.SetCurrentDirectory(workspace.Path);
        try
        {
            summary = serviceHarness.Service.ExtractTrainingPlayfields();
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.Equal(1, summary.Extracted);

        var maskedOutputPath = Path.Combine(workspace.Path, "playfields", "17.sample.masked.png");
        Assert.True(File.Exists(maskedOutputPath));

        using var maskedOutput = Cv2.ImRead(maskedOutputPath, ImreadModes.Grayscale);
        Assert.False(maskedOutput.Empty());
        Assert.Equal(maskedOutput.Width * maskedOutput.Height, Cv2.CountNonZero(maskedOutput));
    }

    [Fact]
    public void ExtractTrainingPlayfields_MaskedCompanionOnlyContainsBaseName_DoesNotExtractMaskedCompanion()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        CreateTrainingWorkspace(workspace.Path);
        CopyTrainingImage(workspace.Path, "17.sample.png");
        WriteSolidImage(Path.Combine(workspace.Path, "Training", "117.sample.expected.masked.png"), Scalar.White);

        using var serviceHarness = new ProjectDiscoveryAutomationServiceHarness();
        var currentDirectory = Directory.GetCurrentDirectory();

        TrainingExtractionSummary summary;

        // Act
        Directory.SetCurrentDirectory(workspace.Path);
        try
        {
            summary = serviceHarness.Service.ExtractTrainingPlayfields();
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.Equal(1, summary.Extracted);
        Assert.False(File.Exists(Path.Combine(workspace.Path, "playfields", "17.sample.masked.png")));
    }

    private sealed class ProjectDiscoveryAutomationServiceHarness : IDisposable
    {
        private readonly PlayfieldDetector m_PlayfieldDetector = new();
        private readonly ClientIsRunningButtonDetector m_ClientIsRunningButtonDetector = new();

        public ProjectDiscoveryAutomationServiceHarness(
            IDiscoveryAutomationStateFactory? discoveryAutomationStateFactory = null)
        {
            var sampleImageProcessor = new SampleImageProcessor(m_PlayfieldDetector, null);
            var screenCaptureService = new ScreenCaptureService(
                new StubScreenCaptureProvider(() => new Mat(1, 1, MatType.CV_8UC3, Scalar.Black)),
                sampleImageProcessor,
                persistCaptures: false);

            Service = new ProjectDiscoveryAutomationService(
                screenCaptureService,
                sampleImageProcessor,
                m_PlayfieldDetector,
                new StubAutomationInputController(),
                new StubGameActionService(),
                new ConnectionLostPopupDetector(),
                m_ClientIsRunningButtonDetector,
                discoveryAutomationStateFactory ?? new StubDiscoveryAutomationStateFactory());
        }

        public ProjectDiscoveryAutomationService Service { get; }

        public void Dispose()
        {
            m_ClientIsRunningButtonDetector.Dispose();
            m_PlayfieldDetector.Dispose();
        }
    }

    private static void CreateTrainingWorkspace(string workspacePath)
    {
        Directory.CreateDirectory(Path.Combine(workspacePath, "Training"));
        Directory.CreateDirectory(Path.Combine(workspacePath, "playfields"));
    }

    private static void CopyTrainingImage(string workspacePath, string fileName)
    {
        File.Copy(
            SyntheticDiscoveryImageFactory.GetTwoClusterImagePath(),
            Path.Combine(workspacePath, "Training", fileName));
    }

    private static void WriteSolidImage(string path, Scalar color)
    {
        using var sampleImage = Cv2.ImRead(SyntheticDiscoveryImageFactory.GetTwoClusterImagePath());
        using var image = new Mat(sampleImage.Size(), MatType.CV_8UC3, color);
        Cv2.ImWrite(path, image);
    }

    private sealed class StubDiscoveryAutomationStateFactory : IDiscoveryAutomationStateFactory
    {
        public IProjectDiscoveryAutomationState Create(DiscoveryAutomationStateKind stateKind)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FixedDiscoveryAutomationStateFactory(IProjectDiscoveryAutomationState state)
        : IDiscoveryAutomationStateFactory
    {
        public IProjectDiscoveryAutomationState Create(DiscoveryAutomationStateKind stateKind)
        {
            return state;
        }
    }

    private sealed class ShutdownDiscoveryState : IProjectDiscoveryAutomationState
    {
        public DiscoveryAutomationStateKind Kind => DiscoveryAutomationStateKind.Discover;
        public int ExecuteCallCount { get; private set; }

        public DiscoveryAutomationStateTransition Execute(
            ProjectDiscoveryAutomationContext context,
            CancellationToken cancellationToken)
        {
            ExecuteCallCount++;
            return new DiscoveryAutomationStateTransition(
                Kind,
                DiscoveryAutomationStateKind.Recovery,
                DiscoveryAutomationActionKind.Shutdown);
        }
    }
}
