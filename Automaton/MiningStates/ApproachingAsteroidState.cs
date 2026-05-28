using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using OpenCvSharp;
using Serilog;
using System.IO;

namespace Automaton.MiningStates;

internal sealed class ApproachingAsteroidState(
    IAutomationInputController automationInputController,
    MineOverviewDetector mineOverviewDetector,
    IFirstAsteroidWithinReachDetector firstAsteroidWithinReachDetector)
    : IMiningAutomationState
{
    private readonly ILogger m_Logger = Log.ForContext<ApproachingAsteroidState>();

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
        var mineOverviewAnalysis = AnalyzeMineOverview(capture.CapturePath, capture.Image);

        // Failed to detect Mine overview tab
        if (!mineOverviewAnalysis.MineOverviewLocated || mineOverviewAnalysis.MineOverviewBounds is null)
        {
            m_Logger.Error("Failed to detect Mine overview tab");
            var result = Recover(capture.CapturePath);
            capture.Dispose();
            return result;
        }
        var mineOverviewBounds = mineOverviewAnalysis.MineOverviewBounds.Value;

        // Asteroid belt is empty
        if (NothingFoundDetector.Detect(capture.Image, mineOverviewBounds))
        {
            m_Logger.Error("Asteroid belt is empty");
            var result = Recover(capture.CapturePath);
            capture.Dispose();
            return result;
        }

        var asteroids = AsteroidRowsDetector.Detect(capture.Image, mineOverviewBounds);
        if (asteroids.Count == 0)
        {
            m_Logger.Error("Failed to detect asteroid rows in MINE overview");
            var result = Recover(capture.CapturePath);
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
            var currentAsteroids = AsteroidRowsDetector.Detect(capture.Image, mineOverviewBounds);
            var firstAsteroidRowBounds = currentAsteroids.Count > 0
                ? currentAsteroids[0].Bounds
                : asteroids[0].Bounds;

            var nearestAsteroidWithinReach = firstAsteroidWithinReachDetector.Detect(
                capture.Image,
                mineOverviewBounds,
                firstAsteroidRowBounds,
                out var distanceTelemetry);
            AnnotateDistanceDetectionCapture(capture.CapturePath, capture.Image, distanceTelemetry.RowSearchBounds, distanceTelemetry.SearchBounds);
            m_Logger.Information("Asteroid within reach detection. Attempt={Attempt}/{MaxAttempts}", attempt + 1, Settings.ApproachingAsteroidDistancePollingAttemptCount);

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

        return Recover(capture.CapturePath);
    }

    private MiningAutomationStateTransition Recover(string capturePath)
    {
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

    private MineOverviewAnalysis AnalyzeMineOverview(string capturePath, Mat screen)
    {
        if (File.Exists(capturePath))
        {
            return mineOverviewDetector.Detect(capturePath);
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"automaton-approaching-mine-overview-{Guid.NewGuid():N}.png");
        try
        {
            Cv2.ImWrite(tempPath, screen);
            return mineOverviewDetector.Detect(tempPath);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
