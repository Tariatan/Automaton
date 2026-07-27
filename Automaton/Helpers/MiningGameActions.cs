using Automaton.Primitives;
using Serilog;

namespace Automaton.Helpers;

internal sealed class MiningGameActions(IAutomationInputController inputController) : IMiningGameActions
{
    private readonly ILogger m_Logger = Log.ForContext<MiningGameActions>();

    public void ToggleFirstLaser(CancellationToken cancellationToken)
    {
        m_Logger.Information("Toggle first laser");
        inputController.PressKey(VirtualKeys.F1, cancellationToken);
    }

    public void ToggleSecondLaser(CancellationToken cancellationToken)
    {
        m_Logger.Information("Toggle second laser");
        inputController.PressKey(VirtualKeys.F2, cancellationToken);
    }

    public void TogglePropulsionModule(CancellationToken cancellationToken)
    {
        m_Logger.Information("Toggle propulsion module");
        inputController.PressKey(VirtualKeys.F4, cancellationToken);
    }

    public void TriggerTargetLock(CancellationToken cancellationToken)
    {
        m_Logger.Information("Trigger target lock");
        inputController.PressKey(VirtualKeys.Control, cancellationToken);
    }

    public void TriggerTargetApproach(CancellationToken cancellationToken)
    {
        m_Logger.Information("Trigger target approach");
        inputController.PressKey(VirtualKeys.A, cancellationToken);
    }

    public void WarpToTarget(CancellationToken cancellationToken)
    {
        m_Logger.Information("Warping to target");
        inputController.PressKey(VirtualKeys.S, cancellationToken);
    }

    public void WarpToTargetAndDock(CancellationToken cancellationToken)
    {
        m_Logger.Information("Warping to target and docking");
        inputController.PressKey(VirtualKeys.D, cancellationToken);
    }
}