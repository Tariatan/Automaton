using Automaton.Detectors;
using OpenCvSharp;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class DockingState : IMiningAutomationState
{
    private const string CaptureSuffix = ".mining-dock";
    private const string DockedCaptureSuffix = ".mining-docked-polling";
    private const int BeforeDockDelayMilliseconds = 1_000;
    private const int DockedPollingDelayMilliseconds = 5_000;
    private const int DockedBounceDelayMilliseconds = 15_000;
    private const ushort VirtualKeyD = 0x44;

    private readonly HomeStationDetector m_HomeStationDetector;
    private readonly UndockButtonDetector m_UndockButtonDetector;
    private readonly ILogger m_Logger;

    public DockingState()
        : this(new HomeStationDetector(), new UndockButtonDetector(), Log.ForContext<DockingState>())
    {
    }

    internal DockingState(
        HomeStationDetector homeStationDetector,
        UndockButtonDetector undockButtonDetector,
        ILogger? logger = null)
    {
        m_HomeStationDetector = homeStationDetector;
        m_UndockButtonDetector = undockButtonDetector;
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
                capturePath,
                AsteroidBeltOverview: analysis.OverviewAnalysis);
        }

        // Select home station in the belt overview
        m_Logger.Information("Selecting home station");
        context.ClickUiElement(Center(analysis.HomeStationBounds.Value), cancellationToken);

        // Wait 1 second
        context.AutomationInputController.Delay(BeforeDockDelayMilliseconds, cancellationToken);

        // Warping home
        m_Logger.Information("Warping home");
        context.AutomationInputController.PressKey(VirtualKeyD, cancellationToken);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.AutomationInputController.Delay(DockedPollingDelayMilliseconds, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(DockedCaptureSuffix);
            using var pollingScreen = Cv2.ImRead(capturePath);

            // Docked
            if (m_UndockButtonDetector.TryLocate(pollingScreen, out _))
            {
                m_Logger.Information("Docked");
                break;
            }
        }

        context.AutomationInputController.Delay(DockedBounceDelayMilliseconds, cancellationToken);

        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.UnloadCargo,
            MiningAutomationActionKind.Dock,
            capturePath,
            AsteroidBeltOverview: analysis.OverviewAnalysis);
    }

    private static Point Center(Rect bounds) => new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
}
