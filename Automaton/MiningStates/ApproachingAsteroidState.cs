using Automaton.Detectors;
using OpenCvSharp;

namespace Automaton.MiningStates;

internal sealed class ApproachingAsteroidState : IMiningAutomationState
{
    private const string CaptureSuffix = ".mining-approaching-asteroid";
    private const int DistancePollingMilliseconds = 500;
    private const int DistancePollingAttemptCount = 60;
    private const int LockAsteroidDelayMilliseconds = 3_000;
    private const int BeforeSecondLaserDelayMilliseconds = 1_000;
    private const ushort VirtualKeyA = 0x41;
    private const ushort VirtualKeyControl = 0x11;
    private const ushort VirtualKeyF1 = 0x70;
    private const ushort VirtualKeyF2 = 0x71;
    private const ushort VirtualKeyF4 = 0x73;

    private readonly MineOverviewDetector m_MineOverviewDetector;
    private readonly AsteroidRowsDetector m_AsteroidRowsDetector;
    private readonly FirstAsteroidDistanceUnitDetector m_FirstAsteroidDistanceUnitDetector;

    public ApproachingAsteroidState()
        : this(new MineOverviewDetector(), new AsteroidRowsDetector(), new FirstAsteroidDistanceUnitDetector())
    {
    }

    internal ApproachingAsteroidState(
        MineOverviewDetector mineOverviewDetector,
        AsteroidRowsDetector asteroidRowsDetector,
        FirstAsteroidDistanceUnitDetector firstAsteroidDistanceUnitDetector)
    {
        m_MineOverviewDetector = mineOverviewDetector;
        m_AsteroidRowsDetector = asteroidRowsDetector;
        m_FirstAsteroidDistanceUnitDetector = firstAsteroidDistanceUnitDetector;
    }

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.ApproachingAsteroid;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        context.AutomationInputController.PressKey(VirtualKeyF4, cancellationToken);

        var capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(CaptureSuffix);
        using var initialScreen = Cv2.ImRead(capturePath);
        if (!m_MineOverviewDetector.TryLocate(initialScreen, out var initialMineOverviewBounds))
        {
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Recover,
                capturePath);
        }

        var initialAsteroids = m_AsteroidRowsDetector.Locate(initialScreen, initialMineOverviewBounds);
        if (initialAsteroids.Count == 0)
        {
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Recover,
                capturePath);
        }

        context.ClickUiElement(Center(initialAsteroids[0].Bounds), cancellationToken);
        context.AutomationInputController.PressKey(VirtualKeyA, cancellationToken);

        var analysis = AsteroidBeltLandingAnalysis.NotFound;
        for (var attempt = 0; attempt < DistancePollingAttemptCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(CaptureSuffix);
            using var screen = Cv2.ImRead(capturePath);
            if (!m_MineOverviewDetector.TryLocate(screen, out var mineOverviewBounds))
            {
                return new MiningAutomationStateTransition(
                    Kind,
                    MiningAutomationStateKind.Recovery,
                    MiningAutomationActionKind.Recover,
                    capturePath);
            }

            var asteroids = m_AsteroidRowsDetector.Locate(screen, mineOverviewBounds);
            if (asteroids.Count == 0)
            {
                return new MiningAutomationStateTransition(
                    Kind,
                    MiningAutomationStateKind.Recovery,
                    MiningAutomationActionKind.Recover,
                    capturePath);
            }

            var firstAsteroidDistanceUnit = m_FirstAsteroidDistanceUnitDetector.Detect(screen, mineOverviewBounds, asteroids[0].Bounds);
            analysis = new AsteroidBeltLandingAnalysis(
                LandedOnAsteroidBelt: true,
                AsteroidBeltLabelBounds: null,
                MineOverviewBounds: mineOverviewBounds,
                Asteroids: asteroids,
                NothingFoundDetected: false,
                FirstAsteroidDistanceUnit: firstAsteroidDistanceUnit);

            if (firstAsteroidDistanceUnit == DistanceUnitKind.Meters)
            {
                context.AutomationInputController.PressKey(VirtualKeyControl, cancellationToken);
                context.AutomationInputController.Delay(LockAsteroidDelayMilliseconds, cancellationToken);
                context.AutomationInputController.PressKey(VirtualKeyF1, cancellationToken);
                context.AutomationInputController.Delay(BeforeSecondLaserDelayMilliseconds, cancellationToken);
                context.AutomationInputController.PressKey(VirtualKeyF2, cancellationToken);
                return new MiningAutomationStateTransition(
                    Kind,
                    MiningAutomationStateKind.Mining,
                    MiningAutomationActionKind.ActivateMiningLasers,
                    capturePath,
                    AsteroidBeltLanding: analysis);
            }

            context.AutomationInputController.Delay(DistancePollingMilliseconds, cancellationToken);
        }

        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Recovery,
            MiningAutomationActionKind.Recover,
            capturePath,
            AsteroidBeltLanding: analysis);
    }

    private static Point Center(Rect bounds)
    {
        return new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
    }
}
