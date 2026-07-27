using Automaton.Primitives;
using OpenCvSharp;
using Serilog;
using System.Runtime.InteropServices;

namespace Automaton.Helpers;

internal sealed class AutomationInputController(ClickTraceRecorder clickTraceRecorder) : IAutomationInputController
{
    private readonly ILogger m_Logger = Log.ForContext<AutomationInputController>();

    private const uint LeftDownEvent = 0x0002;
    private const uint LeftUpEvent = 0x0004;
    private const uint InputKeyboard = 1;
    private const uint KeyUpEvent = 0x0002;
    private const uint ScanCodeEvent = 0x0008;

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

    public void LeftClick(CancellationToken cancellationToken, bool recordClick = true)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Thread.Sleep(Delays.MouseDownMs);
        cancellationToken.ThrowIfCancellationRequested();
        if (recordClick)
        {
            RecordClick();
        }

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
        LogKeyboardAction("Key press", DateTime.Now, virtualKey);
        var key = CreateKeyboardKey(virtualKey);
        var keyPressed = false;

        try
        {
            SendKeyboardInput(BuildKeyDownInput(key));
            keyPressed = true;
            WaitForKeyboardTransition(cancellationToken);
        }
        finally
        {
            if (keyPressed)
            {
                SendKeyboardInput(BuildKeyUpInput(key));
            }
        }

        Thread.Sleep(Delays.MouseDownMs);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public void PressKeyChordWithHold(
        ushort modifierVirtualKey,
        ushort virtualKey,
        CancellationToken cancellationToken,
        int holdDelayMs = Delays.KeyChordHoldMs,
        int transitionDelayMs = Delays.KeyChordTransitionMs)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LogKeyboardAction("Held key chord", DateTime.Now, modifierVirtualKey, virtualKey);
        PressKeyChordCore(cancellationToken, transitionDelayMs, holdDelayMs, modifierVirtualKey, virtualKey);
    }

    public void PressKeyChordWithHold(
        ushort firstModifier,
        ushort secondModifier,
        ushort virtualKey,
        CancellationToken cancellationToken,
        int holdDelayMs = Delays.KeyChordHoldMs,
        int transitionDelayMs = Delays.KeyChordTransitionMs)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LogKeyboardAction("Held key chord", DateTime.Now, firstModifier, secondModifier, virtualKey);
        PressKeyChordCore(cancellationToken, transitionDelayMs, holdDelayMs, firstModifier, secondModifier, virtualKey);
    }

    private void PressKeyChordCore(CancellationToken cancellationToken, int transitionDelayMs, int holdDelayMs, params ushort[] virtualKeys)
    {
        var keys = virtualKeys.Select(CreateKeyboardKey).ToArray();
        var pressedKeys = new Stack<KeyboardKey>();

        try
        {
            for (var index = 0; index < keys.Length - 1; index++)
            {
                SendKeyboardInput(BuildKeyDownInput(keys[index]));
                pressedKeys.Push(keys[index]);
                WaitForKeyboardTransition(cancellationToken, transitionDelayMs);
            }

            var primaryKey = keys[^1];
            SendKeyboardInput(BuildKeyDownInput(primaryKey));
            pressedKeys.Push(primaryKey);
            WaitForKeyboardTransition(cancellationToken, holdDelayMs);

            SendKeyboardInput(BuildKeyUpInput(primaryKey));
            pressedKeys.Pop();
            WaitForKeyboardTransition(cancellationToken, transitionDelayMs);
        }
        finally
        {
            while (pressedKeys.Count > 0)
            {
                SendKeyboardInput(BuildKeyUpInput(pressedKeys.Pop()));
            }
        }

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

    private static KeyboardKey CreateKeyboardKey(ushort virtualKey)
    {
        return new KeyboardKey(virtualKey, MapScanCode(virtualKey));
    }

    private static ushort MapScanCode(ushort virtualKey)
    {
        return (ushort)MapVirtualKey(virtualKey, 0);
    }

    private static INPUT BuildKeyDownInput(KeyboardKey key)
    {
        var virtualKey = key.VirtualKey;
        var scanCode = key.ScanCode;

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

    private static INPUT BuildKeyUpInput(KeyboardKey key)
    {
        var virtualKey = key.VirtualKey;
        var scanCode = key.ScanCode;

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

    private static void WaitForKeyboardTransition(CancellationToken cancellationToken, int delayMs = Delays.KeyChordTransitionMs)
    {
        cancellationToken.WaitHandle.WaitOne(delayMs);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void SendKeyboardInput(INPUT input)
    {
        var inputs = new[] { input };
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent == (uint)inputs.Length)
        {
            return;
        }

        var lastWin32Error = Marshal.GetLastWin32Error();
        m_Logger.Warning(
            "SendInput sent {SentInputCount}/{RequestedInputCount} keyboard events. LastWin32Error={LastWin32Error}. Falling back for remaining keyboard events.",
            sent,
            inputs.Length,
            lastWin32Error);

        SendLegacyKeyboardEvent(input);
    }

    private static void SendLegacyKeyboardEvent(INPUT input)
    {
        var keyboard = input.U.ki;
        var isKeyUp = (keyboard.dwFlags & KeyUpEvent) == KeyUpEvent;

        if ((keyboard.dwFlags & ScanCodeEvent) == ScanCodeEvent)
        {
            var flags = ScanCodeEvent | (isKeyUp ? KeyUpEvent : 0);
            keybd_event(0, (byte)keyboard.wScan, flags, UIntPtr.Zero);
            return;
        }

        if (keyboard.wVk != 0)
        {
            keybd_event((byte)keyboard.wVk, 0, isKeyUp ? KeyUpEvent : 0, UIntPtr.Zero);
            return;
        }

        var virtualKey = (byte)MapVirtualKey(keyboard.wScan, 3);
        keybd_event(virtualKey, 0, isKeyUp ? KeyUpEvent : 0, UIntPtr.Zero);
    }

    private void RecordClick()
    {
        if (GetCursorPos(out var cursorPosition))
        {
            var timestampLocal = DateTime.Now;
            var point = new Point(cursorPosition.X, cursorPosition.Y);
            m_Logger.Information(
                "Left click. X={X}, Y={Y}, TimestampLocal={TimestampLocal:O}",
                point.X,
                point.Y,
                timestampLocal);
            clickTraceRecorder.RecordClick(point, timestampLocal);
            return;
        }

        m_Logger.Warning("GetCursorPos failed before left click.");
    }

    private void LogKeyboardAction(string action, DateTime timestampLocal, params ushort[] virtualKeys)
    {
        m_Logger.Information(
            "{Action}. Keys={Keys}, TimestampLocal={TimestampLocal:O}",
            action,
            string.Join("+", virtualKeys.Select(FormatVirtualKey)),
            timestampLocal);
    }

    private static string FormatVirtualKey(ushort virtualKey)
    {
        var name = virtualKey switch
        {
            VirtualKeys.A => nameof(VirtualKeys.A),
            VirtualKeys.C => nameof(VirtualKeys.C),
            VirtualKeys.D => nameof(VirtualKeys.D),
            VirtualKeys.G => nameof(VirtualKeys.G),
            VirtualKeys.L => nameof(VirtualKeys.L),
            VirtualKeys.M => nameof(VirtualKeys.M),
            VirtualKeys.Q => nameof(VirtualKeys.Q),
            VirtualKeys.S => nameof(VirtualKeys.S),
            VirtualKeys.V => nameof(VirtualKeys.V),
            VirtualKeys.W => nameof(VirtualKeys.W),
            VirtualKeys.X => nameof(VirtualKeys.X),
            VirtualKeys.Enter => nameof(VirtualKeys.Enter),
            VirtualKeys.Shift => nameof(VirtualKeys.Shift),
            VirtualKeys.Control => nameof(VirtualKeys.Control),
            VirtualKeys.Alt => nameof(VirtualKeys.Alt),
            VirtualKeys.LeftShift => nameof(VirtualKeys.LeftShift),
            VirtualKeys.LeftControl => nameof(VirtualKeys.LeftControl),
            VirtualKeys.F1 => nameof(VirtualKeys.F1),
            VirtualKeys.F2 => nameof(VirtualKeys.F2),
            VirtualKeys.F4 => nameof(VirtualKeys.F4),
            VirtualKeys.F9 => nameof(VirtualKeys.F9),
            _ => null
        };

        return name is null
            ? $"0x{virtualKey:X4}"
            : $"{name}(0x{virtualKey:X4})";
    }

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out CursorPoint point);

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
        public MOUSEINPUT mi;

        [FieldOffset(0)]
        public KEYBDINPUT ki;

        [FieldOffset(0)]
        public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    private readonly record struct KeyboardKey(ushort VirtualKey, ushort ScanCode);

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorPoint
    {
        public int X;
        public int Y;
    }
}
