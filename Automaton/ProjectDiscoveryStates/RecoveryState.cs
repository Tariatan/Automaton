using Automaton.Helpers;
using Automaton.Primitives;
using Serilog;

namespace Automaton.ProjectDiscoveryStates;

internal sealed class RecoveryState(
    ScreenCaptureService screenCaptureService,
    IAutomationInputController automationInputController) : IProjectDiscoveryAutomationState
{
    private const string CaptureSuffix = ".discovery-recovery";
    private readonly ILogger m_Logger = Log.ForContext<RecoveryState>();
    public DiscoveryAutomationStateKind Kind => DiscoveryAutomationStateKind.Recovery;

    public DiscoveryAutomationStateTransition Execute(ProjectDiscoveryAutomationContext context, CancellationToken cancellationToken)
    {
        using var capture = screenCaptureService.CaptureCurrentScreen(CaptureSuffix);
        m_Logger.Warning("Executing discovery recovery. CapturePath={CapturePath}", capture.CapturePath);
        automationInputController.Delay(Delays.RecoveryMs, cancellationToken);
        return new DiscoveryAutomationStateTransition(
            Kind,
            DiscoveryAutomationStateKind.StartingGame,
            DiscoveryAutomationActionKind.Recover,
            capture.CapturePath);
    }
}
