using Automaton.Helpers;
using OpenCvSharp;

namespace Automaton.Tests;

internal sealed class StubGameActionService : IGameActionService
{
    public bool QuitGameCalled { get; private set; }
    public bool LogoutCalled { get; private set; }
    public bool RebootOperatingSystemCalled { get; private set; }
    public int CloseActiveWindowCallCount { get; private set; }
    public int ToggleProjectDiscoveryWindowCallCount { get; private set; }

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

    public void Login(int pilotIndex, Point activationPoint, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
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

    public void CloseActiveWindow(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CloseActiveWindowCallCount++;
    }

    public void ToggleProjectDiscoveryWindow(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ToggleProjectDiscoveryWindowCallCount++;
    }
}
