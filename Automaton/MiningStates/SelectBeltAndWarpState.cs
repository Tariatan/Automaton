using Automaton.Detectors;
using OpenCvSharp;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class SelectBeltAndWarpState : IMiningAutomationState
{
    private const string CaptureSuffix = ".mining-select-belt-and-warp";
    private const string LandingCaptureSuffix = ".mining-landed-on-asteroid-belt";
    private const int LandingPollingMilliseconds = 1_000;
    private const int LandingPollingAttemptCount = 60;
    private const ushort VirtualKeyS = 0x53;

    private readonly AsteroidBeltOverviewDetector m_BeltOverviewDetector;
    private readonly AsteroidBeltLandingDetector m_LandingDetector;
    private readonly Func<int, int> m_NextRandomIndex;
    private readonly ILogger m_Logger;

    public SelectBeltAndWarpState()
        : this(new AsteroidBeltOverviewDetector(), new AsteroidBeltLandingDetector(), Random.Shared.Next, Log.ForContext<SelectBeltAndWarpState>())
    {
    }

    internal SelectBeltAndWarpState(
        AsteroidBeltOverviewDetector beltOverviewDetector,
        AsteroidBeltLandingDetector landingDetector,
        Func<int, int> nextRandomIndex,
        ILogger? logger = null)
    {
        m_BeltOverviewDetector = beltOverviewDetector;
        m_LandingDetector = landingDetector;
        m_NextRandomIndex = nextRandomIndex;
        m_Logger = logger ?? Log.ForContext<SelectBeltAndWarpState>();
    }

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.SelectBeltAndWarp;

    public MiningAutomationStateTransition Execute(MiningAutomationContext context, CancellationToken cancellationToken)
    {
        m_Logger.Debug("Executing {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();
        var capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(CaptureSuffix);
        var analysis = Analyze(capturePath);
        // Failed to detect Belt overview tab
        if (!analysis.OverviewLocated || analysis.OverviewBeltButtonBounds is null)
        {
            m_Logger.Error("Failed to detect Belt overview tab");
            return Recover(capturePath, analysis);
        }

        // Select Belt overview tab
        context.ClickUiElement(Center(analysis.OverviewBeltButtonBounds.Value), cancellationToken);

        capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(CaptureSuffix);
        analysis = Analyze(capturePath);

        // Failed to detect any belts
        if (analysis.AsteroidBelts.Count == 0)
        {
            m_Logger.Error("Failed to detect any belts");
            return Recover(capturePath, analysis);
        }

        var selectedAsteroidBeltIndex = Math.Clamp(m_NextRandomIndex(analysis.AsteroidBelts.Count), 0, analysis.AsteroidBelts.Count - 1);
        var selectedAsteroidBelt = analysis.AsteroidBelts[selectedAsteroidBeltIndex];

        // Select asteroid belt
        context.ClickUiElement(Center(selectedAsteroidBelt.Bounds), cancellationToken);
        // Warp to asteroid belt
        context.AutomationInputController.PressKey(VirtualKeyS, cancellationToken);

        m_Logger.Information("Warp to asteroid belt {selectedAsteroidBeltIndex}", selectedAsteroidBeltIndex);

        AsteroidBeltLandingAnalysis? landingAnalysis = null;

        // Wait until landed on asteroid belt with 1 second interval
        for (var attempt = 0; attempt < LandingPollingAttemptCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(LandingCaptureSuffix);
            using var landingScreen = Cv2.ImRead(capturePath);
            landingAnalysis = m_LandingDetector.Analyze(landingScreen);

            // Landed on asteroid belt
            if (landingAnalysis.LandedOnAsteroidBelt)
            {
                m_Logger.Information("Landed on asteroid belt");
                return new MiningAutomationStateTransition(
                    Kind,
                    MiningAutomationStateKind.ApproachingAsteroid,
                    MiningAutomationActionKind.WarpToAsteroidField,
                    capturePath,
                    AsteroidBeltOverview: analysis,
                    AsteroidBeltLanding: landingAnalysis);
            }

            context.AutomationInputController.Delay(LandingPollingMilliseconds, cancellationToken);
        }

        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Recovery,
            MiningAutomationActionKind.Recover,
            capturePath,
            AsteroidBeltOverview: analysis,
            AsteroidBeltLanding: landingAnalysis);
    }

    private AsteroidBeltOverviewAnalysis Analyze(string capturePath)
    {
        using var screen = Cv2.ImRead(capturePath);
        return m_BeltOverviewDetector.Analyze(screen);
    }

    private MiningAutomationStateTransition Recover(string capturePath, AsteroidBeltOverviewAnalysis analysis)
    {
        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Recovery,
            MiningAutomationActionKind.Recover,
            capturePath,
            AsteroidBeltOverview: analysis);
    }

    private static Point Center(Rect bounds) => new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
}
