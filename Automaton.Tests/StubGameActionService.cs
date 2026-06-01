using Automaton.Helpers;

namespace Automaton.Tests;

internal sealed class StubGameActionService : IGameActionService
{
    public bool QuitGameCalled { get; private set; }
    public bool LogoutCalled { get; private set; }
    public bool RebootOperatingSystemCalled { get; private set; }

    public void QuitGame(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        QuitGameCalled = true;
    }

    public void Logout(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LogoutCalled = true;
    }

    public void RebootOperatingSystem(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RebootOperatingSystemCalled = true;
    }

    public void TryHideUi(string? capturePathToValidate, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
    }
}
