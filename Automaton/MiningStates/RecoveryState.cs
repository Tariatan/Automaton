using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class RecoveryState(
    IAutomationInputController automationInputController,
    AsteroidBeltOverviewDetector beltOverviewDetector,
    HomeStationDetector homeStationDetector,
    PlayNowButtonLocator playNowButtonLocator)
    : IMiningAutomationState
{
    private const string CaptureSuffix = ".mining-recovery";
    private const int MaximumStartingGameTransitionsBeforeReboot = 5;
    private static int _sStartingGameTransitionsCount;

    private readonly ILogger m_Logger = Log.ForContext<RecoveryState>();

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.Recovery;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        m_Logger.Debug("Executing {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();

        if (context.LastAction == MiningAutomationActionKind.Relogin)
        {
            m_Logger.Error("Logging out...");
            automationInputController.Logout(cancellationToken);

            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Login,
                MiningAutomationActionKind.None);
        }

        // Debounce
        automationInputController.Delay(Delays.RecoveryMs, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        using var capture = context.ScreenCaptureService.CaptureCurrentScreen(CaptureSuffix);

        var overviewLocated = beltOverviewDetector.AnalyzeAndDrawDebugOverlay(capture.CapturePath).OverviewLocated;
        var homeStationLocated = homeStationDetector.Analyze(capture.Image).HomeStationLocated;
        var inSpace = overviewLocated && homeStationLocated;
        var docked = UndockButtonDetector.TryLocate(capture.Image, out _);

        if (inSpace)
        {
            _sStartingGameTransitionsCount = 0;
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
            _sStartingGameTransitionsCount = 0;
            m_Logger.Error("Undock button found during recovery => try unloading cargo again");
            // Docked and Undock button found
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.UnloadCargo,
                MiningAutomationActionKind.Recover,
                capture.CapturePath);
        }

        // Game crashed?
        var playNowFound = playNowButtonLocator.TryLocateAndDrawDebugOverlay(capture.CapturePath, out _);
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
        automationInputController.QuitGame(cancellationToken);
        // Debounce
        automationInputController.Delay(Delays.RecoveryMs, cancellationToken);
        return BuildStartingGameTransition(
            Kind,
            MiningAutomationStateKind.StartingGame,
            MiningAutomationActionKind.None,
            capture.CapturePath,
            cancellationToken);
    }

    private MiningAutomationStateTransition BuildStartingGameTransition(
        MiningAutomationStateKind state,
        MiningAutomationStateKind nextState,
        MiningAutomationActionKind action,
        string? capturePath,
        CancellationToken cancellationToken)
    {
        _sStartingGameTransitionsCount++;
        if (_sStartingGameTransitionsCount <= MaximumStartingGameTransitionsBeforeReboot)
        {
            return new MiningAutomationStateTransition(state, nextState, action, capturePath);
        }

        m_Logger.Error(
            "StartingGame transition count exceeded threshold ({Threshold}). Triggering operating system reboot.",
            MaximumStartingGameTransitionsBeforeReboot);
        automationInputController.RebootOperatingSystem(cancellationToken);
        return new MiningAutomationStateTransition(state, nextState, MiningAutomationActionKind.Reboot, capturePath);
    }

    internal static void ResetStartingGameTransitionsCounterForTests()
    {
        _sStartingGameTransitionsCount = 0;
    }
}
