using Automaton.Detectors;
using OpenCvSharp;

namespace Automaton.Helpers;

internal interface IGameActionService
{
    void CloseGameClient(CancellationToken cancellationToken);
    void QuitGame(CancellationToken cancellationToken);
    void Logout(
        ScreenCaptureService screenCaptureService,
        PilotAvatarDetector pilotAvatarDetector,
        int currentPilotIndex,
        CancellationToken cancellationToken);
    void RebootOperatingSystem(CancellationToken cancellationToken);
    void ShutdownOperatingSystem(CancellationToken cancellationToken);
    void TryHideUi(Mat captureToValidate, CancellationToken cancellationToken);
    void CloseActiveWindow(CancellationToken cancellationToken);
}