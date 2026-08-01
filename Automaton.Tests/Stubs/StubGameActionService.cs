using Automaton.Core.Detectors;
using Automaton.Core.Helpers;
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
    public int TryHideUiCallCount { get; private set; }
    public Size? LastTryHideUiImageSize { get; private set; }
    public Action? OnCloseGameClient { get; init; }
    public Action? OnLogout { get; init; }
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
        OnLogout?.Invoke();
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
}
