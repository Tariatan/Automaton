using Automaton.Helpers;
using OpenCvSharp;

namespace Automaton.Tests.Stubs;

internal sealed class StubAutomationInputController : IAutomationInputController
{
    public List<Point> MoveTargets { get; } = [];
    public List<int> Delays { get; } = [];
    public List<KeyboardInput> KeyInputs { get; } = [];
    public int ClickCount { get; private set; }
    public Action<int>? OnDelay { get; init; }
    public Action<ushort, ushort>? OnPressKeyChord { get; init; }

    public void ClickUiElement(Point point, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        MoveTargets.Add(point);
        ClickCount++;
        MoveTargets.Add(new Point(250, 250));
    }

    public void MoveTo(Point point)
    {
        MoveTargets.Add(point);
    }

    public void LeftClick(CancellationToken cancellationToken, bool recordClick = true)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ClickCount++;
    }

    public void PressKey(ushort virtualKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        KeyInputs.Add(new KeyboardInput(null, null, virtualKey, HoldDelayMs: 0));
    }

    public void PressKeyChordWithHold(
        ushort modifierVirtualKey,
        ushort virtualKey,
        CancellationToken cancellationToken,
        int holdDelayMs = Automaton.Primitives.Delays.KeyChordHoldMs,
        int transitionDelayMs = Automaton.Primitives.Delays.KeyChordTransitionMs)
    {
        cancellationToken.ThrowIfCancellationRequested();
        KeyInputs.Add(new KeyboardInput(modifierVirtualKey, null, virtualKey, transitionDelayMs, holdDelayMs));
        OnPressKeyChord?.Invoke(modifierVirtualKey, virtualKey);
    }

    public void PressKeyChordWithHold(
        ushort firstModifier,
        ushort secondModifier,
        ushort virtualKey,
        CancellationToken cancellationToken,
        int holdDelayMs = Automaton.Primitives.Delays.KeyChordHoldMs,
        int transitionDelayMs = Automaton.Primitives.Delays.KeyChordTransitionMs)
    {
        cancellationToken.ThrowIfCancellationRequested();
        KeyInputs.Add(new KeyboardInput(firstModifier, secondModifier, virtualKey, transitionDelayMs, holdDelayMs));
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
    ushort VirtualKey,
    int TransitionDelayMs = Automaton.Primitives.Delays.KeyChordTransitionMs,
    int HoldDelayMs = Automaton.Primitives.Delays.KeyChordHoldMs);
