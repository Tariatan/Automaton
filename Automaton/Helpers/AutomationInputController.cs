using System.Runtime.InteropServices;
using Automaton.Primitives;
using OpenCvSharp;

namespace Automaton.Helpers;

internal sealed partial class AutomationInputController : IAutomationInputController
{
    private const uint LeftDownEvent = 0x0002;
    private const uint LeftUpEvent = 0x0004;
    private const uint KeyUpEvent = 0x0002;

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
        keybd_event((byte)virtualKey, 0, 0, UIntPtr.Zero);
        Thread.Sleep(Delays.MouseDownMs);
        keybd_event((byte)virtualKey, 0, KeyUpEvent, UIntPtr.Zero);
        Thread.Sleep(Delays.MouseDownMs);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public void PressKeyChord(ushort modifierVirtualKey, ushort virtualKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        keybd_event((byte)modifierVirtualKey, 0, 0, UIntPtr.Zero);

        try
        {
            keybd_event((byte)virtualKey, 0, 0, UIntPtr.Zero);
            Thread.Sleep(Delays.MouseDownMs);
            keybd_event((byte)virtualKey, 0, KeyUpEvent, UIntPtr.Zero);
        }
        finally
        {
            keybd_event((byte)modifierVirtualKey, 0, KeyUpEvent, UIntPtr.Zero);
            Thread.Sleep(Delays.MouseDownMs);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    public void PressKeyChord(
        ushort firstModifierVirtualKey,
        ushort secondModifierVirtualKey,
        ushort virtualKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        keybd_event((byte)firstModifierVirtualKey, 0, 0, UIntPtr.Zero);
        keybd_event((byte)secondModifierVirtualKey, 0, 0, UIntPtr.Zero);

        try
        {
            keybd_event((byte)virtualKey, 0, 0, UIntPtr.Zero);
            Thread.Sleep(Delays.MouseDownMs);
            keybd_event((byte)virtualKey, 0, KeyUpEvent, UIntPtr.Zero);
        }
        finally
        {
            keybd_event((byte)secondModifierVirtualKey, 0, KeyUpEvent, UIntPtr.Zero);
            keybd_event((byte)firstModifierVirtualKey, 0, KeyUpEvent, UIntPtr.Zero);
            Thread.Sleep(Delays.MouseDownMs);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    public void QuitGame(CancellationToken cancellationToken)
    {
        PressKeyChord(VirtualKeys.Alt, VirtualKeys.Shift, VirtualKeys.Q, cancellationToken);
        Delay(Delays.QuitGameConfirmMs, cancellationToken);
        PressKey(VirtualKeys.Enter, cancellationToken);
    }

    public void Logout(CancellationToken cancellationToken)
    {
        Delay(Delays.WindowActivationMs, cancellationToken);
        PressKeyChord(VirtualKeys.Alt, VirtualKeys.Q, cancellationToken);
        Delay(Delays.WindowActivationMs, cancellationToken);
        PressKey(VirtualKeys.Enter, cancellationToken);
    }

    public void Delay(int milliseconds, CancellationToken cancellationToken)
    {
        cancellationToken.WaitHandle.WaitOne(milliseconds);
        cancellationToken.ThrowIfCancellationRequested();
    }

    [LibraryImport("user32.dll", EntryPoint = "SetCursorPosA")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetCursorPos(int x, int y);

    [LibraryImport("user32.dll", EntryPoint = "mouse_eventA")]
    private static partial void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    [LibraryImport("user32.dll", EntryPoint = "keybd_eventA")]
    private static partial void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
}
