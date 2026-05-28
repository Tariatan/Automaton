using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class MiningState(
    IAutomationInputController automationInputController,
    MiningAsteroidDetector asteroidDetector,
    MiningLaserDetector laserDetector,
    WarOverviewDetector warOverviewDetector)
    : IMiningAutomationState
{
    private const string CaptureSuffix = ".mining-state";
    private readonly ILogger m_Logger = Log.ForContext<MiningState>();

    private enum DockingReason
    {
        Timeout,
        AsteroidDepleted,
        CargoFull,
        Gtfo
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
            automationInputController.Delay(Delays.MiningPollingMs, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            using var capture = context.ScreenCaptureService.CaptureCurrentScreen(CaptureSuffix);
            lastCapturePath = capture.CapturePath;

            if (capture.Image.Empty())
            {
                return Recover(capture.CapturePath);
            }

            using var warDetectionImage = capture.Image.Clone();
            if (warOverviewDetector.Detect(warDetectionImage, out var warOverviewBounds))
            {
                var warOverviewNothingFound = NothingFoundDetector.Detect(warDetectionImage, warOverviewBounds);
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

            if (!asteroidDetector.Detect(capture.Image))
            {
                dockingReason = DockingReason.AsteroidDepleted;
                break;
            }

            if (!laserDetector.Detect(capture.Image))
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
