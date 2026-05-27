using Automaton.Helpers;
using OpenCvSharp;

namespace Automaton.Tests;

internal sealed class StubAutomationInputController : IAutomationInputController
{
    public List<Point> MoveTargets { get; } = [];
    public List<int> Delays { get; } = [];
    public List<KeyboardInput> KeyInputs { get; } = [];
    public int ClickCount { get; private set; }
    public bool QuitGameCalled { get; private set; }
    public bool RebootOperatingSystemCalled { get; private set; }
    public bool LogoutCalled { get; private set; }
    public Action<int>? OnDelay { get; init; }
    public Action<ushort, ushort>? OnPressKeyChord { get; init; }

    public void ClickUiElement(Point point, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        MoveTargets.Add(point);
        ClickCount++;
        MoveTargets.Add(new Point(250, 250));
    }

    public void TryHideUi(string? capturePathToValidate, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public void MoveTo(Point point)
    {
        MoveTargets.Add(point);
    }

    public void LeftClick(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ClickCount++;
    }

    public void PressKey(ushort virtualKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        KeyInputs.Add(new KeyboardInput(null, null, virtualKey));
    }

    public void PressKeyChord(ushort modifierVirtualKey, ushort virtualKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        KeyInputs.Add(new KeyboardInput(modifierVirtualKey, null, virtualKey));
        OnPressKeyChord?.Invoke(modifierVirtualKey, virtualKey);
    }

    public void QuitGame(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        QuitGameCalled = true;
    }

    public void RebootOperatingSystem(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RebootOperatingSystemCalled = true;
    }

    public void Logout(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LogoutCalled = true;
    }

    public void Delay(TimeSpan milliseconds, CancellationToken cancellationToken)
    {
        Delay((int)Math.Round(milliseconds.TotalMilliseconds), cancellationToken);
    }

    public void Delay(int milliseconds, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Delays.Add(milliseconds);
        OnDelay?.Invoke(milliseconds);
        cancellationToken.ThrowIfCancellationRequested();
    }
}

internal readonly record struct KeyboardInput(
    ushort? ModifierVirtualKey,
    ushort? SecondModifierVirtualKey,
    ushort VirtualKey);
