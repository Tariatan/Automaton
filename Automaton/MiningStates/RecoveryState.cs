using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class RecoveryState(
    IAutomationInputController automationInputController,
    IGameActionService gameActionService,
    AsteroidBeltOverviewDetector beltOverviewDetector,
    PlayNowButtonDetector playNowButtonDetector)
    : IMiningAutomationState
{
    private const string CaptureSuffix = ".mining-recovery";
    private static int sStartingGameTransitionsCount;

    private readonly ILogger m_Logger = Log.ForContext<RecoveryState>();

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.Recovery;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        m_Logger.Information("Executing {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();

        if (context.LastAction == MiningAutomationActionKind.RestartGame)
        {
            m_Logger.Error("Game restart requested.");
            return RestartGame(null, cancellationToken);
        }

        // Debounce
        automationInputController.Delay(Delays.RecoveryMs, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        using var capture = context.ScreenCaptureService.CaptureCurrentScreen(CaptureSuffix);

        var overviewAnalysis = beltOverviewDetector.Detect(capture.Image);
        var inSpace = overviewAnalysis is { OverviewLocated: true, HomeStationLocated: true };
        var docked = UndockButtonDetector.Detect(capture.Image, out _);

        if (inSpace)
        {
            sStartingGameTransitionsCount = 0;
            m_Logger.Error("Home station detected during recovery => try docking");
            // We are still in space and docking is possible
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Dock,
                MiningAutomationActionKind.Recover,
                capture.CapturePath);
        }

        if (docked)
        {
            sStartingGameTransitionsCount = 0;
            m_Logger.Error("Undock button found during recovery => try unloading cargo again");
            // Docked and Undock button found
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.UnloadCargo,
                MiningAutomationActionKind.Recover,
                capture.CapturePath);
        }

        // Game crashed?
        var playNowFound = playNowButtonDetector.Detect(capture.CapturePath, out var _);
        if (playNowFound)
        {
            m_Logger.Error("Game crashed => Restarting...");
            return BuildStartingGameTransition(
                Kind,
                MiningAutomationStateKind.StartingGame,
                MiningAutomationActionKind.Recover,
                capture.CapturePath,
                cancellationToken);
        }

        // Last resort
        m_Logger.Error("Home Station not found in Belt overview while undocked. Quit Game instead of endless wandering in space.");
        return RestartGame(capture.CapturePath, cancellationToken);
    }

    private MiningAutomationStateTransition RestartGame(
        string? capturePath,
        CancellationToken cancellationToken)
    {
        gameActionService.QuitGame(cancellationToken);
        // Debounce
        automationInputController.Delay(Delays.RecoveryMs, cancellationToken);
        return BuildStartingGameTransition(
            Kind,
            MiningAutomationStateKind.StartingGame,
            MiningAutomationActionKind.None,
            capturePath,
            cancellationToken);
    }

    private MiningAutomationStateTransition BuildStartingGameTransition(
        MiningAutomationStateKind state,
        MiningAutomationStateKind nextState,
        MiningAutomationActionKind action,
        string? capturePath,
        CancellationToken cancellationToken)
    {
        sStartingGameTransitionsCount++;
        if (sStartingGameTransitionsCount <= Settings.MaximumStartingGameTransitionsBeforeReboot)
        {
            return new MiningAutomationStateTransition(state, nextState, action, capturePath);
        }

        m_Logger.Error(
            "StartingGame transition count exceeded threshold ({Threshold}). Triggering operating system reboot.",
            Settings.MaximumStartingGameTransitionsBeforeReboot);
        gameActionService.RebootOperatingSystem(cancellationToken);
        return new MiningAutomationStateTransition(state, nextState, MiningAutomationActionKind.Reboot, capturePath);
    }

    internal static void ResetStartingGameTransitionsCounterForTests()
    {
        sStartingGameTransitionsCount = 0;
    }
}
