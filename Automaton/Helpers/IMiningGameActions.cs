namespace Automaton.Helpers;

internal interface IMiningGameActions
{
    void ToggleFirstLaser(CancellationToken cancellationToken);
    void ToggleSecondLaser(CancellationToken cancellationToken);
    void TogglePropulsionModule(CancellationToken cancellationToken);
    void TriggerTargetLock(CancellationToken cancellationToken);
    void TriggerTargetApproach(CancellationToken cancellationToken);
    void WarpToTarget(CancellationToken cancellationToken);
    void WarpToTargetAndDock(CancellationToken cancellationToken);
}