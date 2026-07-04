using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Infrastructure;
using Automaton.Primitives;
using Automaton.ProjectDiscoveryStates;
using Automaton.Tests.Stubs;
using OpenCvSharp;

namespace Automaton.Tests;

[Collection(CurrentDirectorySensitiveCollection.Name)]
public sealed class ProjectDiscoveryAutomationServiceTests
{
    [Fact]
    public void Automate_RecoveryProbeMisses_DoesNotStoreProbeCaptures()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        using var cancellationSource = new CancellationTokenSource();
        var currentDirectory = Directory.GetCurrentDirectory();
        var automationInputController = new StubAutomationInputController
        {
            OnDelay = delay =>
            {
                if (delay == Delays.StateMachineNextStepDelayMs)
                {
                    cancellationSource.Cancel();
                }
            }
        };
        var probeState = new RecordingDiscoveryState(
            DiscoveryAutomationStateKind.Login,
            DiscoveryAutomationStateKind.Login,
            DiscoveryAutomationActionKind.Recover,
            () => { });
        using var serviceHarness = new ProjectDiscoveryAutomationServiceHarness(
            new FixedDiscoveryAutomationStateFactory(probeState),
            screenCaptureProvider: new StubScreenCaptureProvider(() => new Mat(1200, 900, MatType.CV_8UC3, Scalar.Black)),
            automationInputController: automationInputController,
            persistCaptures: true);
        DiscoveryAutomationStepSummary summary;

        // Act
        Directory.SetCurrentDirectory(workspace.Path);
        try
        {
            summary = serviceHarness.Service.Automate(
                cancellationSource.Token,
                DiscoveryAutomationStateKind.Login);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.Equal(DiscoveryAutomationActionKind.Recover, summary.Action);
        Assert.False(Directory.Exists(Path.Combine(workspace.Path, "captures")));
    }

    [Fact]
    public void Automate_ConnectionLostProbeHits_StoresConnectionLostCapture()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var currentDirectory = Directory.GetCurrentDirectory();
        var probeState = new RecordingDiscoveryState(
            DiscoveryAutomationStateKind.Login,
            DiscoveryAutomationStateKind.Login,
            DiscoveryAutomationActionKind.Recover,
            () => { });
        var shutdownState = new RecordingDiscoveryState(
            DiscoveryAutomationStateKind.RecoverConnectionLostPopup,
            DiscoveryAutomationStateKind.Recovery,
            DiscoveryAutomationActionKind.Shutdown,
            () => { });
        var stateFactory = new MappingDiscoveryAutomationStateFactory(stateKind => stateKind switch
        {
            DiscoveryAutomationStateKind.RecoverConnectionLostPopup => shutdownState,
            _ => probeState
        });
        using var serviceHarness = new ProjectDiscoveryAutomationServiceHarness(
            stateFactory,
            screenCaptureProvider: new StubScreenCaptureProvider(SyntheticCommonImageFactory.LoadConnectionLostPopupImage),
            persistCaptures: true);

        // Act
        Directory.SetCurrentDirectory(workspace.Path);
        try
        {
            serviceHarness.Service.Automate(
                CancellationToken.None,
                DiscoveryAutomationStateKind.Login);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        var capturesDirectory = Path.Combine(workspace.Path, "captures");
        var captureFile = Assert.Single(Directory.EnumerateFiles(capturesDirectory, "*connection-lost-popup-check.png"));
        Assert.DoesNotContain("client-is-running-button-check", Path.GetFileName(captureFile));
    }

    [Fact]
    public void Automate_ClientIsRunningProbeHits_StoresClientIsRunningCapture()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        using var clientIsRunningScreen = CreateClientIsRunningButtonScreen();
        var currentDirectory = Directory.GetCurrentDirectory();
        var probeState = new RecordingDiscoveryState(
            DiscoveryAutomationStateKind.Login,
            DiscoveryAutomationStateKind.Login,
            DiscoveryAutomationActionKind.Recover,
            () => { });
        var shutdownState = new RecordingDiscoveryState(
            DiscoveryAutomationStateKind.RecoverClientIsRunningButtonVisible,
            DiscoveryAutomationStateKind.Recovery,
            DiscoveryAutomationActionKind.Shutdown,
            () => { });
        var stateFactory = new MappingDiscoveryAutomationStateFactory(stateKind => stateKind switch
        {
            DiscoveryAutomationStateKind.RecoverClientIsRunningButtonVisible => shutdownState,
            _ => probeState
        });
        using var serviceHarness = new ProjectDiscoveryAutomationServiceHarness(
            stateFactory,
            screenCaptureProvider: new StubScreenCaptureProvider(clientIsRunningScreen.Clone),
            persistCaptures: true);

        // Act
        Directory.SetCurrentDirectory(workspace.Path);
        try
        {
            serviceHarness.Service.Automate(
                CancellationToken.None,
                DiscoveryAutomationStateKind.Login);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        var capturesDirectory = Path.Combine(workspace.Path, "captures");
        var captureFile = Assert.Single(Directory.EnumerateFiles(capturesDirectory, "*client-is-running-button-check.png"));
        Assert.DoesNotContain("connection-lost-popup-check", Path.GetFileName(captureFile));
    }

    [Fact]
    public void Automate_CurrentStateIsDiscover_HidesUiBeforeExecutingStep()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var currentDirectory = Directory.GetCurrentDirectory();
        var events = new List<string>();
        var gameActionService = new StubGameActionService
        {
            OnTryHideUi = () => events.Add("hide-ui")
        };
        var discoveryState = new RecordingDiscoveryState(
            DiscoveryAutomationStateKind.Discover,
            DiscoveryAutomationStateKind.Recovery,
            DiscoveryAutomationActionKind.Shutdown,
            () => events.Add("execute"));
        using var serviceHarness = new ProjectDiscoveryAutomationServiceHarness(
            new FixedDiscoveryAutomationStateFactory(discoveryState),
            gameActionService,
            new StubScreenCaptureProvider(() => new Mat(2, 3, MatType.CV_8UC3, Scalar.Black)),
            persistCaptures: true);
        DiscoveryAutomationStepSummary summary;

        // Act
        Directory.SetCurrentDirectory(workspace.Path);
        try
        {
            summary = serviceHarness.Service.Automate(
                CancellationToken.None,
                DiscoveryAutomationStateKind.Discover);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.Equal(["hide-ui", "execute"], events);
        Assert.Equal(1, gameActionService.TryHideUiCallCount);
        Assert.Equal(new Size(3, 2), gameActionService.LastTryHideUiImageSize);
        Assert.False(Directory.Exists(Path.Combine(workspace.Path, "captures")));
        Assert.Equal(DiscoveryAutomationActionKind.Shutdown, summary.Action);
    }

    [Fact]
    public void Automate_CurrentStateIsNotDiscover_DoesNotHideUiBeforeExecutingStep()
    {
        // Arrange
        var events = new List<string>();
        var gameActionService = new StubGameActionService
        {
            OnTryHideUi = () => events.Add("hide-ui")
        };
        var loginState = new RecordingDiscoveryState(
            DiscoveryAutomationStateKind.Login,
            DiscoveryAutomationStateKind.Recovery,
            DiscoveryAutomationActionKind.Shutdown,
            () => events.Add("execute"));
        using var serviceHarness = new ProjectDiscoveryAutomationServiceHarness(
            new FixedDiscoveryAutomationStateFactory(loginState),
            gameActionService);

        // Act
        var summary = serviceHarness.Service.Automate(
            CancellationToken.None,
            DiscoveryAutomationStateKind.Login);

        // Assert
        Assert.Equal(["execute"], events);
        Assert.Equal(0, gameActionService.TryHideUiCallCount);
        Assert.Equal(DiscoveryAutomationActionKind.Shutdown, summary.Action);
    }

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
            IDiscoveryAutomationStateFactory? discoveryAutomationStateFactory = null,
            IGameActionService? gameActionService = null,
            IScreenCaptureProvider? screenCaptureProvider = null,
            IAutomationInputController? automationInputController = null,
            bool persistCaptures = false)
        {
            var sampleImageProcessor = new SampleImageProcessor(m_PlayfieldDetector, null);
            var screenCaptureService = new ScreenCaptureService(
                screenCaptureProvider ?? new StubScreenCaptureProvider(() => new Mat(1, 1, MatType.CV_8UC3, Scalar.Black)),
                sampleImageProcessor,
                persistCaptures: persistCaptures);

            Service = new ProjectDiscoveryAutomationService(
                screenCaptureService,
                sampleImageProcessor,
                m_PlayfieldDetector,
                automationInputController ?? new StubAutomationInputController(),
                gameActionService ?? new StubGameActionService(),
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

    private static Mat CreateClientIsRunningButtonScreen()
    {
        using var playButtonScreen = SyntheticCommonImageFactory.LoadPlayButtonScreenImage();
        using var playNowButtonDetector = new PlayNowButtonDetector();
        using var workspace = new TemporaryDirectory();
        var playButtonScreenPath = Path.Combine(workspace.Path, "play-button-screen.png");
        Cv2.ImWrite(playButtonScreenPath, playButtonScreen);
        Assert.True(playNowButtonDetector.Detect(playButtonScreenPath, out var playNowButtonLocation));

        using var clientIsRunningButton = EmbeddedResourceLoader.LoadMat("client_is_running.png");
        var screen = new Mat(playButtonScreen.Size(), MatType.CV_8UC3, Scalar.Black);
        var expectedBounds = new Rect(
            playNowButtonLocation.Bounds.X,
            playNowButtonLocation.Bounds.Y,
            clientIsRunningButton.Width,
            clientIsRunningButton.Height);
        using var region = new Mat(screen, expectedBounds);
        clientIsRunningButton.CopyTo(region);
        return screen;
    }

    private sealed class FixedDiscoveryAutomationStateFactory(IProjectDiscoveryAutomationState state)
        : IDiscoveryAutomationStateFactory
    {
        public IProjectDiscoveryAutomationState Create(DiscoveryAutomationStateKind stateKind)
        {
            return state;
        }
    }

    private sealed class MappingDiscoveryAutomationStateFactory(Func<DiscoveryAutomationStateKind, IProjectDiscoveryAutomationState> createState)
        : IDiscoveryAutomationStateFactory
    {
        public IProjectDiscoveryAutomationState Create(DiscoveryAutomationStateKind stateKind)
        {
            return createState(stateKind);
        }
    }

    private sealed class RecordingDiscoveryState(
        DiscoveryAutomationStateKind state,
        DiscoveryAutomationStateKind nextState,
        DiscoveryAutomationActionKind action,
        Action onExecute) : IProjectDiscoveryAutomationState
    {
        public DiscoveryAutomationStateKind Kind => state;

        public DiscoveryAutomationStateTransition Execute(
            ProjectDiscoveryAutomationContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            onExecute();
            return new DiscoveryAutomationStateTransition(Kind, nextState, action);
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
