using Automaton.Detectors;
using OpenCvSharp;
using Serilog;

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
    private readonly ILogger m_Logger;

    public ApproachingAsteroidState()
        : this(new MineOverviewDetector(), new AsteroidRowsDetector(), new FirstAsteroidDistanceUnitDetector(), Log.ForContext<ApproachingAsteroidState>())
    {
    }

    internal ApproachingAsteroidState(
        MineOverviewDetector mineOverviewDetector,
        AsteroidRowsDetector asteroidRowsDetector,
        FirstAsteroidDistanceUnitDetector firstAsteroidDistanceUnitDetector,
        ILogger? logger = null)
    {
        m_MineOverviewDetector = mineOverviewDetector;
        m_AsteroidRowsDetector = asteroidRowsDetector;
        m_FirstAsteroidDistanceUnitDetector = firstAsteroidDistanceUnitDetector;
        m_Logger = logger ?? Log.ForContext<ApproachingAsteroidState>();
    }

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.ApproachingAsteroid;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        m_Logger.Debug("Executing {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();

        // Activate propulsion module
        context.AutomationInputController.PressKey(VirtualKeyF4, cancellationToken);

        var capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(CaptureSuffix);
        using var initialScreen = Cv2.ImRead(capturePath);

        // Failed to detect Mine overview tab
        if (!m_MineOverviewDetector.TryLocate(initialScreen, out var mineOverviewBounds))
        {
            m_Logger.Error("Failed to detect Mine overview tab");
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Recover,
                capturePath);
        }

        // Asteroid belt is empty
        if (m_MineOverviewDetector.DetectNothingFound(initialScreen, mineOverviewBounds))
        {
            m_Logger.Error("Asteroid belt is empty");
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Recover,
                capturePath);
        }

        var asteroids = m_AsteroidRowsDetector.Locate(initialScreen, mineOverviewBounds);

        // Select nearest asteroid
        context.ClickUiElement(Center(asteroids[0].Bounds), cancellationToken);
        // Approach
        context.AutomationInputController.PressKey(VirtualKeyA, cancellationToken);

        var analysis = AsteroidBeltLandingAnalysis.NotFound;

        m_Logger.Information("Approaching nearest asteroid...");
        // Approach asteroid until the distance becomes less than 10 km
        for (var attempt = 0; attempt < DistancePollingAttemptCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(CaptureSuffix);
            using var screen = Cv2.ImRead(capturePath);

            var nearestAsteroidDistanceUnit = m_FirstAsteroidDistanceUnitDetector.Detect(screen, mineOverviewBounds, asteroids[0].Bounds);
            analysis = new AsteroidBeltLandingAnalysis(
                LandedOnAsteroidBelt: true,
                AsteroidBeltLabelBounds: null,
                MineOverviewBounds: mineOverviewBounds,
                Asteroids: asteroids,
                NothingFoundDetected: false,
                FirstAsteroidDistanceUnit: nearestAsteroidDistanceUnit);

            // Distance decreased below 10 km
            if (nearestAsteroidDistanceUnit == DistanceUnitKind.Meters)
            {
                m_Logger.Information("Distance to asteroid decreased below 10 km => locking target and activating lasers");

                // Target the asteroid
                context.AutomationInputController.PressKey(VirtualKeyControl, cancellationToken);
                // Wait for target lock
                context.AutomationInputController.Delay(LockAsteroidDelayMilliseconds, cancellationToken);
                // Activate first laser
                context.AutomationInputController.PressKey(VirtualKeyF1, cancellationToken);
                // Wait
                context.AutomationInputController.Delay(BeforeSecondLaserDelayMilliseconds, cancellationToken);
                // Activate second laser
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

    private static Point Center(Rect bounds) => new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
}
