namespace Automaton.Helpers;

internal interface IGameActionService
{
    void QuitGame(CancellationToken cancellationToken);
    void Logout(CancellationToken cancellationToken);
    void RebootOperatingSystem(CancellationToken cancellationToken);
    void TryHideUi(string? capturePathToValidate, CancellationToken cancellationToken);
}
