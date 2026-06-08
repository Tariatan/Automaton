using Automaton.Helpers;
using Automaton.Primitives;
using Serilog;

namespace Automaton.ProjectDiscoveryStates;

internal sealed class RecoveryState(
    ScreenCaptureService screenCaptureService,
    IGameActionService gameActionService,
    IAutomationInputController automationInputController) : IProjectDiscoveryAutomationState
{
    private const string CaptureSuffix = ".discovery-recovery";
    private readonly ILogger m_Logger = Log.ForContext<RecoveryState>();
    public DiscoveryAutomationStateKind Kind => DiscoveryAutomationStateKind.Recovery;

    private static int sStartingGameTransitionsCount;

    public DiscoveryAutomationStateTransition Execute(ProjectDiscoveryAutomationContext context, CancellationToken cancellationToken)
    {
        using var capture = screenCaptureService.CaptureCurrentScreen(CaptureSuffix);
        m_Logger.Warning("Executing discovery recovery. CapturePath={CapturePath}", capture.CapturePath);
        automationInputController.Delay(Delays.RecoveryMs, cancellationToken);


        if (context.LastAction == DiscoveryAutomationActionKind.RestartGame)
        {
            m_Logger.Error("Game restart requested.");
            return RestartGame(null, cancellationToken);
        }

        return new DiscoveryAutomationStateTransition(
            Kind,
            DiscoveryAutomationStateKind.StartingGame,
            DiscoveryAutomationActionKind.Recover,
            capture.CapturePath);
    }

    private DiscoveryAutomationStateTransition RestartGame(
        string? capturePath,
        CancellationToken cancellationToken)
    {
        gameActionService.QuitGame(cancellationToken);
        // Debounce
        automationInputController.Delay(Delays.RecoveryMs, cancellationToken);
        return BuildStartingGameTransition(
            Kind,
            DiscoveryAutomationStateKind.StartingGame,
            DiscoveryAutomationActionKind.StartGame,
            capturePath,
            cancellationToken);
    }

    private DiscoveryAutomationStateTransition BuildStartingGameTransition(
        DiscoveryAutomationStateKind state,
        DiscoveryAutomationStateKind nextState,
        DiscoveryAutomationActionKind action,
        string? capturePath,
        CancellationToken cancellationToken)
    {
        sStartingGameTransitionsCount++;
        if (sStartingGameTransitionsCount <= Settings.MaximumStartingGameTransitionsBeforeReboot)
        {
            return new DiscoveryAutomationStateTransition(state, nextState, action, capturePath);
        }

        m_Logger.Error(
            "StartingGame transition count exceeded threshold ({Threshold}). Triggering operating system reboot.",
            Settings.MaximumStartingGameTransitionsBeforeReboot);
        gameActionService.RebootOperatingSystem(cancellationToken);
        return new DiscoveryAutomationStateTransition(state, nextState, DiscoveryAutomationActionKind.Reboot, capturePath);
    }

    internal static void ResetStartingGameTransitionsCounterForTests()
    {
        sStartingGameTransitionsCount = 0;
    }
}
