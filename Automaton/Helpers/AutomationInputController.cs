using Automaton.Primitives;
using OpenCvSharp;
using Serilog;
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
    private const int DefaultTransitionDelayMs = 50;

    public void ClickUiElement(Point point, CancellationToken cancellationToken)
    {
        MoveTo(point);
        LeftClick(cancellationToken);
    }

    public void MoveTo(Point point)
    {
        if (!SetCursorPos(point.X, point.Y))
        {
            m_Logger.Warning("SetCursorPos failed for ({X}, {Y})", point.X, point.Y);
        }
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
        var scanCode = MapScanCode(virtualKey);
        SendKeyboardInput(BuildKeyDownInput(virtualKey, scanCode));
        WaitForKeyboardTransition(cancellationToken);
        SendKeyboardInput(BuildKeyUpInput(virtualKey, scanCode));
        Thread.Sleep(Delays.MouseDownMs);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public void PressKeyChord(ushort modifierVirtualKey, ushort virtualKey, CancellationToken cancellationToken, int transitionDelayMs = DefaultTransitionDelayMs)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var modScan = MapScanCode(modifierVirtualKey);
        var keyScan = MapScanCode(virtualKey);
        SendKeyboardInput(BuildKeyDownInput(modifierVirtualKey, modScan));
        WaitForKeyboardTransition(cancellationToken, transitionDelayMs);
        SendKeyboardInput(BuildKeyDownInput(virtualKey, keyScan));
        WaitForKeyboardTransition(cancellationToken, transitionDelayMs);
        SendKeyboardInput(BuildKeyUpInput(virtualKey, keyScan));
        WaitForKeyboardTransition(cancellationToken, transitionDelayMs);
        SendKeyboardInput(BuildKeyUpInput(modifierVirtualKey, modScan));
        Thread.Sleep(Delays.MouseDownMs);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public void PressKeyChord(
        ushort firstModifier,
        ushort secondModifier,
        ushort virtualKey,
        CancellationToken cancellationToken,
        int transitionDelayMs = DefaultTransitionDelayMs)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var firstScan = MapScanCode(firstModifier);
        var secondScan = MapScanCode(secondModifier);
        var keyScan = MapScanCode(virtualKey);
        SendKeyboardInput(BuildKeyDownInput(firstModifier, firstScan));
        WaitForKeyboardTransition(cancellationToken, transitionDelayMs);
        SendKeyboardInput(BuildKeyDownInput(secondModifier, secondScan));
        WaitForKeyboardTransition(cancellationToken, transitionDelayMs);
        SendKeyboardInput(BuildKeyDownInput(virtualKey, keyScan));
        WaitForKeyboardTransition(cancellationToken, transitionDelayMs);
        SendKeyboardInput(BuildKeyUpInput(virtualKey, keyScan));
        WaitForKeyboardTransition(cancellationToken, transitionDelayMs);
        SendKeyboardInput(BuildKeyUpInput(secondModifier, secondScan));
        WaitForKeyboardTransition(cancellationToken, transitionDelayMs);
        SendKeyboardInput(BuildKeyUpInput(firstModifier, firstScan));
        Thread.Sleep(Delays.MouseDownMs);
        cancellationToken.ThrowIfCancellationRequested();
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

    private static ushort MapScanCode(ushort virtualKey)
    {
        return (ushort)MapVirtualKey(virtualKey, 0);
    }

    private static INPUT BuildKeyDownInput(ushort virtualKey, ushort scanCode)
    {
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

    private static INPUT BuildKeyUpInput(ushort virtualKey, ushort scanCode)
    {
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

    private static void WaitForKeyboardTransition(CancellationToken cancellationToken, int delayMs = DefaultTransitionDelayMs)
    {
        cancellationToken.WaitHandle.WaitOne(delayMs);
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
