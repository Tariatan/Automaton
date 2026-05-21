using Automaton.Detectors;
using Automaton.Primitives;
using OpenCvSharp;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class ApproachingAsteroidState : IMiningAutomationState
{
    private readonly MineOverviewDetector m_MineOverviewDetector;
    private readonly FirstAsteroidWithinReachDetector m_FirstAsteroidWithinReachDetector;
    private readonly ILogger m_Logger;

    public ApproachingAsteroidState()
        : this(
            new MineOverviewDetector(),
            new FirstAsteroidWithinReachDetector(),
            Log.ForContext<ApproachingAsteroidState>())
    {
    }

    private ApproachingAsteroidState(
        MineOverviewDetector mineOverviewDetector,
        FirstAsteroidWithinReachDetector firstAsteroidWithinReachDetector,
        ILogger? logger = null)
    {
        m_MineOverviewDetector = mineOverviewDetector;
        m_FirstAsteroidWithinReachDetector = firstAsteroidWithinReachDetector;
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
        context.AutomationInputController.PressKey(VirtualKeys.F4, cancellationToken);

        var capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(Settings.ApproachingAsteroidCaptureSuffix);
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
        if (NothingFoundDetector.Detect(initialScreen, mineOverviewBounds))
        {
            m_Logger.Error("Asteroid belt is empty");
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Recover,
                capturePath);
        }

        var asteroids = AsteroidRowsDetector.Locate(initialScreen, mineOverviewBounds);
        if (asteroids.Count == 0)
        {
            m_Logger.Error("Failed to detect asteroid rows in MINE overview");
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Recover,
                capturePath);
        }

        // Select nearest asteroid
        context.ClickUiElement(Center(asteroids[0].Bounds), cancellationToken);
        // Approach
        context.AutomationInputController.PressKey(VirtualKeys.A, cancellationToken);

        m_Logger.Information("Approaching nearest asteroid...");
        // Approach asteroid until the distance becomes less than 10 km
        for (var attempt = 0; attempt < Settings.ApproachingAsteroidDistancePollingAttemptCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(Settings.ApproachingAsteroidCaptureSuffix);
            using var screen = Cv2.ImRead(capturePath);
            var currentAsteroids = AsteroidRowsDetector.Locate(screen, mineOverviewBounds);
            var firstAsteroidRowBounds = currentAsteroids.Count > 0
                ? currentAsteroids[0].Bounds
                : asteroids[0].Bounds;

            var nearestAsteroidWithinReach = m_FirstAsteroidWithinReachDetector.Detect(
                screen,
                mineOverviewBounds,
                firstAsteroidRowBounds,
                out var distanceTelemetry);
            AnnotateDistanceDetectionCapture(capturePath, screen, distanceTelemetry.RowSearchBounds, distanceTelemetry.SearchBounds);
            m_Logger.Information(
                "Distance unit detection. Attempt={Attempt}/{MaxAttempts}, CurrentAsteroidCount={CurrentAsteroidCount}, FirstAsteroidRowBounds={FirstAsteroidRowBounds}, RowSearchBounds={RowSearchBounds}, UnitSearchBounds={UnitSearchBounds}, IsMetersTemplateMatch={IsMetersTemplateMatch}, BestMetersScore={BestMetersScore}, MatchedMetersScale={MatchedMetersScale}, nearestAsteroidWithinReach={NearestAsteroidWithinReach}",
                attempt + 1,
                Settings.ApproachingAsteroidDistancePollingAttemptCount,
                currentAsteroids.Count,
                firstAsteroidRowBounds,
                distanceTelemetry.RowSearchBounds,
                distanceTelemetry.SearchBounds,
                distanceTelemetry.IsMetersTemplateMatch,
                distanceTelemetry.BestMetersScore,
                distanceTelemetry.MatchedMetersScale,
                nearestAsteroidWithinReach);

            // Nearest asteroid is within reach
            if (nearestAsteroidWithinReach)
            {
                m_Logger.Information("Distance to asteroid decreased below 10 km => locking target and activating lasers");

                // Target the asteroid
                context.AutomationInputController.PressKey(VirtualKeys.Control, cancellationToken);
                // Wait for target lock
                context.AutomationInputController.Delay(Delays.LockAsteroidMs, cancellationToken);
                // Activate first laser
                context.AutomationInputController.PressKey(VirtualKeys.F1, cancellationToken);
                // Activate second laser
                context.AutomationInputController.PressKey(VirtualKeys.F2, cancellationToken);

                return new MiningAutomationStateTransition(
                    Kind,
                    MiningAutomationStateKind.Mining,
                    MiningAutomationActionKind.ActivateMiningLasers,
                    capturePath);
            }

            context.AutomationInputController.Delay(Delays.ApproachAsteroidDistancePollingMs, cancellationToken);
        }

        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Recovery,
            MiningAutomationActionKind.Recover,
            capturePath);
    }

    private static void AnnotateDistanceDetectionCapture(
        string capturePath,
        Mat screen,
        Rect? rowSearchBounds,
        Rect? unitSearchBounds)
    {
        using var annotated = screen.Clone();
        if (rowSearchBounds.HasValue)
        {
            Cv2.Rectangle(annotated, rowSearchBounds.Value, new Scalar(0, 255, 0), 2);
        }

        if (unitSearchBounds.HasValue)
        {
            Cv2.Rectangle(annotated, unitSearchBounds.Value, new Scalar(0, 255, 255), 2);
        }

        Cv2.ImWrite(capturePath, annotated);
    }

    private static Point Center(Rect bounds) => new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
}
