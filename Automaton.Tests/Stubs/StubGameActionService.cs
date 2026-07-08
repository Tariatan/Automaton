using Automaton.Detectors;
using Automaton.Helpers;
using OpenCvSharp;

namespace Automaton.Tests.Stubs;

internal sealed class StubGameActionService : IGameActionService
{
    public bool CloseGameClientCalled { get; private set; }
    public bool QuitGameCalled { get; private set; }
    public bool LogoutCalled { get; private set; }
    public int LogoutCallCount { get; private set; }
    public bool RebootOperatingSystemCalled { get; private set; }
    public bool ShutdownOperatingSystemCalled { get; private set; }
    public int CloseActiveWindowCallCount { get; private set; }
    public int ToggleProjectDiscoveryWindowCallCount { get; private set; }
    public int ToggleFirstLaserCallCount { get; private set; }
    public int ToggleSecondLaserCallCount { get; private set; }
    public int TogglePropulsionModuleCallCount { get; private set; }
    public int TriggerTargetLockCallCount { get; private set; }
    public int TriggerTargetApproachCallCount { get; private set; }
    public int WarpToTargetCallCount { get; private set; }
    public int WarpToTargetAndDockCallCount { get; private set; }
    public int TryHideUiCallCount { get; private set; }
    public Size? LastTryHideUiImageSize { get; private set; }
    public Action? OnCloseGameClient { get; init; }
    public Action? OnTryHideUi { get; init; }

    public void QuitGame(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        QuitGameCalled = true;
    }

    public void CloseGameClient(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CloseGameClientCalled = true;
        OnCloseGameClient?.Invoke();
    }

    public void Logout(
        ScreenCaptureService screenCaptureService,
        PilotAvatarDetector pilotAvatarDetector,
        int currentPilotIndex,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LogoutCalled = true;
        LogoutCallCount++;
    }

    public void RebootOperatingSystem(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RebootOperatingSystemCalled = true;
    }

    public void ShutdownOperatingSystem(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ShutdownOperatingSystemCalled = true;
    }

    public void TryHideUi(Mat captureToValidate, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TryHideUiCallCount++;
        LastTryHideUiImageSize = captureToValidate.Size();
        OnTryHideUi?.Invoke();
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
