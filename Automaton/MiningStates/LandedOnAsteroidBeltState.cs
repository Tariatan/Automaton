using Automaton.Detectors;
using OpenCvSharp;

namespace Automaton.MiningStates;

internal sealed class LandedOnAsteroidBeltState : IMiningAutomationState
{
    private const string CaptureSuffix = ".mining-landed-on-asteroid-belt";
    private readonly AsteroidBeltOverviewDetector m_Detector;

    public LandedOnAsteroidBeltState()
        : this(new AsteroidBeltOverviewDetector())
    {
    }

    internal LandedOnAsteroidBeltState(AsteroidBeltOverviewDetector detector)
    {
        m_Detector = detector;
    }

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.LandedOnAsteroidBelt;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(CaptureSuffix);
        using var screen = Cv2.ImRead(capturePath);
        var analysis = m_Detector.Analyze(screen);

        if (!analysis.OverviewLocated)
        {
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Recover,
                capturePath);
        }

        if (analysis.AsteroidBelts.Count == 0)
        {
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.SelectBeltAndWarp,
                MiningAutomationActionKind.None,
                capturePath);
        }

        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.ApproachingAsteroid,
            MiningAutomationActionKind.ApproachAsteroid,
            capturePath);
    }
}
