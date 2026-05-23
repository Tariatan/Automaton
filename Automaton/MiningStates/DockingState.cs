using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using OpenCvSharp;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class DockingState(
    IAutomationInputController automationInputController,
    HomeStationDetector homeStationDetector,
    ILogger? logger = null)
    : IMiningAutomationState
{
    private const string CaptureSuffix = ".mining-dock";
    private const string DockedCaptureSuffix = ".mining-docked-polling";

    private readonly ILogger m_Logger = logger ?? Log.ForContext<DockingState>();

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.Dock;

    public MiningAutomationStateTransition Execute(MiningAutomationContext context, CancellationToken cancellationToken)
    {
        m_Logger.Debug("Executing {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();

        var capture = context.ScreenCaptureService.CaptureCurrentScreen(CaptureSuffix);
        var analysis = homeStationDetector.Analyze(capture.Image);
        if (!analysis.HomeStationLocated || analysis.HomeStationBounds is null)
        {
            m_Logger.Error(
                "Failed to detect home station. BestMatchScore={BestMatchScore:F3}, OverviewLocated={OverviewLocated}, OverviewBounds={OverviewBounds}",
                analysis.BestMatchScore,
                analysis.OverviewAnalysis.OverviewLocated,
                analysis.OverviewAnalysis.OverviewBounds);
            var result = Recover(capture.CapturePath);
            capture.Dispose();
            return result;
        }

        // Select home station in the belt overview
        m_Logger.Information("Selecting home station");
        automationInputController.ClickUiElement(GeometryHelper.Center(analysis.HomeStationBounds.Value), cancellationToken);
        capture.Dispose();

        // Wait 1 second
        automationInputController.Delay(Delays.BeforeDockMs, cancellationToken);

        // Warping home
        m_Logger.Information("Warping home");
        automationInputController.PressKey(VirtualKeys.D, cancellationToken);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            automationInputController.Delay(Delays.DockedPollingMs, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            capture = context.ScreenCaptureService.CaptureCurrentScreen(DockedCaptureSuffix);

            // Docked
            if (UndockButtonDetector.TryLocate(capture.Image, out _))
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

    private MiningAutomationStateTransition Recover(string capturePath)
    {
        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Recovery,
            MiningAutomationActionKind.QuitGameFromSpace,
            capturePath);
    }
}
