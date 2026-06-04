using System.Diagnostics;
using System.IO;
using Automaton.Primitives;
using OpenCvSharp;
using Serilog;

namespace Automaton.Helpers;

internal sealed class GameActionService(IAutomationInputController inputController) : IGameActionService
{
    private const int HideUiTransitionDelayMs = 80;
    private readonly ILogger m_Logger = Log.ForContext<GameActionService>();

    public void QuitGame(CancellationToken cancellationToken)
    {
        // Ensure no window/popup is blocking Quit Game request
        CloseActiveWindow(cancellationToken);
        CloseActiveWindow(cancellationToken);
        inputController.PressKey(VirtualKeys.Enter, cancellationToken);
        inputController.PressKey(VirtualKeys.Enter, cancellationToken);


        inputController.PressKeyChord(VirtualKeys.Alt, VirtualKeys.Shift, VirtualKeys.Q, cancellationToken);
        inputController.Delay(Delays.QuitGameConfirmMs, cancellationToken);
        inputController.PressKey(VirtualKeys.Enter, cancellationToken);
    }

    public void Logout(CancellationToken cancellationToken)
    {
        inputController.Delay(Delays.WindowActivationMs, cancellationToken);

        inputController.PressKeyChord(VirtualKeys.Alt, VirtualKeys.Q, cancellationToken);
        inputController.Delay(Delays.WindowActivationMs, cancellationToken);

        inputController.PressKey(VirtualKeys.Enter, cancellationToken);

        var delay = TimeSpan.FromMilliseconds(Delays.PilotLogoutMs);
        m_Logger.Information("Logging out for {Seconds} seconds", delay.TotalSeconds);
        inputController.Delay(delay, cancellationToken);

        CloseActiveWindow(cancellationToken);
    }

    public void Login(int pilotIndex, Point activationPoint, CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromMilliseconds(Delays.PilotLoginMs);
        m_Logger.Information("Logging in pilot {PilotIndex} for {DelaySeconds:0.###} seconds...", pilotIndex, delay.TotalSeconds);
        inputController.MoveTo(activationPoint);
        inputController.LeftClick(cancellationToken);
        inputController.Delay(delay, cancellationToken);

        CloseActiveWindow(cancellationToken);
        inputController.Delay(Delays.MinimumClickMs, cancellationToken);
    }

    public void RebootOperatingSystem(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
            inputController.Delay(Delays.HideUiMs, cancellationToken);
        }
    }

    public void CloseActiveWindow(CancellationToken cancellationToken)
    {
        m_Logger.Information("Hide active window");
        inputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.Q, cancellationToken);
    }

    public void ToggleProjectDiscoveryWindow(CancellationToken cancellationToken)
    {
        m_Logger.Information("Toggle Project Discovery window");
        inputController.PressKeyChord(VirtualKeys.Alt, VirtualKeys.L, cancellationToken);
    }

    public void ToggleFirstLaser(CancellationToken cancellationToken)
    {
        m_Logger.Information("Toggle first laser");
        inputController.PressKey(VirtualKeys.F1, cancellationToken);
    }

    public void ToggleSecondLaser(CancellationToken cancellationToken)
    {
        m_Logger.Information("Toggle second laser");
        inputController.PressKey(VirtualKeys.F2, cancellationToken);
    }

    public void TogglePropulsionModule(CancellationToken cancellationToken)
    {
        m_Logger.Information("Toggle propulsion module");
        inputController.PressKey(VirtualKeys.F4, cancellationToken);
    }

    public void TriggerTargetLock(CancellationToken cancellationToken)
    {
        m_Logger.Information("Trigger target lock");
        inputController.PressKey(VirtualKeys.Control, cancellationToken);
    }

    public void TriggerTargetApproach(CancellationToken cancellationToken)
    {
        m_Logger.Information("Trigger target approach");
        inputController.PressKey(VirtualKeys.A, cancellationToken);
    }

    public void WarpToTarget(CancellationToken cancellationToken)
    {
        m_Logger.Information("Warping to target");
        inputController.PressKey(VirtualKeys.S, cancellationToken);
    }

    public void WarpToTargetAndDock(CancellationToken cancellationToken)
    {
        m_Logger.Information("Warping to target and docking");
        inputController.PressKey(VirtualKeys.D, cancellationToken);
    }

    private void ToggleUiVisibility(CancellationToken cancellationToken)
    {
        m_Logger.Information("Toggle UI visibility");
        inputController.PressKeyChord(
            VirtualKeys.LeftControl,
            VirtualKeys.LeftShift,
            VirtualKeys.F9,
            cancellationToken,
            HideUiTransitionDelayMs);
    }
}
