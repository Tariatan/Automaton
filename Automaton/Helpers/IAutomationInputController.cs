using OpenCvSharp;

namespace Automaton.Helpers;

internal interface IAutomationInputController
{
    void MoveTo(Point point);

    void LeftClick(CancellationToken cancellationToken);

    void PressKey(ushort virtualKey, CancellationToken cancellationToken);

    void PressKeyChord(ushort modifierVirtualKey, ushort virtualKey, CancellationToken cancellationToken);

    void PressKeyChord(
        ushort firstModifierVirtualKey,
        ushort secondModifierVirtualKey,
        ushort virtualKey,
        CancellationToken cancellationToken);

    void QuitGame(CancellationToken cancellationToken);

    void Logout(CancellationToken cancellationToken);

    void ClickUiElement(Point point, CancellationToken cancellationToken);

    void Delay(TimeSpan milliseconds, CancellationToken cancellationToken);
    void Delay(int milliseconds, CancellationToken cancellationToken);
}
