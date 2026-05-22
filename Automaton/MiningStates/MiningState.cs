using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class MiningState : IMiningAutomationState
{
    private const string CaptureSuffix = ".mining-state";
    private readonly IAutomationInputController m_AutomationInputController;
    private readonly MiningAsteroidDetector m_AsteroidDetector;
    private readonly MiningLaserDetector m_LaserDetector;
    private readonly WarOverviewDetector m_WarOverviewDetector;
    private readonly ILogger m_Logger;

    private enum DockingReason
    {
        Timeout,
        AsteroidDepleted,
        CargoFull,
        Gtfo
    }

    public MiningState(IAutomationInputController automationInputController)
        : this(
            automationInputController,
            new MiningAsteroidDetector(),
            new MiningLaserDetector(),
            new WarOverviewDetector(),
            Log.ForContext<MiningState>())
    {
    }

    internal MiningState(
        IAutomationInputController automationInputController,
        MiningAsteroidDetector asteroidDetector,
        MiningLaserDetector laserDetector,
        WarOverviewDetector warOverviewDetector,
        ILogger? logger = null)
    {
        m_AutomationInputController = automationInputController;
        m_AsteroidDetector = asteroidDetector;
        m_LaserDetector = laserDetector;
        m_WarOverviewDetector = warOverviewDetector;
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
        while (DateTime.UtcNow - loopStart < Delays.MiningLoopDuration)
        {
            cancellationToken.ThrowIfCancellationRequested();
            m_AutomationInputController.Delay(Delays.MiningPollingMs, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            using var capture = context.ScreenCaptureService.CaptureCurrentScreen(CaptureSuffix);
            lastCapturePath = capture.CapturePath;

            if (capture.Image.Empty())
            {
                return Recover(capture.CapturePath);
            }

            if (m_WarOverviewDetector.TryLocate(capture.Image, out var warOverviewBounds))
            {
                var warOverviewNothingFound = NothingFoundDetector.Detect(capture.Image, warOverviewBounds);
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

            if (!m_AsteroidDetector.TryLocate(capture.Image))
            {
                dockingReason = DockingReason.AsteroidDepleted;
                break;
            }

            if (!m_LaserDetector.TryLocate(capture.Image))
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
