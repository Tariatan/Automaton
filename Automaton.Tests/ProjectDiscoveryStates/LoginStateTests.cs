using Automaton.Core.Detectors;
using Automaton.Core.Helpers;
using Automaton.Core.Infrastructure;
using Automaton.ProjectDiscoveryStates;
using Automaton.Detectors;
using Automaton.Tests.Stubs;
using OpenCvSharp;

namespace Automaton.Tests.ProjectDiscoveryStates;

[Collection(CurrentDirectorySensitiveCollection.Name)]
public sealed class LoginStateTests
{
    [Theory]
    [InlineData("claim.png")]
    [InlineData("close.png")]
    public void Execute_GiftDetected_AnnotatesSavedWindowAndButton(string filename)
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        using var image = ScreenshotLoader.LoadOrSkip($"DailyLogin/{filename}");
        using var pilotAvatarDetector = new PilotAvatarDetector();
        using var loggedInPilotDetector = new LoggedInPilotDetector();
        using var dailyGiftDetector = new DailyGiftDetector();
        Assert.True(dailyGiftDetector.Detect(image, out var detection));
        using var cancellation = new CancellationTokenSource();
        var inputController = new StubAutomationInputController
        {
            OnDelay = _ => cancellation.Cancel(),
        };
        var captureService = new ScreenCaptureService(new StubScreenCaptureProvider(() => image.Clone()));
        var state = new LoginState(captureService, new StubGameActionService(), inputController,
            pilotAvatarDetector, loggedInPilotDetector, dailyGiftDetector);
        var originalTelemetryRoot = UserSettings.Default.TelemetryRootBase;
        try
        {
            UserSettings.Default.TelemetryRootBase = workspace.Path;

            // Act
            Assert.Throws<OperationCanceledException>(() => state.Execute(new ProjectDiscoveryAutomationContext(2), cancellation.Token));

            // Assert
            var path = Assert.Single(Directory.GetFiles(TelemetryRootDirectory.GetCapturesDirectory(), "*.png"));
            using var annotated = Cv2.ImRead(path);
            var window = detection.WindowBounds;
            var button = (detection.ClaimButtonBounds ?? detection.CloseButtonBounds)!.Value;
            Assert.Equal(new Vec3b(80, 120, 255), annotated.At<Vec3b>(window.Y + window.Height / 2, window.X));
            Assert.Equal(new Vec3b(80, 120, 255), annotated.At<Vec3b>(button.Y + button.Height / 2, button.X));
        }
        finally
        {
            UserSettings.Default.TelemetryRootBase = originalTelemetryRoot;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Execute_GiftRemainsVisible_ReturnsRecoveryWithoutLogin(bool missingButton)
    {
        // Arrange
        using var image = ScreenshotLoader.LoadOrSkip("DailyLogin/claim.png");
        if (missingButton)
        {
            using var button = new Mat(image, new Rect(1758, 1379, 164, 56));
            button.SetTo(Scalar.Black);
        }
        using var pilotAvatarDetector = new PilotAvatarDetector();
        using var loggedInPilotDetector = new LoggedInPilotDetector();
        using var dailyGiftDetector = new DailyGiftDetector();
        var inputController = new StubAutomationInputController();
        var gameActionService = new StubGameActionService();
        var captureCount = 0;
        var captureService = new ScreenCaptureService(new StubScreenCaptureProvider(() =>
        {
            captureCount++;
            return image.Clone();
        }), persistCaptures: false);
        var state = new LoginState(captureService, gameActionService, inputController,
            pilotAvatarDetector, loggedInPilotDetector, dailyGiftDetector);

        // Act
        var transition = state.Execute(new ProjectDiscoveryAutomationContext(2), CancellationToken.None);

        // Assert
        Assert.Equal(DiscoveryAutomationStateKind.Recovery, transition.NextState);
        Assert.Equal(DiscoveryAutomationActionKind.RestartGame, transition.Action);
        Assert.Equal(missingButton ? 0 : 1, inputController.ClickCount);
        Assert.Equal(4, captureCount);
        Assert.Equal(0, gameActionService.CloseActiveWindowCallCount);
    }

    [Fact]
    public void Execute_CancelledDuringClaimWait_StopsBeforeCloseAndLogin()
    {
        // Arrange
        using var image = ScreenshotLoader.LoadOrSkip("DailyLogin/claim.png");
        using var pilotAvatarDetector = new PilotAvatarDetector();
        using var loggedInPilotDetector = new LoggedInPilotDetector();
        using var dailyGiftDetector = new DailyGiftDetector();
        using var cancellation = new CancellationTokenSource();
        var inputController = new StubAutomationInputController
        {
            OnDelay = _ => cancellation.Cancel(),
        };
        var gameActionService = new StubGameActionService();
        var captureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() => image.Clone()), persistCaptures: false);
        var state = new LoginState(captureService, gameActionService, inputController,
            pilotAvatarDetector, loggedInPilotDetector, dailyGiftDetector);

        // Act
        var exception = Record.Exception(() => state.Execute(new ProjectDiscoveryAutomationContext(2), cancellation.Token));

        // Assert
        Assert.IsType<OperationCanceledException>(exception);
        Assert.Equal(1, inputController.ClickCount);
        Assert.Equal(0, gameActionService.CloseActiveWindowCallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("claim.png")]
    [InlineData("close.png")]
    public void Execute_CurrentPilotLoginSucceeds_HandlesGiftBeforeReturningDiscover(string? giftFilename)
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        using var loginScreen = SyntheticCommonImageFactory.LoadLoginPilotSelectionScreenImage();
        using var loggedInScreen = SyntheticCommonImageFactory.LoadLoggedInPilotScreenImage();
        using var pilotAvatar = SyntheticCommonImageFactory.LoadPilotAvatarImage(2);
        using var focusedPilotAvatar = SyntheticCommonImageFactory.LoadFocusedPilotAvatarImage(2);
        using var pilotAvatarDetector = new PilotAvatarDetector();
        using var loggedInPilotDetector = new LoggedInPilotDetector();
        using var dailyGiftDetector = new DailyGiftDetector();
        using var giftScreen = giftFilename is null ? new Mat() : ScreenshotLoader.LoadOrSkip($"DailyLogin/{giftFilename}");
        using var closeScreen = ScreenshotLoader.LoadOrSkip("DailyLogin/close.png");
        var inputController = new StubAutomationInputController();
        var captureCount = 0;
        var giftCaptureCount = giftFilename == "claim.png" ? 2 : giftFilename == "close.png" ? 1 : 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() =>
            {
                captureCount++;
                if (captureCount <= giftCaptureCount)
                {
                    return captureCount == 1 ? giftScreen.Clone() : closeScreen.Clone();
                }

                return captureCount <= giftCaptureCount + 2
                    ? loginScreen.Clone()
                    : loggedInScreen.Clone();
            }),
            persistCaptures: false);
        var gameActionService = new StubGameActionService();
        var state = new LoginState(
            screenCaptureService,
            gameActionService,
            inputController,
            pilotAvatarDetector,
            loggedInPilotDetector, dailyGiftDetector);
        var context = new ProjectDiscoveryAutomationContext(2);
        var originalAvatarDirectory = UserSettings.Default.PilotAvatarDirectory;

        try
        {
            UserSettings.Default.PilotAvatarDirectory = Path.Combine(workspace.Path, "avatars");
            var pilotDirectory = AvatarsDirectory.GetDirectory();
            Directory.CreateDirectory(pilotDirectory);
            Cv2.ImWrite(Path.Combine(pilotDirectory, "2.png"), pilotAvatar);
            Cv2.ImWrite(Path.Combine(pilotDirectory, "2_focused.png"), focusedPilotAvatar);

            // Act
            var transition = state.Execute(context, CancellationToken.None);

            // Assert
            Assert.Equal(DiscoveryAutomationStateKind.Login, transition.State);
            Assert.Equal(DiscoveryAutomationStateKind.Discover, transition.NextState);
            Assert.Equal(DiscoveryAutomationActionKind.LoginPilot, transition.Action);
            Assert.Equal(2, context.CurrentPilotIndex);
            Assert.Equal(1, gameActionService.CloseActiveWindowCallCount);
            Assert.Equal(giftFilename == "claim.png" ? 2 : 1, inputController.Delays.Count(delay => delay == 5000));
            if (giftFilename is not null)
            {
                Assert.Contains(new Point(1840, 1407), inputController.MoveTargets);
                Assert.Equal(giftFilename == "claim.png" ? 3 : 2, inputController.ClickCount);
            }
            Assert.False(gameActionService.LogoutCalled);
            Assert.False(gameActionService.CloseGameClientCalled);
            Assert.False(gameActionService.QuitGameCalled);
        }
        finally
        {
            UserSettings.Default.PilotAvatarDirectory = originalAvatarDirectory;
        }
    }
}