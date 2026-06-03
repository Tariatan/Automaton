using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class DockingState(
    IAutomationInputController automationInputController,
    IGameActionService gameActionService,
    AsteroidBeltOverviewDetector asteroidBeltOverviewDetector)
    : IMiningAutomationState
{
    private const string CaptureSuffix = ".mining-dock";
    private const string DockedCaptureSuffix = ".mining-docked-polling";

    private readonly ILogger m_Logger = Log.ForContext<DockingState>();

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.Dock;

    public MiningAutomationStateTransition Execute(MiningAutomationContext context, CancellationToken cancellationToken)
    {
        m_Logger.Information("Executing {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();

        var capture = context.ScreenCaptureService.CaptureCurrentScreen(CaptureSuffix);
        var analysis = asteroidBeltOverviewDetector.Detect(capture.Image);
        if (!analysis.OverviewLocated || !analysis.HomeStationLocated)
        {
            m_Logger.Error("Failed to detect home station. CapturePath={CapturePath}", capture.CapturePath);
            var result = Recover(capture.CapturePath, MiningAutomationFailureReason.DetectionMiss);
            capture.Dispose();
            return result;
        }

        capture.Dispose();

        // Select home station in the belt overview
        m_Logger.Information("Returning home");
        automationInputController.ClickUiElement(GeometryHelper.Center(analysis.HomeStationBounds!.Value), cancellationToken);

        // Wait 1 second
        automationInputController.Delay(Delays.BeforeDockMs, cancellationToken);

        // Warping home
        gameActionService.WarpToTargetAndDock(cancellationToken);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            automationInputController.Delay(Delays.DockedPollingMs, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            capture = context.ScreenCaptureService.CaptureCurrentScreen(DockedCaptureSuffix);

            // Docked
            if (UndockButtonDetector.Detect(capture.Image, out _))
            {
                m_Logger.Information("Docked");
                break;
            }

            capture.Dispose();
        }

        automationInputController.Delay(Delays.DockedBounceMs, cancellationToken);

        var transitionResult = new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.UnloadCargo,
            MiningAutomationActionKind.Dock,
            capture.CapturePath);
        capture.Dispose();
        return transitionResult;
    }

    private MiningAutomationStateTransition Recover(string capturePath, MiningAutomationFailureReason failureReason = MiningAutomationFailureReason.None)
    {
        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Recovery,
            MiningAutomationActionKind.Recover,
            capturePath)
        {
            FailureReason = failureReason
        };
    }
}
