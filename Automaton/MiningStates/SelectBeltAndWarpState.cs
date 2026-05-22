using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using OpenCvSharp;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class SelectBeltAndWarpState(
    IAutomationInputController automationInputController,
    AsteroidBeltOverviewDetector beltOverviewDetector,
    MineOverviewDetector mineOverviewDetector,
    Func<int, int> nextRandomIndex,
    ILogger? logger = null)
    : IMiningAutomationState
{
    private const string CaptureSuffix = ".mining-select-belt-and-warp";
    private const string LandingCaptureSuffix = ".mining-landed-on-asteroid-belt";
    private const int LandingPollingAttemptCount = 60;

    private readonly ILogger m_Logger = logger ?? Log.ForContext<SelectBeltAndWarpState>();

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.SelectBeltAndWarp;

    public MiningAutomationStateTransition Execute(MiningAutomationContext context, CancellationToken cancellationToken)
    {
        m_Logger.Debug("Executing {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();
        var capture = context.ScreenCaptureService.CaptureCurrentScreen(CaptureSuffix);
        var analysis = Analyze(capture.Image);
        m_Logger.Information(
            "Belt overview analysis (initial). OverviewLocated={OverviewLocated}, OverviewBounds={OverviewBounds}, OverviewBeltButtonBounds={OverviewBeltButtonBounds}, HomeStationBounds={HomeStationBounds}, BeltCount={BeltCount}",
            analysis.OverviewLocated,
            analysis.OverviewBounds,
            analysis.OverviewBeltButtonBounds,
            analysis.HomeStationBounds,
            analysis.AsteroidBelts.Count);
        // Failed to detect Belt overview tab
        if (!analysis.OverviewLocated || analysis.OverviewBeltButtonBounds is null)
        {
            m_Logger.Error("Failed to detect Belt overview tab");
            var result = Recover(capture.CapturePath);
            capture.Dispose();
            return result;
        }

        // Failed to detect Home Station in the Belt overview
        if (analysis.HomeStationBounds is null)
        {
            m_Logger.Error("Failed to detect Home Station in the Belt overview");
            automationInputController.QuitGame(cancellationToken);
            var result = Recover(capture.CapturePath);
            capture.Dispose();
            return result;
        }

        // Select Belt overview tab
        automationInputController.ClickUiElement(GeometryHelper.Center(analysis.OverviewBeltButtonBounds.Value), cancellationToken);
        capture.Dispose();

        capture = context.ScreenCaptureService.CaptureCurrentScreen(CaptureSuffix);
        analysis = Analyze(capture.Image);
        m_Logger.Information(
            "Belt overview analysis (after tab click). OverviewLocated={OverviewLocated}, OverviewBounds={OverviewBounds}, OverviewBeltButtonBounds={OverviewBeltButtonBounds}, HomeStationBounds={HomeStationBounds}, BeltCount={BeltCount}",
            analysis.OverviewLocated,
            analysis.OverviewBounds,
            analysis.OverviewBeltButtonBounds,
            analysis.HomeStationBounds,
            analysis.AsteroidBelts.Count);

        // Failed to detect any belts
        if (analysis.AsteroidBelts.Count == 0)
        {
            m_Logger.Error("Failed to detect any belts");
            var result = Recover(capture.CapturePath);
            capture.Dispose();
            return result;
        }

        var availableAsteroidBelts = analysis.AsteroidBelts
            .Where(asteroidBelt => !context.IsAsteroidBeltBlacklisted(asteroidBelt.Bounds))
            .ToArray();
        if (availableAsteroidBelts.Length == 0)
        {
            m_Logger.Error(
                "No asteroid belts available after blacklist filtering. TotalBelts={TotalBelts}, BlacklistedBeltCount={BlacklistedBeltCount}",
                analysis.AsteroidBelts.Count,
                context.BlacklistedAsteroidBeltCount);
            var result = Recover(capture.CapturePath);
            capture.Dispose();
            return result;
        }

        var requestedAsteroidBeltIndex = nextRandomIndex(availableAsteroidBelts.Length);
        var selectedAsteroidBeltIndex = Math.Clamp(requestedAsteroidBeltIndex, 0, availableAsteroidBelts.Length - 1);
        var selectedAsteroidBeltDisplayIndex = selectedAsteroidBeltIndex + 1;
        var selectedAsteroidBelt = availableAsteroidBelts[selectedAsteroidBeltIndex];
        context.SetCurrentAsteroidBelt(selectedAsteroidBelt.Bounds);

        // Select asteroid belt
        automationInputController.ClickUiElement(GeometryHelper.Center(selectedAsteroidBelt.Bounds), cancellationToken);
        // Warp to asteroid belt
        automationInputController.PressKey(VirtualKeys.S, cancellationToken);
        capture.Dispose();

        m_Logger.Information(
            "Warp to asteroid belt. RequestedIndexZeroBased={RequestedIndexZeroBased}, SelectedIndexZeroBased={SelectedIndexZeroBased}, SelectedIndexOneBased={SelectedIndexOneBased}, DetectedBeltCount={DetectedBeltCount}, MinIndexZeroBased={MinIndexZeroBased}, MaxIndexZeroBased={MaxIndexZeroBased}",
            requestedAsteroidBeltIndex,
            selectedAsteroidBeltIndex,
            selectedAsteroidBeltDisplayIndex,
            availableAsteroidBelts.Length,
            0,
            availableAsteroidBelts.Length - 1);

        // Wait until landed on asteroid belt with 1 second interval
        for (var attempt = 0; attempt < LandingPollingAttemptCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            capture = context.ScreenCaptureService.CaptureCurrentScreen(LandingCaptureSuffix);
            var landingAnalysis = AsteroidBeltLandingDetector.Analyze(capture.Image);

            // Landed on asteroid belt
            if (landingAnalysis.LandedOnAsteroidBelt)
            {
                m_Logger.Information("Landed on asteroid belt");

                var mineOverviewLocated = mineOverviewDetector.TryLocate(capture.Image, out var mineOverviewBounds);
                var nothingFoundDetected = mineOverviewLocated &&
                                           NothingFoundDetector.Detect(capture.Image, mineOverviewBounds);
                if (nothingFoundDetected)
                {
                    context.BlacklistAsteroidBelt(selectedAsteroidBelt.Bounds);
                    m_Logger.Warning(
                        "Nothing Found detected in MINE overview. Blacklisting asteroid belt and selecting another. BlacklistedBeltCount={BlacklistedBeltCount}, BlacklistedBeltBounds={BlacklistedBeltBounds}",
                        context.BlacklistedAsteroidBeltCount,
                        selectedAsteroidBelt.Bounds);
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

        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Recovery,
            MiningAutomationActionKind.Recover,
            capture.CapturePath);
    }

    private AsteroidBeltOverviewAnalysis Analyze(Mat screen)
    {
        return beltOverviewDetector.Analyze(screen);
    }

    private MiningAutomationStateTransition Recover(string capturePath)
    {
        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Recovery,
            MiningAutomationActionKind.QuitGameFromSpace,
            capturePath);
    }

}
