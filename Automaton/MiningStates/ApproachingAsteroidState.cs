using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using OpenCvSharp;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class ApproachingAsteroidState(
    IAutomationInputController automationInputController,
    MineOverviewDetector mineOverviewDetector,
    IFirstAsteroidWithinReachDetector firstAsteroidWithinReachDetector,
    ILogger? logger = null)
    : IMiningAutomationState
{
    private readonly ILogger m_Logger = logger ?? Log.ForContext<ApproachingAsteroidState>();

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.ApproachingAsteroid;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        m_Logger.Debug("Executing {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();

        // Activate propulsion module
        automationInputController.PressKey(VirtualKeys.F4, cancellationToken);

        var capture = context.ScreenCaptureService.CaptureCurrentScreen(Settings.ApproachingAsteroidCaptureSuffix);

        // Failed to detect Mine overview tab
        if (!mineOverviewDetector.TryLocate(capture.Image, out var mineOverviewBounds))
        {
            m_Logger.Error("Failed to detect Mine overview tab");
            var result = new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Recover,
                capture.CapturePath);
            capture.Dispose();
            return result;
        }

        // Asteroid belt is empty
        if (NothingFoundDetector.Detect(capture.Image, mineOverviewBounds))
        {
            m_Logger.Error("Asteroid belt is empty");
            var result = new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Recover,
                capture.CapturePath);
            capture.Dispose();
            return result;
        }

        var asteroids = AsteroidRowsDetector.Locate(capture.Image, mineOverviewBounds);
        if (asteroids.Count == 0)
        {
            m_Logger.Error("Failed to detect asteroid rows in MINE overview");
            var result = new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Recover,
                capture.CapturePath);
            capture.Dispose();
            return result;
        }

        // Select nearest asteroid
        automationInputController.ClickUiElement(GeometryHelper.Center(asteroids[0].Bounds), cancellationToken);
        // Approach
        automationInputController.PressKey(VirtualKeys.A, cancellationToken);
        capture.Dispose();

        m_Logger.Information("Approaching nearest asteroid...");
        // Approach asteroid until the distance becomes less than 10 km
        for (var attempt = 0; attempt < Settings.ApproachingAsteroidDistancePollingAttemptCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            capture = context.ScreenCaptureService.CaptureCurrentScreen(Settings.ApproachingAsteroidCaptureSuffix);
            var currentAsteroids = AsteroidRowsDetector.Locate(capture.Image, mineOverviewBounds);
            var firstAsteroidRowBounds = currentAsteroids.Count > 0
                ? currentAsteroids[0].Bounds
                : asteroids[0].Bounds;

            var nearestAsteroidWithinReach = firstAsteroidWithinReachDetector.Detect(
                capture.Image,
                mineOverviewBounds,
                firstAsteroidRowBounds,
                out var distanceTelemetry);
            AnnotateDistanceDetectionCapture(capture.CapturePath, capture.Image, distanceTelemetry.RowSearchBounds, distanceTelemetry.SearchBounds);
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
                automationInputController.PressKey(VirtualKeys.Control, cancellationToken);
                // Wait for target lock
                automationInputController.Delay(Delays.LockAsteroidMs, cancellationToken);
                // Activate first laser
                automationInputController.PressKey(VirtualKeys.F1, cancellationToken);
                // Activate second laser
                automationInputController.PressKey(VirtualKeys.F2, cancellationToken);

                var result = new MiningAutomationStateTransition(
                    Kind,
                    MiningAutomationStateKind.Mining,
                    MiningAutomationActionKind.ActivateMiningLasers,
                    capture.CapturePath);
                capture.Dispose();
                return result;
            }

            capture.Dispose();
            automationInputController.Delay(Delays.ApproachAsteroidDistancePollingMs, cancellationToken);
        }

        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Recovery,
            MiningAutomationActionKind.Recover,
            capture.CapturePath);
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

}
