using OpenCvSharp;

namespace Automaton.MiningStates;

internal sealed class LandedOnAsteroidBeltState : IMiningAutomationState
{
    private const int LandingPollingMilliseconds = 1_000;
    private const int LandingPollingAttemptCount = 60;
    private const string CaptureSuffix = ".mining-landed-on-asteroid-belt";
    private const ushort VirtualKeyA = 0x41;

    private readonly AsteroidBeltLandingDetector m_Detector;

    public LandedOnAsteroidBeltState()
        : this(new AsteroidBeltLandingDetector())
    {
    }

    internal LandedOnAsteroidBeltState(AsteroidBeltLandingDetector detector)
    {
        m_Detector = detector;
    }

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.LandedOnAsteroidBelt;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        AsteroidBeltLandingAnalysis? analysis = null;
        string? capturePath = null;
        for (var attempt = 0; attempt < LandingPollingAttemptCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(CaptureSuffix);
            using var screen = Cv2.ImRead(capturePath);
            analysis = m_Detector.Analyze(screen);
            if (analysis.LandedOnAsteroidBelt)
            {
                break;
            }

            context.AutomationInputController.Delay(LandingPollingMilliseconds, cancellationToken);
        }

        if (analysis is null ||
            !analysis.LandedOnAsteroidBelt ||
            analysis.MineOverviewBounds is null)
        {
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Recover,
                capturePath,
                AsteroidBeltLanding: analysis);
        }

        if (analysis.Asteroids.Count == 0)
        {
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.EmptyOnUndock,
                MiningAutomationActionKind.None,
                capturePath,
                AsteroidBeltLanding: analysis);
        }

        context.ClickUiElement(Center(analysis.Asteroids[0].Bounds), cancellationToken);
        context.AutomationInputController.PressKey(VirtualKeyA, cancellationToken);
        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Mining,
            MiningAutomationActionKind.ApproachAsteroid,
            capturePath,
            AsteroidBeltLanding: analysis);
    }

    private static Point Center(Rect bounds)
    {
        return new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
    }
}
