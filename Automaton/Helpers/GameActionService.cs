using System.Diagnostics;
using System.IO;
using Automaton.Detectors;
using Automaton.Primitives;
using Serilog;

namespace Automaton.Helpers;

internal sealed class GameActionService : IGameActionService
{
    private const int HideUiTransitionDelayMs = 80;
    private const string LogoutPilotCheckCaptureSuffix = ".logout-current-pilot-check";
    private const string QuitGamePlayNowCheckCaptureSuffix = ".quit-game-play-now-check";
    private readonly ILogger m_Logger = Log.ForContext<GameActionService>();
    private readonly IAutomationInputController m_InputController;
    private readonly ScreenCaptureService m_ScreenCaptureService;
    private readonly PlayNowButtonDetector m_PlayNowButtonDetector;
    private readonly Action<CancellationToken>? m_RebootOperatingSystemOverride;

    public GameActionService(
        IAutomationInputController inputController,
        ScreenCaptureService screenCaptureService,
        PlayNowButtonDetector playNowButtonDetector)
        : this(inputController, screenCaptureService, playNowButtonDetector, null)
    {
    }

    internal GameActionService(
        IAutomationInputController inputController,
        ScreenCaptureService screenCaptureService,
        PlayNowButtonDetector playNowButtonDetector,
        Action<CancellationToken>? rebootOperatingSystemOverride)
    {
        m_InputController = inputController;
        m_ScreenCaptureService = screenCaptureService;
        m_PlayNowButtonDetector = playNowButtonDetector;
        m_RebootOperatingSystemOverride = rebootOperatingSystemOverride;
    }

    public void QuitGame(CancellationToken cancellationToken)
    {
        CloseGameClient(cancellationToken);

        var elapsedMs = 0;
        while (elapsedMs < Delays.QuitGameTimeoutMs)
        {
            m_InputController.Delay(Delays.QuitGamePollingMs, cancellationToken);
            elapsedMs += Delays.QuitGamePollingMs;

            using var capture = m_ScreenCaptureService.CaptureCurrentScreen(QuitGamePlayNowCheckCaptureSuffix);
            if (m_PlayNowButtonDetector.Detect(capture.CapturePath, out _))
            {
                m_Logger.Information(
                    "PLAY NOW detected after quit game trigger. ElapsedSeconds={ElapsedSeconds:0.###}, CapturePath={CapturePath}",
                    TimeSpan.FromMilliseconds(elapsedMs).TotalSeconds,
                    capture.CapturePath);
                return;
            }

            m_Logger.Warning(
                "PLAY NOW not detected after quit game trigger. ElapsedSeconds={ElapsedSeconds:0.###}, CapturePath={CapturePath}",
                TimeSpan.FromMilliseconds(elapsedMs).TotalSeconds,
                capture.CapturePath);
        }

        m_Logger.Error(
            "PLAY NOW was not detected before quit game timeout. Rebooting operating system. TimeoutSeconds={TimeoutSeconds:0.###}",
            TimeSpan.FromMilliseconds(Delays.QuitGameTimeoutMs).TotalSeconds);
        RebootOperatingSystem(cancellationToken);
    }

    public void CloseGameClient(CancellationToken cancellationToken)
    {
        m_Logger.Information("Close game client");
        m_InputController.PressKeyChord(VirtualKeys.Alt, VirtualKeys.Shift, VirtualKeys.Q, cancellationToken);
    }

    public void Logout(
        ScreenCaptureService screenCaptureService,
        PilotAvatarDetector pilotAvatarDetector,
        int currentPilotIndex,
        CancellationToken cancellationToken)
    {
        m_InputController.Delay(Delays.WindowActivationMs, cancellationToken);

        m_InputController.PressKeyChord(VirtualKeys.Alt, VirtualKeys.Q, cancellationToken);

        var delay = TimeSpan.FromMilliseconds(Delays.PilotLogoutDebounceMs);
        m_Logger.Information("Logging out for {Seconds} seconds", delay.TotalSeconds);
        m_InputController.Delay(delay, cancellationToken);

        var elapsedMs = 0;
        while (true)
        {
            m_InputController.Delay(Delays.PilotLogoutPollingMs, cancellationToken);
            elapsedMs += Delays.PilotLogoutPollingMs;

            // Close any window on Logon screen
            CloseActiveWindow(cancellationToken);
            m_InputController.Delay(Delays.MinimumClickMs, cancellationToken);

            using var capture = screenCaptureService.CaptureCurrentScreen(LogoutPilotCheckCaptureSuffix);
            if (pilotAvatarDetector.Detect(capture.Image, currentPilotIndex, out _))
            {
                m_Logger.Information(
                    "Current pilot detected after logout. CurrentPilotIndex={CurrentPilotIndex}, ElapsedSeconds={ElapsedSeconds:0.###}, CapturePath={CapturePath}",
                    currentPilotIndex,
                    TimeSpan.FromMilliseconds(elapsedMs).TotalSeconds,
                    capture.CapturePath);
                return;
            }

            // All logout attempts failed
            if (elapsedMs >= Delays.PilotLogoutTimeoutMs)
            {
                break;
            }

            m_Logger.Warning(
                "Current pilot not detected after logout. Retrying logout. CurrentPilotIndex={CurrentPilotIndex}, ElapsedSeconds={ElapsedSeconds:0.###}, CapturePath={CapturePath}",
                currentPilotIndex,
                TimeSpan.FromMilliseconds(elapsedMs).TotalSeconds,
                capture.CapturePath);
            // Try logging out again
            m_InputController.PressKeyChord(VirtualKeys.Alt, VirtualKeys.Q, cancellationToken);
        }

        m_Logger.Error(
            "Current pilot was not detected before logout timeout. Rebooting operating system. CurrentPilotIndex={CurrentPilotIndex}, TimeoutSeconds={TimeoutSeconds:0.###}",
            currentPilotIndex,
            TimeSpan.FromMilliseconds(Delays.PilotLogoutTimeoutMs).TotalSeconds);
        RebootOperatingSystem(cancellationToken);
    }

    public void RebootOperatingSystem(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (m_RebootOperatingSystemOverride is not null)
        {
            m_RebootOperatingSystemOverride(cancellationToken);
            return;
        }

        m_Logger.Error("Scheduling safe operating system reboot.");
        var startInfo = new ProcessStartInfo
        {
            FileName = "shutdown.exe",
            Arguments = "/r /t 30 /c \"Automaton recovery threshold exceeded\"",
            CreateNoWindow = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("Failed to schedule operating system reboot.");
        }
    }

    public void TryHideUi(string? capturePathToValidate, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(capturePathToValidate))
        {
            return;
        }

        var captureFileInfo = new FileInfo(capturePathToValidate);
        if (captureFileInfo is { Exists: true, Length: > Settings.HideUiFileSizeThreshold })
        {
            m_Logger.Information("Hiding UI because capture size exceeded {Threshold} Mb ({CaptureSizeMb} MB).",
                Settings.HideUiFileSizeThreshold / 1024 / 1024,
                captureFileInfo.Length / 1024 / 1024);
            ToggleUiVisibility(cancellationToken);
            m_InputController.Delay(Delays.HideUiMs, cancellationToken);
        }
    }

    public void CloseActiveWindow(CancellationToken cancellationToken)
    {
        m_Logger.Information("Hide active window");
        m_InputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.Q, cancellationToken);
    }

    public void ToggleProjectDiscoveryWindow(CancellationToken cancellationToken)
    {
        m_Logger.Information("Toggle Project Discovery window");
        m_InputController.PressKeyChordWithHold(
            VirtualKeys.Alt,
            VirtualKeys.L,
            Delays.ProjectDiscoveryWindowToggleChordHoldMs,
            cancellationToken);
        m_InputController.Delay(Delays.WindowActivationMs, cancellationToken);
    }

    public void ToggleFirstLaser(CancellationToken cancellationToken)
    {
        m_Logger.Information("Toggle first laser");
        m_InputController.PressKey(VirtualKeys.F1, cancellationToken);
    }

    public void ToggleSecondLaser(CancellationToken cancellationToken)
    {
        m_Logger.Information("Toggle second laser");
        m_InputController.PressKey(VirtualKeys.F2, cancellationToken);
    }

    public void TogglePropulsionModule(CancellationToken cancellationToken)
    {
        m_Logger.Information("Toggle propulsion module");
        m_InputController.PressKey(VirtualKeys.F4, cancellationToken);
    }

    public void TriggerTargetLock(CancellationToken cancellationToken)
    {
        m_Logger.Information("Trigger target lock");
        m_InputController.PressKey(VirtualKeys.Control, cancellationToken);
    }

    public void TriggerTargetApproach(CancellationToken cancellationToken)
    {
        m_Logger.Information("Trigger target approach");
        m_InputController.PressKey(VirtualKeys.A, cancellationToken);
    }

    public void WarpToTarget(CancellationToken cancellationToken)
    {
        m_Logger.Information("Warping to target");
        m_InputController.PressKey(VirtualKeys.S, cancellationToken);
    }

    public void WarpToTargetAndDock(CancellationToken cancellationToken)
    {
        m_Logger.Information("Warping to target and docking");
        m_InputController.PressKey(VirtualKeys.D, cancellationToken);
    }

    private void ToggleUiVisibility(CancellationToken cancellationToken)
    {
        m_Logger.Information("Toggle UI visibility");
        m_InputController.PressKeyChord(
            VirtualKeys.LeftControl,
            VirtualKeys.LeftShift,
            VirtualKeys.F9,
            cancellationToken,
            HideUiTransitionDelayMs);
    }
}
