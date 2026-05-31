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
    private const uint ScanCodeEvent = 0x0008;
    private const int KeyboardTransitionDelayMs = 30;
    private const int HideUiTransitionDelayMs = 80;

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
            PressHideUiChord(cancellationToken);
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
        SendKeyboardInput(BuildKeyDownInput(virtualKey));
        WaitForKeyboardTransition(cancellationToken);
        SendKeyboardInput(BuildKeyUpInput(virtualKey));
        Thread.Sleep(Delays.MouseDownMs);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public void PressKeyChord(ushort modifierVirtualKey, ushort virtualKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SendKeyboardInput(BuildKeyDownInput(modifierVirtualKey));
        WaitForKeyboardTransition(cancellationToken);
        SendKeyboardInput(BuildKeyDownInput(virtualKey));
        WaitForKeyboardTransition(cancellationToken);
        SendKeyboardInput(BuildKeyUpInput(virtualKey));
        WaitForKeyboardTransition(cancellationToken);
        SendKeyboardInput(BuildKeyUpInput(modifierVirtualKey));
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
        SendKeyboardInput(BuildKeyDownInput(firstModifierVirtualKey));
        WaitForKeyboardTransition(cancellationToken);
        SendKeyboardInput(BuildKeyDownInput(secondModifierVirtualKey));
        WaitForKeyboardTransition(cancellationToken);
        SendKeyboardInput(BuildKeyDownInput(virtualKey));
        WaitForKeyboardTransition(cancellationToken);
        SendKeyboardInput(BuildKeyUpInput(virtualKey));
        WaitForKeyboardTransition(cancellationToken);
        SendKeyboardInput(BuildKeyUpInput(secondModifierVirtualKey));
        WaitForKeyboardTransition(cancellationToken);
        SendKeyboardInput(BuildKeyUpInput(firstModifierVirtualKey));
        Thread.Sleep(Delays.MouseDownMs);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void PressHideUiChord(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SendKeyboardInput(BuildKeyDownInput(VirtualKeys.LeftControl));
        WaitForKeyboardTransition(cancellationToken, HideUiTransitionDelayMs);
        SendKeyboardInput(BuildKeyDownInput(VirtualKeys.LeftShift));
        WaitForKeyboardTransition(cancellationToken, HideUiTransitionDelayMs);
        SendKeyboardInput(BuildKeyDownInput(VirtualKeys.F9));
        WaitForKeyboardTransition(cancellationToken, HideUiTransitionDelayMs);
        SendKeyboardInput(BuildKeyUpInput(VirtualKeys.F9));
        WaitForKeyboardTransition(cancellationToken, HideUiTransitionDelayMs);
        SendKeyboardInput(BuildKeyUpInput(VirtualKeys.LeftShift));
        WaitForKeyboardTransition(cancellationToken, HideUiTransitionDelayMs);
        SendKeyboardInput(BuildKeyUpInput(VirtualKeys.LeftControl));

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
        var scanCode = (ushort)MapVirtualKey(virtualKey, 0);
        if (scanCode == 0)
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

        return new INPUT
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = scanCode,
                    dwFlags = ScanCodeEvent
                }
            }
        };
    }

    private static INPUT BuildKeyUpInput(ushort virtualKey)
    {
        var scanCode = (ushort)MapVirtualKey(virtualKey, 0);
        if (scanCode == 0)
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

        return new INPUT
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = scanCode,
                    dwFlags = ScanCodeEvent | KeyUpEvent
                }
            }
        };
    }

    private static void WaitForKeyboardTransition(CancellationToken cancellationToken, int delayMs = KeyboardTransitionDelayMs)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Thread.Sleep(delayMs);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void SendKeyboardInput(INPUT input)
    {
        var inputs = new[] { input };
        var sent = SendInput(1, inputs, Marshal.SizeOf<INPUT>());
        if (sent == 1)
        {
            return;
        }

        SendLegacyKeyboardEvent(input);
    }

    private static void SendLegacyKeyboardEvent(INPUT input)
    {
        var keyboard = input.U.ki;
        var isKeyUp = (keyboard.dwFlags & KeyUpEvent) == KeyUpEvent;

        if (keyboard.wVk != 0)
        {
            keybd_event((byte)keyboard.wVk, 0, isKeyUp ? KeyUpEvent : 0, UIntPtr.Zero);
            return;
        }

        var virtualKey = (byte)MapVirtualKey(keyboard.wScan, 3);
        keybd_event(virtualKey, 0, isKeyUp ? KeyUpEvent : 0, UIntPtr.Zero);
    }

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

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
