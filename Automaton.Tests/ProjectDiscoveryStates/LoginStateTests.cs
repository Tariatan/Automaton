using Automaton.Core.Detectors;
using Automaton.Core.Helpers;
using Automaton.Core.Infrastructure;
using Automaton.ProjectDiscoveryStates;
using Automaton.Tests.Stubs;
using OpenCvSharp;

namespace Automaton.Tests.ProjectDiscoveryStates;

[Collection(CurrentDirectorySensitiveCollection.Name)]
public sealed class LoginStateTests
{
    [Fact]
    public void Execute_CurrentPilotLoginSucceeds_ReturnsDiscover()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        using var loginScreen = SyntheticCommonImageFactory.LoadLoginPilotSelectionScreenImage();
        using var loggedInScreen = SyntheticCommonImageFactory.LoadLoggedInPilotScreenImage();
        using var pilotAvatar = SyntheticCommonImageFactory.LoadPilotAvatarImage(2);
        using var focusedPilotAvatar = SyntheticCommonImageFactory.LoadFocusedPilotAvatarImage(2);
        using var pilotAvatarDetector = new PilotAvatarDetector();
        using var loggedInPilotDetector = new LoggedInPilotDetector();
        var captureCount = 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() =>
            {
                captureCount++;
                return captureCount == 1
                    ? loginScreen.Clone()
                    : loggedInScreen.Clone();
            }),
            persistCaptures: false);
        var gameActionService = new StubGameActionService();
        var state = new LoginState(
            screenCaptureService,
            gameActionService,
            new StubAutomationInputController(),
            pilotAvatarDetector,
            loggedInPilotDetector);
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
