using Automaton.Detectors;
using OpenCvSharp;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class SelectBeltAndWarpState : IMiningAutomationState
{
    private const string CaptureSuffix = ".mining-select-belt-and-warp";
    private const string LandingCaptureSuffix = ".mining-landed-on-asteroid-belt";
    private const int LandingPollingMilliseconds = 1_000;
    private const int LandingPollingAttemptCount = 60;
    private const ushort VirtualKeyS = 0x53;

    private readonly AsteroidBeltOverviewDetector m_BeltOverviewDetector;
    private readonly AsteroidBeltLandingDetector m_LandingDetector;
    private readonly MineOverviewDetector m_MineOverviewDetector;
    private readonly NothingFoundDetector m_NothingFoundDetector;
    private readonly Func<int, int> m_NextRandomIndex;
    private readonly ILogger m_Logger;

    public SelectBeltAndWarpState()
        : this(
            new AsteroidBeltOverviewDetector(),
            new AsteroidBeltLandingDetector(),
            new MineOverviewDetector(),
            new NothingFoundDetector(),
            Random.Shared.Next,
            Log.ForContext<SelectBeltAndWarpState>())
    {
    }

    internal SelectBeltAndWarpState(
        AsteroidBeltOverviewDetector beltOverviewDetector,
        AsteroidBeltLandingDetector landingDetector,
        MineOverviewDetector mineOverviewDetector,
        NothingFoundDetector nothingFoundDetector,
        Func<int, int> nextRandomIndex,
        ILogger? logger = null)
    {
        m_BeltOverviewDetector = beltOverviewDetector;
        m_LandingDetector = landingDetector;
        m_MineOverviewDetector = mineOverviewDetector;
        m_NothingFoundDetector = nothingFoundDetector;
        m_NextRandomIndex = nextRandomIndex;
        m_Logger = logger ?? Log.ForContext<SelectBeltAndWarpState>();
    }

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.SelectBeltAndWarp;

    public MiningAutomationStateTransition Execute(MiningAutomationContext context, CancellationToken cancellationToken)
    {
        m_Logger.Debug("Executing {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();
        var capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(CaptureSuffix);
        var analysis = Analyze(capturePath);
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
            return Recover(capturePath, analysis);
        }

        // Select Belt overview tab
        context.ClickUiElement(Center(analysis.OverviewBeltButtonBounds.Value), cancellationToken);

        capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(CaptureSuffix);
        analysis = Analyze(capturePath);
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
            return Recover(capturePath, analysis);
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
            return Recover(capturePath, analysis);
        }

        var requestedAsteroidBeltIndex = m_NextRandomIndex(availableAsteroidBelts.Length);
        var selectedAsteroidBeltIndex = Math.Clamp(requestedAsteroidBeltIndex, 0, availableAsteroidBelts.Length - 1);
        var selectedAsteroidBeltDisplayIndex = selectedAsteroidBeltIndex + 1;
        var selectedAsteroidBelt = availableAsteroidBelts[selectedAsteroidBeltIndex];
        context.SetCurrentAsteroidBelt(selectedAsteroidBelt.Bounds);

        // Select asteroid belt
        context.ClickUiElement(Center(selectedAsteroidBelt.Bounds), cancellationToken);
        // Warp to asteroid belt
        context.AutomationInputController.PressKey(VirtualKeyS, cancellationToken);

        m_Logger.Information(
            "Warp to asteroid belt. RequestedIndexZeroBased={RequestedIndexZeroBased}, SelectedIndexZeroBased={SelectedIndexZeroBased}, SelectedIndexOneBased={SelectedIndexOneBased}, DetectedBeltCount={DetectedBeltCount}, MinIndexZeroBased={MinIndexZeroBased}, MaxIndexZeroBased={MaxIndexZeroBased}",
            requestedAsteroidBeltIndex,
            selectedAsteroidBeltIndex,
            selectedAsteroidBeltDisplayIndex,
            availableAsteroidBelts.Length,
            0,
            availableAsteroidBelts.Length - 1);

        AsteroidBeltLandingAnalysis? landingAnalysis = null;

        // Wait until landed on asteroid belt with 1 second interval
        for (var attempt = 0; attempt < LandingPollingAttemptCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(LandingCaptureSuffix);
            using var landingScreen = Cv2.ImRead(capturePath);
            landingAnalysis = m_LandingDetector.Analyze(landingScreen);

            // Landed on asteroid belt
            if (landingAnalysis.LandedOnAsteroidBelt)
            {
                m_Logger.Information("Landed on asteroid belt");

                var mineOverviewLocated = m_MineOverviewDetector.TryLocate(landingScreen, out var mineOverviewBounds);
                var nothingFoundDetected = mineOverviewLocated &&
                                           m_NothingFoundDetector.Detect(landingScreen, mineOverviewBounds);
                if (nothingFoundDetected)
                {
                    context.BlacklistAsteroidBelt(selectedAsteroidBelt.Bounds);
                    m_Logger.Warning(
                        "Nothing Found detected in MINE overview. Blacklisting asteroid belt and selecting another. BlacklistedBeltCount={BlacklistedBeltCount}, BlacklistedBeltBounds={BlacklistedBeltBounds}",
                        context.BlacklistedAsteroidBeltCount,
                        selectedAsteroidBelt.Bounds);
                    return new MiningAutomationStateTransition(
                        Kind,
                        MiningAutomationStateKind.SelectBeltAndWarp,
                        MiningAutomationActionKind.WarpToAsteroidField,
                        capturePath,
                        AsteroidBeltOverview: analysis,
                        AsteroidBeltLanding: landingAnalysis);
                }

                return new MiningAutomationStateTransition(
                    Kind,
                    MiningAutomationStateKind.ApproachingAsteroid,
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
        return m_BeltOverviewDetector.Analyze(screen);
    }

    private MiningAutomationStateTransition Recover(string capturePath, AsteroidBeltOverviewAnalysis analysis)
    {
        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Recovery,
            MiningAutomationActionKind.Recover,
            capturePath,
            AsteroidBeltOverview: analysis);
    }

    private static Point Center(Rect bounds) => new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
}
