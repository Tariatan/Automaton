using System.IO;
using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using OpenCvSharp;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class SelectBeltAndWarpState(
    IAutomationInputController automationInputController,
    IGameActionService gameActionService,
    AsteroidBeltOverviewDetector beltOverviewDetector,
    MineOverviewDetector mineOverviewDetector,
    WarOverviewDetector warOverviewDetector,
    Func<int, int> nextRandomIndex)
    : IMiningAutomationState
{
    private const string CaptureSuffix = ".mining-select-belt-and-warp";
    private const string LandingCaptureSuffix = ".mining-landed-on-asteroid-belt";
    private const int LandingPollingAttemptCount = 60;

    private readonly ILogger m_Logger = Log.ForContext<SelectBeltAndWarpState>();

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.SelectBeltAndWarp;

    public MiningAutomationStateTransition Execute(MiningAutomationContext context, CancellationToken cancellationToken)
    {
        m_Logger.Information("Executing {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();
        var capture = context.ScreenCaptureService.CaptureCurrentScreen(CaptureSuffix);
        var analysis = Analyze(capture.CapturePath, capture.Image);

        // Failed to detect Belt overview tab
        if (!analysis.OverviewLocated || analysis.OverviewBeltButtonBounds is null)
        {
            m_Logger.Error("Failed to detect Belt overview tab");
            var result = Recover(capture.CapturePath, MiningAutomationFailureReason.DetectionMiss);
            capture.Dispose();
            return result;
        }

        // Failed to detect Home Station in the Belt overview
        if (!analysis.HomeStationLocated)
        {
            m_Logger.Error("Failed to detect Home Station in the Belt overview");
            gameActionService.QuitGame(cancellationToken);
            var result = Recover(capture.CapturePath);
            capture.Dispose();
            return result;
        }

        // Select Belt overview tab
        automationInputController.ClickUiElement(GeometryHelper.Center(analysis.OverviewBeltButtonBounds.Value), cancellationToken);
        capture.Dispose();

        capture = context.ScreenCaptureService.CaptureCurrentScreen(CaptureSuffix);
        analysis = Analyze(capture.CapturePath, capture.Image);
        m_Logger.Information("Belt overview detected with {BeltCount} belts", analysis.AsteroidBelts.Count);

        // Failed to detect any belts
        if (analysis.AsteroidBelts.Count == 0)
        {
            m_Logger.Error("Failed to detect any belts");
            var result = Recover(capture.CapturePath, MiningAutomationFailureReason.DetectionMiss);
            capture.Dispose();
            return result;
        }

        var availableAsteroidBelts = analysis.AsteroidBelts
            .Where(asteroidBelt => !context.IsAsteroidBeltBlacklisted(asteroidBelt.Bounds))
            .ToArray();
        if (availableAsteroidBelts.Length == 0)
        {
            var blacklistedAsteroidBeltIndexes = analysis.AsteroidBelts
                .Select((asteroidBelt, index) => new { asteroidBelt.Bounds, Index = index + 1 })
                .Where(asteroidBelt => context.IsAsteroidBeltBlacklisted(asteroidBelt.Bounds))
                .Select(asteroidBelt => asteroidBelt.Index)
                .ToArray();
            m_Logger.Error(
                "No asteroid belts available after blacklist filtering. TotalBelts={TotalBelts}, BlacklistedBeltCount={BlacklistedBeltCount}, BlacklistedBeltIndexes={BlacklistedBeltIndexes}",
                analysis.AsteroidBelts.Count,
                context.BlacklistedAsteroidBeltCount,
                string.Join(",", blacklistedAsteroidBeltIndexes));
            var result = new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.QuitGameAndExitApplication,
                capture.CapturePath);
            capture.Dispose();
            return result;
        }

        var requestedAsteroidBeltIndex = nextRandomIndex(availableAsteroidBelts.Length);
        var selectedAsteroidBeltIndex = Math.Clamp(requestedAsteroidBeltIndex, 0, availableAsteroidBelts.Length - 1);
        var selectedAsteroidBelt = availableAsteroidBelts[selectedAsteroidBeltIndex];
        var selectedDetectedAsteroidBeltIndex = analysis.AsteroidBelts
            .Select((asteroidBelt, index) => new { AsteroidBelt = asteroidBelt, Index = index + 1 })
            .First(asteroidBelt => asteroidBelt.AsteroidBelt == selectedAsteroidBelt)
            .Index;
        context.SetCurrentAsteroidBelt(selectedAsteroidBelt.Bounds);

        // Select asteroid belt
        automationInputController.ClickUiElement(GeometryHelper.Center(selectedAsteroidBelt.Bounds), cancellationToken);
        // Warp to asteroid belt
        gameActionService.WarpToTarget(cancellationToken);
        capture.Dispose();

        m_Logger.Information("Warp to asteroid belt {SelectedIndexBased} / {DetectedBeltCount}", selectedAsteroidBeltIndex + 1, availableAsteroidBelts.Length);

        // Wait until landed on asteroid belt with 1 second interval
        for (var attempt = 0; attempt < LandingPollingAttemptCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            capture = context.ScreenCaptureService.CaptureCurrentScreen(LandingCaptureSuffix);
            var landingAnalysis = AsteroidBeltLandingDetector.Detect(capture.Image);

            // Landed on asteroid belt
            if (landingAnalysis.LandedOnAsteroidBelt)
            {
                m_Logger.Information("Landed on asteroid belt => detecting asteroids");

                using var warDetectionImage = capture.Image.Clone();
                var warAnalysis = warOverviewDetector.Detect(warDetectionImage);
                DrawWarOverviewOverlay(warDetectionImage, warAnalysis);
                if (warAnalysis is { WarOverviewLocated: true, WarOverviewBounds: not null })
                {
                    var warOverviewNothingFound = NothingFoundDetector.Detect(warDetectionImage, warAnalysis.WarOverviewBounds.Value);
                    if (!warOverviewNothingFound)
                    {
                        context.BlacklistAsteroidBelt(selectedAsteroidBelt.Bounds);
                        m_Logger.Error(
                            "WAR overview is active and not empty in SelectBeltAndWarpState => GTFO docking. BlacklistedBeltCount={BlacklistedBeltCount}, BlacklistedBeltIndex={BlacklistedBeltIndex}",
                            context.BlacklistedAsteroidBeltCount,
                            selectedDetectedAsteroidBeltIndex);
                        var gtfoResult = new MiningAutomationStateTransition(
                            Kind,
                            MiningAutomationStateKind.Dock,
                            MiningAutomationActionKind.None,
                            capture.CapturePath);
                        capture.Dispose();
                        return gtfoResult;
                    }
                }

                var mineOverviewAnalysis = mineOverviewDetector.Detect(capture.Image);
                var nothingFoundDetected = mineOverviewAnalysis is { MineOverviewLocated: true, MineOverviewBounds: not null } &&
                                           NothingFoundDetector.Detect(capture.Image, mineOverviewAnalysis.MineOverviewBounds.Value);
                if (nothingFoundDetected)
                {
                    context.BlacklistAsteroidBelt(selectedAsteroidBelt.Bounds);
                    m_Logger.Warning(
                        "Nothing Found detected in MINE overview. Blacklisting asteroid belt and selecting another. BlacklistedBeltCount={BlacklistedBeltCount}, BlacklistedBeltIndex={BlacklistedBeltIndex}",
                        context.BlacklistedAsteroidBeltCount,
                        selectedDetectedAsteroidBeltIndex);
                    var blacklistResult = new MiningAutomationStateTransition(
                        Kind,
                        MiningAutomationStateKind.SelectBeltAndWarp,
                        MiningAutomationActionKind.WarpToAsteroidField,
                        capture.CapturePath);
                    capture.Dispose();
                    return blacklistResult;
                }

                var landedResult = new MiningAutomationStateTransition(
                    Kind,
                    MiningAutomationStateKind.ApproachingAsteroid,
                    MiningAutomationActionKind.WarpToAsteroidField,
                    capture.CapturePath);
                capture.Dispose();
                return landedResult;
            }

            capture.Dispose();
            automationInputController.Delay(Delays.LandingPollingMs, cancellationToken);
        }

        return Recover(capture.CapturePath, MiningAutomationFailureReason.DetectionMiss);
    }

    private AsteroidBeltOverviewAnalysis Analyze(string capturePath, Mat screen)
    {
        var analysis = beltOverviewDetector.Detect(screen);

        if (File.Exists(capturePath))
        {
            DrawDebugOverlay(screen, analysis);
            Cv2.ImWrite(capturePath, screen);
        }

        return analysis;
    }

    private static void DrawDebugOverlay(Mat image, AsteroidBeltOverviewAnalysis analysis)
    {
        var items = new List<(Rect, OverlayColor)>();

        if (analysis.OverviewBounds is not null)
        {
            items.Add((analysis.OverviewBounds.Value, OverlayColor.LightBlue));
        }

        if (analysis.OverviewBeltButtonBounds is not null)
        {
            items.Add((analysis.OverviewBeltButtonBounds.Value, OverlayColor.RedOrange));
        }

        if (analysis.HomeStationBounds is not null)
        {
            items.Add((analysis.HomeStationBounds.Value, OverlayColor.Green));
        }

        items.AddRange(analysis.AsteroidBelts.Select(asteroidBelt => (asteroidBelt.Bounds, OverlayColor.Amber)));

        DebugOverlay.Annotate(image, items.ToArray());
        DebugOverlay.Label(
            image,
            $"Overview {(analysis.OverviewLocated ? "found" : "not found")}; Belts: {analysis.AsteroidBelts.Count}",
            OverlayColor.RedOrange);
    }

    private static void DrawWarOverviewOverlay(Mat image, WarOverviewAnalysis analysis)
    {
        if (analysis.WarOverviewBounds is not null)
        {
            DebugOverlay.Annotate(image, (analysis.WarOverviewBounds.Value, OverlayColor.RedOrange));
        }
    }

    private MiningAutomationStateTransition Recover(string capturePath, MiningAutomationFailureReason failureReason = MiningAutomationFailureReason.None)
    {
        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Recovery,
            MiningAutomationActionKind.Recover,
            capturePath)
        {
            FailureReason = failureReason
        };
    }
}
