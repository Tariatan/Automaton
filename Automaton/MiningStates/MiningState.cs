using Automaton.Detectors;
using OpenCvSharp;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class MiningState : IMiningAutomationState
{
    private const string CaptureSuffix = ".mining-state";
    private const int PollingDelayMilliseconds = 5_000;
    private static readonly TimeSpan MiningLoopDuration = TimeSpan.FromMinutes(15);
    private readonly MiningAsteroidDetector m_AsteroidDetector;
    private readonly MiningLaserDetector m_LaserDetector;
    private readonly WarOverviewDetector m_WarOverviewDetector;
    private readonly NothingFoundDetector m_NothingFoundDetector;
    private readonly ILogger m_Logger;

    private enum DockingReason
    {
        Timeout,
        AsteroidDepleted,
        CargoFull,
        Gtfo
    }

    public MiningState()
        : this(
            new MiningAsteroidDetector(),
            new MiningLaserDetector(),
            new WarOverviewDetector(),
            new NothingFoundDetector(),
            Log.ForContext<MiningState>())
    {
    }

    internal MiningState(
        MiningAsteroidDetector asteroidDetector,
        MiningLaserDetector laserDetector,
        WarOverviewDetector warOverviewDetector,
        NothingFoundDetector nothingFoundDetector,
        ILogger? logger = null)
    {
        m_AsteroidDetector = asteroidDetector;
        m_LaserDetector = laserDetector;
        m_WarOverviewDetector = warOverviewDetector;
        m_NothingFoundDetector = nothingFoundDetector;
        m_Logger = logger ?? Log.ForContext<MiningState>();
    }

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.Mining;

    public MiningAutomationStateTransition Execute(MiningAutomationContext context, CancellationToken cancellationToken)
    {
        m_Logger.Debug("Executing {State}", Kind);
        var loopStart = DateTime.UtcNow;
        string? lastCapturePath = null;
        var dockingReason = DockingReason.Timeout;

        // Start mining
        m_Logger.Information("Start mining...");
        while (DateTime.UtcNow - loopStart < MiningLoopDuration)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.AutomationInputController.Delay(PollingDelayMilliseconds, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(CaptureSuffix);
            lastCapturePath = capturePath;
            using var screen = Cv2.ImRead(capturePath);

            if (screen.Empty())
            {
                return Recover(capturePath);
            }

            if (m_WarOverviewDetector.TryLocate(screen, out var warOverviewBounds))
            {
                var warOverviewNothingFound = m_NothingFoundDetector.Detect(screen, warOverviewBounds);
                if (!warOverviewNothingFound)
                {
                    if (context.TryGetCurrentAsteroidBelt(out var currentAsteroidBeltBounds))
                    {
                        context.BlacklistAsteroidBelt(currentAsteroidBeltBounds);
                    }

                    dockingReason = DockingReason.Gtfo;
                    break;
                }
            }

            if (!m_AsteroidDetector.TryLocate(screen))
            {
                dockingReason = DockingReason.AsteroidDepleted;
                break;
            }

            if (!m_LaserDetector.TryLocate(screen))
            {
                dockingReason = DockingReason.CargoFull;
                break;
            }
        }

        switch (dockingReason)
        {
            case DockingReason.Timeout:
                m_Logger.Warning("Mining cycle took longer than expected => Docking");
                break;
            case DockingReason.AsteroidDepleted:
                m_Logger.Information("Asteroid depleted => Docking");
                break;
            case DockingReason.CargoFull:
                m_Logger.Information("Cargo full => Docking");
                break;
            case DockingReason.Gtfo:
                m_Logger.Error("WAR overview is active and not empty => GTFO docking");
                break;
        }

        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Dock,
            MiningAutomationActionKind.None,
            lastCapturePath);
    }

    private MiningAutomationStateTransition Recover(string capturePath)
    {
        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Recovery,
            MiningAutomationActionKind.Recover,
            capturePath);
    }
}
