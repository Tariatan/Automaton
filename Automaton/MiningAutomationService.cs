using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.MiningStates;
using Automaton.Primitives;
using OpenCvSharp;
using Serilog;

namespace Automaton;

internal sealed class MiningAutomationService(
    ScreenCaptureService screenCaptureService,
    IAutomationInputController automationInputController,
    IGameActionService gameActionService,
    IAutomationClock automationClock,
    PlayNowButtonDetector playNowButtonDetector,
    LocationChangeTimerDetector locationChangeTimerDetector,
    InventoryDetector inventoryDetector,
    DowntimeDetector downtimeDetector,
    AsteroidBeltOverviewDetector asteroidBeltOverviewDetector,
    MineOverviewDetector mineOverviewDetector,
    FirstAsteroidWithinReachDetector firstAsteroidWithinReachDetector,
    MiningAsteroidDetector miningAsteroidDetector,
    MiningLaserDetector miningLaserDetector,
    WarOverviewDetector warOverviewDetector,
    ConnectionLostPopupDetector connectionLostPopupDetector,
    CommonAutomationStates.ConnectionLostPopupRecoveryBehavior connectionLostPopupRecoveryBehavior,
    PilotAvatarDetector pilotAvatarDetector)
{
    private static readonly ILogger Logger = Log.ForContext<MiningAutomationService>();
    private readonly MiningAutomationContext m_Context = new(screenCaptureService, automationClock);
    private IMiningAutomationState m_CurrentState = null!;

    public MiningAutomationStepSummary Automate(
        MiningAutomationStateKind startingState,
        CancellationToken cancellationToken)
    {
        m_CurrentState = CreateState(startingState);
        Logger.Information("Mining automation loop starting. StartingState={StartingState}", startingState);
        automationInputController.Delay(Delays.AutomationStartupDelayMs, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        MiningAutomationStepSummary? lastSummary = null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                lastSummary = ExecuteSingleStep(cancellationToken);


                if (lastSummary.Action is not (MiningAutomationActionKind.StartGame or MiningAutomationActionKind.LoginPilot))
                {
                    gameActionService.TryHideUi(lastSummary.CapturePath, cancellationToken);
                }

                if (TryTransitionToRecoverConnectionLostPopup(cancellationToken))
                {
                    continue;
                }

                if (lastSummary.Action is MiningAutomationActionKind.QuitGameAndExitApplication or MiningAutomationActionKind.Reboot)
                {
                    Logger.Information(
                        "Mining automation requested application stop. State={State}, NextState={NextState}, Action={Action}, CapturePath={CapturePath}",
                        lastSummary.State,
                        lastSummary.NextState,
                        lastSummary.Action,
                        lastSummary.CapturePath);
                    return lastSummary;
                }

                automationInputController.Delay(Delays.StateMachineNextStepDelayMs, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (lastSummary is not null)
        {
            Logger.Information(
                "Mining automation loop canceled after a completed step. State={State}, NextState={NextState}, Action={Action}, CapturePath={CapturePath}",
                lastSummary.State,
                lastSummary.NextState,
                lastSummary.Action,
                lastSummary.CapturePath);
            return lastSummary;
        }
        finally
        {
            screenCaptureService.FlushClickTrace();
        }

        return lastSummary ?? throw new OperationCanceledException(cancellationToken);
    }

    private MiningAutomationStepSummary ExecuteSingleStep(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        MiningAutomationStateTransition transition = null!;
        for (var attempt = 1; attempt <= Settings.DetectionRetryAttempts; attempt++)
        {
            transition = m_CurrentState.Execute(m_Context, cancellationToken);
            if (!ShouldRetryAfterDetectionMiss(transition) || attempt >= Settings.DetectionRetryAttempts)
            {
                break;
            }

            Logger.Warning(
                "Detection miss in {State}. Retrying once before recovery. Attempt={Attempt}/{MaxAttempts}, CapturePath={CapturePath}",
                transition.State,
                attempt,
                Settings.DetectionRetryAttempts,
                transition.CapturePath);
            automationInputController.Delay(Settings.DetectionRetryDelayMs, cancellationToken);
        }

        Logger.Information(
            "Mining automation step executed. State={State}, NextState={NextState}, Action={Action}",
            transition.State,
            transition.NextState,
            transition.Action);
        m_Context.LastAction = transition.Action;
        m_CurrentState = CreateState(transition.NextState);

        return new MiningAutomationStepSummary(
            transition.State,
            transition.NextState,
            transition.Action,
            transition.CapturePath);
    }

    private static bool ShouldRetryAfterDetectionMiss(MiningAutomationStateTransition transition)
    {
        return transition is { Action: MiningAutomationActionKind.Recover, FailureReason: MiningAutomationFailureReason.DetectionMiss };
    }

    private bool TryTransitionToRecoverConnectionLostPopup(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var capture = m_Context.ScreenCaptureService.CaptureCurrentScreen(".mining-connection-lost-popup-check");
        var detection = connectionLostPopupDetector.Detect(capture.Image);
        if (detection.State != PopupState.ConnectionLost)
        {
            return false;
        }

        DrawPopupDebugOverlay(capture.CapturePath, detection, "Connection lost popup detected");
        Logger.Error("Connection Lost popup detected during {CurrentState}. CapturePath={CapturePath}", m_CurrentState.Kind, capture.CapturePath);
        m_CurrentState = CreateState(MiningAutomationStateKind.RecoverConnectionLostPopup);
        return true;
    }

    private static void DrawPopupDebugOverlay(string imagePath, PopupDetection detection, string label)
    {
        using var image = Cv2.ImRead(imagePath);
        if (image.Empty())
        {
            return;
        }

        DebugOverlay.Annotate(image, (detection.Bounds, OverlayColor.RedOrange));
        DebugOverlay.Label(image, label, OverlayColor.RedOrange);
        Cv2.ImWrite(imagePath, image);
    }

    private IMiningAutomationState CreateState(MiningAutomationStateKind stateKind)
    {
        return stateKind switch
        {
            MiningAutomationStateKind.StartingGame => new StartingGameState(automationInputController, gameActionService, playNowButtonDetector),
            MiningAutomationStateKind.Login => new LoginState(gameActionService, pilotAvatarDetector),
            MiningAutomationStateKind.Dock => new DockingState(automationInputController, gameActionService, asteroidBeltOverviewDetector),
            MiningAutomationStateKind.UnloadCargo => new UnloadingCargoState(automationInputController, gameActionService, inventoryDetector, downtimeDetector),
            MiningAutomationStateKind.Undocking => new UndockingState(automationInputController, locationChangeTimerDetector),
            MiningAutomationStateKind.SelectBeltAndWarp => new SelectBeltAndWarpState(automationInputController, gameActionService, asteroidBeltOverviewDetector, mineOverviewDetector, warOverviewDetector, Random.Shared.Next),
            MiningAutomationStateKind.ApproachingAsteroid => new ApproachingAsteroidState(automationInputController, gameActionService, mineOverviewDetector, firstAsteroidWithinReachDetector),
            MiningAutomationStateKind.Mining => new MiningState(automationInputController, miningAsteroidDetector, miningLaserDetector, warOverviewDetector),
            MiningAutomationStateKind.Recovery => new RecoveryState(automationInputController, gameActionService, asteroidBeltOverviewDetector, playNowButtonDetector, pilotAvatarDetector),
            MiningAutomationStateKind.RecoverConnectionLostPopup => new RecoverConnectionLostPopupState(connectionLostPopupRecoveryBehavior),
            _ => new PendingMiningAutomationState(stateKind)
        };
    }
}
