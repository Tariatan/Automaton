using Automaton.Primitives;
using OpenCvSharp;
using Serilog;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Automaton.Helpers;

internal sealed class AutomationInputController : IAutomationInputController
{
    private readonly ILogger m_Logger = Log.ForContext<AutomationInputController>();

    private const uint LeftDownEvent = 0x0002;
    private const uint LeftUpEvent = 0x0004;
    private const uint InputKeyboard = 1;
    private const uint KeyUpEvent = 0x0002;

    public void ClickUiElement(Point point, CancellationToken cancellationToken)
    {
        MoveTo(point);
        LeftClick(cancellationToken);
    }

    public void TryHideUi(string? capturePathToValidate, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(capturePathToValidate))
        {
            return;
        }

        var captureFileInfo = new FileInfo(capturePathToValidate);
        // Hide UI if captured file size is more than 1Mb
        if (captureFileInfo is { Exists: true, Length: > 1024 * 1024 })
        {
            m_Logger.Information("Hiding UI because capture size exceeded 1 MB ({CaptureSizeMb} MB).", captureFileInfo.Length / 1024 / 1024);
            PressKeyChord(VirtualKeys.Control, VirtualKeys.Shift, VirtualKeys.F9, cancellationToken);
            Delay(Delays.HideUiMs, cancellationToken);
        }
    }

    public void MoveTo(Point point)
    {
        _ = SetCursorPos(point.X, point.Y);
    }

    public void LeftClick(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Thread.Sleep(Delays.MouseDownMs);
        cancellationToken.ThrowIfCancellationRequested();
        var leftButtonPressed = false;

        try
        {
            mouse_event(LeftDownEvent, 0, 0, 0, UIntPtr.Zero);
            leftButtonPressed = true;
            Thread.Sleep(Delays.MouseDownMs);
        }
        finally
        {
            if (leftButtonPressed)
            {
                mouse_event(LeftUpEvent, 0, 0, 0, UIntPtr.Zero);
                Thread.Sleep(Delays.MouseDownMs);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    public void PressKey(ushort virtualKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SendKeyboardInputs(
            BuildKeyDownInput(virtualKey),
            BuildKeyUpInput(virtualKey));
        Thread.Sleep(Delays.MouseDownMs);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public void PressKeyChord(ushort modifierVirtualKey, ushort virtualKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SendKeyboardInputs(
            BuildKeyDownInput(modifierVirtualKey),
            BuildKeyDownInput(virtualKey),
            BuildKeyUpInput(virtualKey),
            BuildKeyUpInput(modifierVirtualKey));
        Thread.Sleep(Delays.MouseDownMs);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void PressKeyChord(
        ushort firstModifierVirtualKey,
        ushort secondModifierVirtualKey,
        ushort virtualKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SendKeyboardInputs(
            BuildKeyDownInput(firstModifierVirtualKey),
            BuildKeyDownInput(secondModifierVirtualKey),
            BuildKeyDownInput(virtualKey),
            BuildKeyUpInput(virtualKey),
            BuildKeyUpInput(secondModifierVirtualKey),
            BuildKeyUpInput(firstModifierVirtualKey));
        Thread.Sleep(Delays.MouseDownMs);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public void QuitGame(CancellationToken cancellationToken)
    {
        PressKeyChord(VirtualKeys.Alt, VirtualKeys.Shift, VirtualKeys.Q, cancellationToken);
        Delay(Delays.QuitGameConfirmMs, cancellationToken);
        PressKey(VirtualKeys.Enter, cancellationToken);
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

    public void Logout(CancellationToken cancellationToken)
    {
        Delay(Delays.WindowActivationMs, cancellationToken);

        // Activate Logout window
        PressKeyChord(VirtualKeys.Alt, VirtualKeys.Q, cancellationToken);
        Delay(Delays.WindowActivationMs, cancellationToken);

        // Confirm Logout
        PressKey(VirtualKeys.Enter, cancellationToken);

        var delay = TimeSpan.FromMilliseconds(Delays.PilotLogoutMs);
        m_Logger.Information("Logging out for {Seconds} seconds", delay.TotalSeconds);
        // Wait for full logout
        Delay(delay, cancellationToken);

        // Close any window on login screen
        PressKeyChord(VirtualKeys.Control, VirtualKeys.W, cancellationToken);
    }

    public void Delay(TimeSpan milliseconds, CancellationToken cancellationToken)
    {
        Delay((int)milliseconds.TotalMilliseconds, cancellationToken);
    }

    public void Delay(int milliseconds, CancellationToken cancellationToken)
    {
        cancellationToken.WaitHandle.WaitOne(milliseconds);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static INPUT BuildKeyDownInput(ushort virtualKey)
    {
        return new INPUT
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = virtualKey
                }
            }
        };
    }

    private static INPUT BuildKeyUpInput(ushort virtualKey)
    {
        return new INPUT
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = virtualKey,
                    dwFlags = KeyUpEvent
                }
            }
        };
    }

    private static void SendKeyboardInputs(params INPUT[] inputs)
    {
        if (inputs.Length == 0)
        {
            return;
        }

        _ = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }
}
