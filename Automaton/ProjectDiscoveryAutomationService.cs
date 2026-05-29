using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using Automaton.ProjectDiscoveryStates;
using OpenCvSharp;
using Serilog;

namespace Automaton;

internal sealed class ProjectDiscoveryAutomationService(
    ScreenCaptureService screenCaptureService,
    IAutomationInputController automationInputController,
    ConnectionLostPopupDetector connectionLostPopupDetector,
    IDiscoveryAutomationStateFactory discoveryAutomationStateFactory)
{
    private const int InitialPilotIndex = 1;
    private static readonly ILogger Logger = Log.ForContext<ProjectDiscoveryAutomationService>();
    private IProjectDiscoveryAutomationState m_CurrentState = null!;
    private ProjectDiscoveryAutomationContext m_Context = null!;

    private ScreenCaptureService ScreenCaptureService { get; } = screenCaptureService;

    public void ProcessSamples()
    {
        Logger.Information("Processing samples through automation service.");
        ScreenCaptureService.ProcessSamples();
    }

    public DiscoveryAutomationStepSummary Automate(
        CancellationToken cancellationToken,
        DiscoveryAutomationStateKind startingState = DiscoveryAutomationStateKind.Discover,
        int initialPilotIndex = InitialPilotIndex,
        bool keepDebugImages = false)
    {
        Logger.Information("Automation loop starting. InitialPilotIndex={InitialPilotIndex}", initialPilotIndex);
        automationInputController.Delay(Delays.AutomationStartupDelayMs, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        m_Context = new ProjectDiscoveryAutomationContext(initialPilotIndex, keepDebugImages);

        m_CurrentState = CreateState(startingState);

        DiscoveryAutomationStepSummary? lastSummary = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                lastSummary = ExecuteSingleStep(cancellationToken);

                automationInputController.TryHideUi(lastSummary.CapturePath, cancellationToken);

                if (TryTransitionToRecoverConnectionLostPopup(cancellationToken))
                {
                    continue;
                }

                if (lastSummary.Action == DiscoveryAutomationActionKind.StopAutomation)
                {
                    Logger.Information(
                        "Project Discovery automation requested application exit. State={State}, NextState={NextState}",
                        lastSummary.State,
                        lastSummary.NextState);
                    return lastSummary;
                }
                if (lastSummary.Action == DiscoveryAutomationActionKind.NoFurtherPilotsAvailable)
                {
                    Logger.Information(
                        "Project Discovery automation completed for all available pilots. State={State}, NextState={NextState}",
                        lastSummary.State,
                        lastSummary.NextState);
                    return lastSummary;
                }

                automationInputController.Delay(Delays.StateMachineNextStepDelayMs, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (lastSummary is not null)
        {
            Logger.Information("Automation loop canceled after a completed cycle. State={State}, NextState={NextState}, Action={Action}, CapturePath={CapturePath}",
                    lastSummary.State,
                    lastSummary.NextState,
                    lastSummary.Action,
                    lastSummary.CapturePath);
            return lastSummary;
        }

        return lastSummary ?? throw new OperationCanceledException(cancellationToken);
    }

    private DiscoveryAutomationStepSummary ExecuteSingleStep(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var transition = m_CurrentState.Execute(m_Context, cancellationToken);
        Logger.Information(
            "Project Discovery automation step executed. State={State}, NextState={NextState}, Action={Action}",
            transition.State,
            transition.NextState,
            transition.Action);
        m_Context.LastAction = transition.Action;
        m_CurrentState = CreateState(transition.NextState);

        return new DiscoveryAutomationStepSummary(
            transition.State,
            transition.NextState,
            transition.Action,
            transition.CapturePath);
    }

    private bool TryTransitionToRecoverConnectionLostPopup(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var capture = ScreenCaptureService.CaptureCurrentScreen(".discovery-connection-lost-popup-check");
        var detection = connectionLostPopupDetector.Detect(capture.CapturePath);
        if (detection.State != PopupState.ConnectionLost)
        {
            return false;
        }

        DrawPopupDebugOverlay(capture.CapturePath, detection, "Connection lost popup detected");
        Logger.Warning("Connection Lost popup detected during {CurrentState}. CapturePath={CapturePath}", m_CurrentState.Kind, capture.CapturePath);
        m_CurrentState = CreateState(DiscoveryAutomationStateKind.RecoverConnectionLostPopup);
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

    private IProjectDiscoveryAutomationState CreateState(DiscoveryAutomationStateKind stateKind)
    {
        return discoveryAutomationStateFactory.Create(stateKind);
    }
}