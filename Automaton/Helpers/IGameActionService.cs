using OpenCvSharp;

namespace Automaton.Helpers;

internal interface IGameActionService
{
    void QuitGame(CancellationToken cancellationToken);
    void Logout(CancellationToken cancellationToken);
    void Login(int pilotIndex, Point activationPoint, CancellationToken cancellationToken);
    void RebootOperatingSystem(CancellationToken cancellationToken);
    void TryHideUi(string? capturePathToValidate, CancellationToken cancellationToken);
    void CloseActiveWindow(CancellationToken cancellationToken);
    void ToggleProjectDiscoveryWindow(CancellationToken cancellationToken);
}
