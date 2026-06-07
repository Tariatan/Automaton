using Automaton.Detectors;
using OpenCvSharp;

namespace Automaton.Helpers;

internal interface IGameActionService
{
    void QuitGame(CancellationToken cancellationToken);
    void Logout(
        ScreenCaptureService screenCaptureService,
        PilotAvatarDetector pilotAvatarDetector,
        int currentPilotIndex,
        CancellationToken cancellationToken);
    void Login(int pilotIndex, Point activationPoint, CancellationToken cancellationToken);
    void RebootOperatingSystem(CancellationToken cancellationToken);
    void TryHideUi(string? capturePathToValidate, CancellationToken cancellationToken);
    void CloseActiveWindow(CancellationToken cancellationToken);
    void ToggleProjectDiscoveryWindow(CancellationToken cancellationToken);
    void ToggleFirstLaser(CancellationToken cancellationToken);
    void ToggleSecondLaser(CancellationToken cancellationToken);
    void TogglePropulsionModule(CancellationToken cancellationToken);
    void TriggerTargetLock(CancellationToken cancellationToken);
    void TriggerTargetApproach(CancellationToken cancellationToken);
    void WarpToTarget(CancellationToken cancellationToken);
    void WarpToTargetAndDock(CancellationToken cancellationToken);
}
