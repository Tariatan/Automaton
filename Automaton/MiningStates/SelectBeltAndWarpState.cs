using Automaton.Detectors;
using OpenCvSharp;

namespace Automaton.MiningStates;

internal sealed class SelectBeltAndWarpState : IMiningAutomationState
{
    private const string CaptureSuffix = ".mining-select-belt-and-warp";
    private const string LandingCaptureSuffix = ".mining-landed-on-asteroid-belt";
    private const int LandingPollingMilliseconds = 1_000;
    private const int LandingPollingAttemptCount = 60;
    private const ushort VirtualKeyS = 0x53;

    private readonly AsteroidBeltOverviewDetector m_Detector;
    private readonly AsteroidBeltLandingDetector m_LandingDetector;
    private readonly Func<int, int> m_NextRandomIndex;

    public SelectBeltAndWarpState()
        : this(new AsteroidBeltOverviewDetector(), new AsteroidBeltLandingDetector(), Random.Shared.Next)
    {
    }

    internal SelectBeltAndWarpState(
        AsteroidBeltOverviewDetector detector,
        AsteroidBeltLandingDetector landingDetector,
        Func<int, int> nextRandomIndex)
    {
        m_Detector = detector;
        m_LandingDetector = landingDetector;
        m_NextRandomIndex = nextRandomIndex;
    }

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.SelectBeltAndWarp;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(CaptureSuffix);
        var analysis = Analyze(capturePath);
        if (!analysis.OverviewLocated ||
            analysis.OverviewBeltButtonBounds is null)
        {
            return Recover(capturePath, analysis);
        }

        context.ClickUiElement(Center(analysis.OverviewBeltButtonBounds.Value), cancellationToken);

        capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(CaptureSuffix);
        analysis = Analyze(capturePath);
        if (analysis.AsteroidBelts.Count == 0)
        {
            return Recover(capturePath, analysis);
        }

        var selectedAsteroidBeltIndex = Math.Clamp(
            m_NextRandomIndex(analysis.AsteroidBelts.Count),
            0,
            analysis.AsteroidBelts.Count - 1);
        var selectedAsteroidBelt = analysis.AsteroidBelts[selectedAsteroidBeltIndex];
        context.ClickUiElement(Center(selectedAsteroidBelt.Bounds), cancellationToken);
        context.AutomationInputController.PressKey(VirtualKeyS, cancellationToken);

        AsteroidBeltLandingAnalysis? landingAnalysis = null;
        for (var attempt = 0; attempt < LandingPollingAttemptCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(LandingCaptureSuffix);
            using var landingScreen = Cv2.ImRead(capturePath);
            landingAnalysis = m_LandingDetector.Analyze(landingScreen);
            if (landingAnalysis.LandedOnAsteroidBelt)
            {
                return new MiningAutomationStateTransition(
                    Kind,
                    MiningAutomationStateKind.LandedOnAsteroidBelt,
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
        return m_Detector.Analyze(screen);
    }

    private MiningAutomationStateTransition Recover(
        string capturePath,
        AsteroidBeltOverviewAnalysis analysis)
    {
        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Recovery,
            MiningAutomationActionKind.Recover,
            capturePath,
            AsteroidBeltOverview: analysis);
    }

    private static Point Center(Rect bounds)
    {
        return new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
    }
}
