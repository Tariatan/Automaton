using OpenCvSharp;

namespace Automaton.Helpers;

internal interface IAutomationInputController
{
    void MoveTo(Point point);

    void LeftClick(CancellationToken cancellationToken, bool recordClick = true);

    void PressKey(ushort virtualKey, CancellationToken cancellationToken);

    void PressKeyChord(ushort modifierVirtualKey, ushort virtualKey, CancellationToken cancellationToken, int transitionDelayMs = 30);

    void PressKeyChord(ushort firstModifier, ushort secondModifier, ushort virtualKey, CancellationToken cancellationToken, int transitionDelayMs = 30);

    void ClickUiElement(Point point, CancellationToken cancellationToken);

    void Delay(TimeSpan milliseconds, CancellationToken cancellationToken);
    void Delay(int milliseconds, CancellationToken cancellationToken);
}
