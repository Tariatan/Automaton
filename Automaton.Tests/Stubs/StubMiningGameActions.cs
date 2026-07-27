using Automaton.Helpers;

namespace Automaton.Tests.Stubs;

internal sealed class StubMiningGameActions : IMiningGameActions
{
    public int ToggleFirstLaserCallCount { get; private set; }
    public int ToggleSecondLaserCallCount { get; private set; }
    public int TogglePropulsionModuleCallCount { get; private set; }
    public int TriggerTargetLockCallCount { get; private set; }
    public int TriggerTargetApproachCallCount { get; private set; }
    public int WarpToTargetCallCount { get; private set; }
    public int WarpToTargetAndDockCallCount { get; private set; }

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