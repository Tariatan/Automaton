using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using Automaton.Tests.Stubs;
using OpenCvSharp;

namespace Automaton.Tests.Helpers;

public sealed class GameActionServiceTests
{
    [Fact]
    public void Logout_CurrentPilotDetected_ReturnsWithoutQuitGame()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        WritePilotAvatarTemplates(Path.Combine(workspace.Path, "pilot"), 1);
        using var pilotScreen = SyntheticCommonImageFactory.LoadLoginPilotSelectionScreenImage();
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(pilotScreen.Clone),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var gameActionService = CreateGameActionService(automationInputController, screenCaptureService);
        using var pilotAvatarDetector = new PilotAvatarDetector();

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            gameActionService.Logout(screenCaptureService, pilotAvatarDetector, 1, CancellationToken.None);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.Equal([Delays.WindowActivationMs,
            Delays.PilotLogoutDebounceMs,
            Delays.PilotLogoutPollingMs,
            Delays.MinimumClickMs], automationInputController.Delays);
        Assert.Equal(1, CountKeyChord(automationInputController, VirtualKeys.Alt, VirtualKeys.Q));
        Assert.Equal(0, CountKeyChord(automationInputController, VirtualKeys.Alt, VirtualKeys.Shift, VirtualKeys.Q));
    }

    [Fact]
    public void Logout_CurrentPilotMissingUntilTimeout_Reboots()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        WritePilotAvatarTemplates(Path.Combine(workspace.Path, "pilot"), 1);
        using var blankScreen = new Mat(new Size(900, 640), MatType.CV_8UC3, Scalar.Black);
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(blankScreen.Clone),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var rebootOperatingSystemCalled = false;
        var gameActionService = CreateGameActionService(
            automationInputController,
            screenCaptureService,
            _ => rebootOperatingSystemCalled = true);
        using var pilotAvatarDetector = new PilotAvatarDetector();

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            gameActionService.Logout(screenCaptureService, pilotAvatarDetector, 1, CancellationToken.None);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        var retryCount = Delays.PilotLogoutTimeoutMs / Delays.PilotLogoutPollingMs;
        Assert.Equal(retryCount, CountKeyChord(automationInputController, VirtualKeys.Alt, VirtualKeys.Q));
        Assert.Equal(retryCount, automationInputController.Delays.Skip(2).Count(delay => delay == Delays.PilotLogoutPollingMs));
        Assert.True(rebootOperatingSystemCalled);
    }

    [Fact]
    public void QuitGame_PlayNowDetected_ReturnsWithoutReboot()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        using var playButtonScreen = SyntheticCommonImageFactory.LoadPlayButtonScreenImage();
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(playButtonScreen.Clone),
            new SampleImageProcessor());
        var automationInputController = new StubAutomationInputController();
        var rebootOperatingSystemCalled = false;
        var gameActionService = CreateGameActionService(
            automationInputController,
            screenCaptureService,
            _ => rebootOperatingSystemCalled = true);

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            gameActionService.QuitGame(CancellationToken.None);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.Equal([Delays.QuitGamePollingMs], automationInputController.Delays);
        Assert.Equal(1, CountKeyChord(automationInputController, VirtualKeys.Alt, VirtualKeys.Shift, VirtualKeys.Q));
        Assert.False(rebootOperatingSystemCalled);
    }

    [Fact]
    public void CloseGameClient_WhenCalled_TriggersQuitShortcutWithoutRecoveryWait()
    {
        // Arrange
        using var blankScreen = new Mat(new Size(900, 640), MatType.CV_8UC3, Scalar.Black);
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(blankScreen.Clone),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var rebootOperatingSystemCalled = false;
        var gameActionService = CreateGameActionService(
            automationInputController,
            screenCaptureService,
            _ => rebootOperatingSystemCalled = true);

        // Act
        gameActionService.CloseGameClient(CancellationToken.None);

        // Assert
        Assert.Empty(automationInputController.Delays);
        Assert.Equal(1, CountKeyChord(automationInputController, VirtualKeys.Alt, VirtualKeys.Shift, VirtualKeys.Q));
        Assert.False(rebootOperatingSystemCalled);
    }

    [Fact]
    public void QuitGame_PlayNowMissingUntilTimeout_RebootsOperatingSystem()
    {
        // Arrange
        using var blankScreen = new Mat(new Size(900, 640), MatType.CV_8UC3, Scalar.Black);
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(blankScreen.Clone),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var rebootOperatingSystemCalled = false;
        var gameActionService = CreateGameActionService(
            automationInputController,
            screenCaptureService,
            _ => rebootOperatingSystemCalled = true);

        // Act
        gameActionService.QuitGame(CancellationToken.None);

        // Assert
        var retryCount = Delays.QuitGameTimeoutMs / Delays.QuitGamePollingMs;
        Assert.Equal(retryCount, automationInputController.Delays.Count(delay => delay == Delays.QuitGamePollingMs));
        Assert.Equal(1, CountKeyChord(automationInputController, VirtualKeys.Alt, VirtualKeys.Shift, VirtualKeys.Q));
        Assert.True(rebootOperatingSystemCalled);
    }

    [Fact]
    public void ToggleProjectDiscoveryWindow_WhenCalled_HoldsAltLChordBeforeActivationDelay()
    {
        // Arrange
        using var blankScreen = new Mat(new Size(900, 640), MatType.CV_8UC3, Scalar.Black);
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(blankScreen.Clone),
            new SampleImageProcessor(),
            persistCaptures: false);
        var automationInputController = new StubAutomationInputController();
        var gameActionService = CreateGameActionService(automationInputController, screenCaptureService);

        // Act
        gameActionService.ToggleProjectDiscoveryWindow(CancellationToken.None);

        // Assert
        var keyInput = Assert.Single(automationInputController.KeyInputs);
        Assert.Equal(VirtualKeys.Alt, keyInput.ModifierVirtualKey);
        Assert.Null(keyInput.SecondModifierVirtualKey);
        Assert.Equal(VirtualKeys.L, keyInput.VirtualKey);
        Assert.Equal(Delays.KeyChordTransitionMs, keyInput.TransitionDelayMs);
        Assert.Equal(Delays.ProjectDiscoveryWindowToggleChordHoldMs, keyInput.HoldDelayMs);
        Assert.Equal([Delays.WindowActivationMs], automationInputController.Delays);
    }

    private static int CountKeyChord(StubAutomationInputController automationInputController, ushort modifier, ushort virtualKey)
    {
        return automationInputController.KeyInputs.Count(input =>
            input.ModifierVirtualKey == modifier &&
            input.SecondModifierVirtualKey is null &&
            input.VirtualKey == virtualKey);
    }

    private static int CountKeyChord(StubAutomationInputController automationInputController, ushort firstModifier, ushort secondModifier, ushort virtualKey)
    {
        return automationInputController.KeyInputs.Count(input =>
            input.ModifierVirtualKey == firstModifier &&
            input.SecondModifierVirtualKey == secondModifier &&
            input.VirtualKey == virtualKey);
    }

    private static GameActionService CreateGameActionService(
        StubAutomationInputController automationInputController,
        ScreenCaptureService screenCaptureService,
        Action<CancellationToken>? rebootOperatingSystemOverride = null)
    {
        return new GameActionService(
            automationInputController,
            screenCaptureService,
            new PlayNowButtonDetector(),
            rebootOperatingSystemOverride);
    }

    private static void WritePilotAvatarTemplates(string pilotDirectory, int pilotIndex)
    {
        Directory.CreateDirectory(pilotDirectory);
        using var avatar = SyntheticCommonImageFactory.LoadPilotAvatarImage(pilotIndex);
        using var focusedAvatar = SyntheticCommonImageFactory.LoadFocusedPilotAvatarImage(pilotIndex);
        Cv2.ImWrite(Path.Combine(pilotDirectory, $"{pilotIndex}.png"), avatar);
        Cv2.ImWrite(Path.Combine(pilotDirectory, $"{pilotIndex}_focused.png"), focusedAvatar);
    }
}
