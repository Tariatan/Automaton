using Automaton.Detectors;
using Automaton.Primitives;
using OpenCvSharp;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class DockingState : IMiningAutomationState
{
    private const string CaptureSuffix = ".mining-dock";
    private const string DockedCaptureSuffix = ".mining-docked-polling";

    private readonly HomeStationDetector m_HomeStationDetector;
    private readonly ILogger m_Logger;

    public DockingState()
        : this(new HomeStationDetector(), Log.ForContext<DockingState>())
    {
    }

    private DockingState(
        HomeStationDetector homeStationDetector,
        ILogger? logger = null)
    {
        m_HomeStationDetector = homeStationDetector;
        m_Logger = logger ?? Log.ForContext<DockingState>();
    }

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.Dock;

    public MiningAutomationStateTransition Execute(MiningAutomationContext context, CancellationToken cancellationToken)
    {
        m_Logger.Debug("Executing {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();

        var capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(CaptureSuffix);
        using var screen = Cv2.ImRead(capturePath);
        var analysis = m_HomeStationDetector.Analyze(screen);
        if (!analysis.HomeStationLocated || analysis.HomeStationBounds is null)
        {
            m_Logger.Error(
                "Failed to detect home station. BestMatchScore={BestMatchScore:F3}, OverviewLocated={OverviewLocated}, OverviewBounds={OverviewBounds}",
                analysis.BestMatchScore,
                analysis.OverviewAnalysis.OverviewLocated,
                analysis.OverviewAnalysis.OverviewBounds);
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Recover,
                capturePath);
        }

        // Select home station in the belt overview
        m_Logger.Information("Selecting home station");
        context.ClickUiElement(Center(analysis.HomeStationBounds.Value), cancellationToken);

        // Wait 1 second
        context.AutomationInputController.Delay(Delays.BeforeDockMs, cancellationToken);

        // Warping home
        m_Logger.Information("Warping home");
        context.AutomationInputController.PressKey(VirtualKeys.D, cancellationToken);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.AutomationInputController.Delay(Delays.DockedPollingMs, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(DockedCaptureSuffix);
            using var pollingScreen = Cv2.ImRead(capturePath);

            // Docked
            if (UndockButtonDetector.TryLocate(pollingScreen, out _))
            {
                m_Logger.Information("Docked");
                break;
            }
        }

        context.AutomationInputController.Delay(Delays.DockedBounceMs, cancellationToken);

        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.UnloadCargo,
            MiningAutomationActionKind.Dock,
            capturePath);
    }

    private static Point Center(Rect bounds) => new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
}
