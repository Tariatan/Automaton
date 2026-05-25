using System.Runtime.InteropServices;
using Automaton.Primitives;
using OpenCvSharp;

namespace Automaton.Helpers;

internal sealed class AutomationInputController : IAutomationInputController
{
    private const uint LeftDownEvent = 0x0002;
    private const uint LeftUpEvent = 0x0004;
    private const uint KeyUpEvent = 0x0002;

    private const int MouseParkingAreaLeft = 200;
    private const int MouseParkingAreaTop = 200;
    private const int MouseParkingAreaWidth = 100;
    private const int MouseParkingAreaHeight = 100;

    public void ClickUiElement(Point point, CancellationToken cancellationToken)
    {
        MoveTo(point);
        LeftClick(cancellationToken);
        MoveTo(BuildRandomMouseParkingPoint());
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

    public void Delay(TimeSpan milliseconds, CancellationToken cancellationToken)
    {
        Delay((int)Math.Round(milliseconds.TotalMilliseconds), cancellationToken);
    }

    public void Delay(int milliseconds, CancellationToken cancellationToken)
    {
        cancellationToken.WaitHandle.WaitOne(milliseconds);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static Point BuildRandomMouseParkingPoint()
    {
        return new Point(
            Random.Shared.Next(MouseParkingAreaLeft, MouseParkingAreaLeft + MouseParkingAreaWidth),
            Random.Shared.Next(MouseParkingAreaTop, MouseParkingAreaTop + MouseParkingAreaHeight));
    }

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
}
