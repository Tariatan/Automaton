using Automaton.Helpers;
using OpenCvSharp;

namespace Automaton.Tests.Stubs;

internal sealed class StubGameActionService : IGameActionService
{
    public bool QuitGameCalled { get; private set; }
    public bool LogoutCalled { get; private set; }
    public bool RebootOperatingSystemCalled { get; private set; }
    public int CloseActiveWindowCallCount { get; private set; }
    public int ToggleProjectDiscoveryWindowCallCount { get; private set; }
    public int ToggleFirstLaserCallCount { get; private set; }
    public int ToggleSecondLaserCallCount { get; private set; }
    public int TogglePropulsionModuleCallCount { get; private set; }
    public int TriggerTargetLockCallCount { get; private set; }
    public int TriggerTargetApproachCallCount { get; private set; }
    public int WarpToTargetCallCount { get; private set; }
    public int WarpToTargetAndDockCallCount { get; private set; }
    public int LoginCallCount { get; private set; }
    public List<(int PilotIndex, Point ActivationPoint)> LoginCalls { get; } = [];

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
        cancellationToken.ThrowIfCancellationRequested();
        LoginCallCount++;
        LoginCalls.Add((pilotIndex, activationPoint));
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

    public void ToggleFirstLaser(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ToggleFirstLaserCallCount++;
    }

    public void ToggleSecondLaser(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ToggleSecondLaserCallCount++;
    }

    public void TogglePropulsionModule(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TogglePropulsionModuleCallCount++;
    }

    public void TriggerTargetLock(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TriggerTargetLockCallCount++;
    }

    public void TriggerTargetApproach(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TriggerTargetApproachCallCount++;
    }

    public void WarpToTarget(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WarpToTargetCallCount++;
    }

    public void WarpToTargetAndDock(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WarpToTargetAndDockCallCount++;
    }
}
