using System.IO;
using Automaton.Core.CommonAutomationStates;
using Automaton.Core.Detectors;
using Automaton.Core.Helpers;
using Automaton.Detectors;
using OpenCvSharp;
using Serilog;

namespace Automaton.ProjectDiscoveryStates;

internal sealed class LoginState(
    ScreenCaptureService screenCaptureService,
    IGameActionService gameActionService,
    IAutomationInputController automationInputController,
    PilotAvatarDetector pilotAvatarDetector,
    LoggedInPilotDetector loggedInPilotDetector,
    DailyGiftDetector dailyGiftDetector) : IProjectDiscoveryAutomationState
{
    private const string CaptureSuffix = ".discovery-login";
    private const string DailyGiftCaptureSuffix = ".discovery-login-daily-gift";
    private const int ClaimDelayMilliseconds = 5_000;
    private const int GiftPollingDelayMilliseconds = 1_000;
    private const int MaximumGiftChecks = 4;
    private readonly CommonLoginState m_CommonLoginState = new(gameActionService, automationInputController, pilotAvatarDetector, loggedInPilotDetector);
    private readonly ILogger m_Logger = Log.ForContext<LoginState>();
    public DiscoveryAutomationStateKind Kind => DiscoveryAutomationStateKind.Login;

    public DiscoveryAutomationStateTransition Execute(ProjectDiscoveryAutomationContext context, CancellationToken cancellationToken)
    {
        m_Logger.Information("Attempting pilot {PilotIndex} login", context.CurrentPilotIndex);
        if (!TryDismissDailyGift(cancellationToken, out var capturePath)
            || !m_CommonLoginState.TryLoginPilot(
            screenCaptureService,
            context.CurrentPilotIndex,
            CaptureSuffix,
            cancellationToken,
            out capturePath))
        {
            m_Logger.Error("Pilot {PilotIndex} login failed! CapturePath={CapturePath}", context.CurrentPilotIndex, capturePath);
            return new DiscoveryAutomationStateTransition(
                Kind,
                DiscoveryAutomationStateKind.Recovery,
                DiscoveryAutomationActionKind.RestartGame,
                capturePath);
        }

        m_Logger.Information("Pilot {PilotIndex} login succeeded. CapturePath={CapturePath}", context.CurrentPilotIndex, capturePath);
        return new DiscoveryAutomationStateTransition(
            Kind,
            DiscoveryAutomationStateKind.Discover,
            DiscoveryAutomationActionKind.LoginPilot,
            capturePath);
    }

    private bool TryDismissDailyGift(CancellationToken cancellationToken, out string capturePath)
    {
        capturePath = string.Empty;
        var claimClicked = false;
        for (var check = 0; check < MaximumGiftChecks; check++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var capture = screenCaptureService.CaptureCurrentScreen($"{DailyGiftCaptureSuffix}-{check}");
            capturePath = capture.CapturePath;
            if (!dailyGiftDetector.Detect(capture.Image, out var detection))
            {
                return true;
            }

            DrawDailyGiftOverlay(capturePath, detection);
            if (check == 0)
            {
                m_Logger.Information("Gift window detected. CapturePath={CapturePath}", capturePath);
            }

            if (check == MaximumGiftChecks - 1)
            {
                break;
            }

            if (!claimClicked && detection.ClaimButtonBounds is { } claimBounds)
            {
                m_Logger.Information("Claiming daily gift. CapturePath={CapturePath}", capturePath);
                automationInputController.ClickUiElement(GeometryHelper.Center(claimBounds), cancellationToken);
                claimClicked = true;
                automationInputController.Delay(ClaimDelayMilliseconds, cancellationToken);
                continue;
            }

            if (detection.CloseButtonBounds is { } closeBounds)
            {
                m_Logger.Information(
                    claimClicked ? "Daily gift claimed. CapturePath={CapturePath}" : "Daily gift already claimed. CapturePath={CapturePath}",
                    capturePath);
                m_Logger.Information("Closing daily gift window. CapturePath={CapturePath}", capturePath);
                automationInputController.ClickUiElement(GeometryHelper.Center(closeBounds), cancellationToken);
            }

            automationInputController.Delay(GiftPollingDelayMilliseconds, cancellationToken);
        }

        m_Logger.Warning("Daily gift window could not be dismissed. CapturePath={CapturePath}", capturePath);
        return false;
    }

    private static void DrawDailyGiftOverlay(string capturePath, DailyGiftDetectorDetection detection)
    {
        if (!File.Exists(capturePath))
        {
            return;
        }

        using var image = Cv2.ImRead(capturePath);
        if (image.Empty())
        {
            return;
        }

        DebugOverlay.Annotate(image, (detection.WindowBounds, OverlayColor.RedOrange));
        if (detection.ClaimButtonBounds is { } claimBounds)
        {
            DebugOverlay.Annotate(image, (claimBounds, OverlayColor.RedOrange));
        }
        if (detection.CloseButtonBounds is { } closeBounds)
        {
            DebugOverlay.Annotate(image, (closeBounds, OverlayColor.RedOrange));
        }
        DebugOverlay.Label(image, "Gift window detected", OverlayColor.RedOrange);
        ImageFileWriter.WriteImage(capturePath, image);
    }
}