using System.Diagnostics;
using System.IO;
using Automaton.Primitives;
using Serilog;

namespace Automaton.Helpers;

internal sealed class GameActionService(IAutomationInputController inputController) : IGameActionService
{
    private const int HideUiTransitionDelayMs = 80;
    private readonly ILogger m_Logger = Log.ForContext<GameActionService>();

    public void QuitGame(CancellationToken cancellationToken)
    {
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

        inputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.W, cancellationToken);
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
            PressHideUiChord(cancellationToken);
            inputController.Delay(Delays.HideUiMs, cancellationToken);
        }
    }

    private void PressHideUiChord(CancellationToken cancellationToken)
    {
        inputController.PressKeyChord(
            VirtualKeys.LeftControl,
            VirtualKeys.LeftShift,
            VirtualKeys.F9,
            cancellationToken,
            HideUiTransitionDelayMs);
    }
}
