using Automaton.Detectors;
using OpenCvSharp;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class UnloadingCargoState : IMiningAutomationState
{
    private const string CaptureSuffix = ".mining-docked";

    private readonly MiningHoldDetector m_Detector;
    private readonly UndockButtonDetector m_UndockButtonDetector;
    private readonly ILogger m_Logger;

    public UnloadingCargoState()
        : this(new MiningHoldDetector(), new UndockButtonDetector(), Log.ForContext<UnloadingCargoState>())
    {
    }

    internal UnloadingCargoState(
        MiningHoldDetector detector,
        UndockButtonDetector undockButtonDetector,
        ILogger? logger = null)
    {
        m_Detector = detector;
        m_UndockButtonDetector = undockButtonDetector;
        m_Logger = logger ?? Log.ForContext<UnloadingCargoState>();
    }

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.UnloadCargo;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        m_Logger.Debug("Executing {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();
        var capturePath = context.ScreenCaptureService.CaptureCurrentScreenTrace(CaptureSuffix);
        cancellationToken.ThrowIfCancellationRequested();

        using var screen = Cv2.ImRead(capturePath);

        // TryLocate Undock button
        if (!m_UndockButtonDetector.TryLocate(screen, out var _))
        {
            // Failed to detect Undock button
            m_Logger.Error("Not in Dock => abort unloading");
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Recover,
                capturePath);
        }

        var analysis = m_Detector.Analyze(screen);
        if (!analysis.MiningHoldFocused)
        {
            if (analysis.MiningHoldEntryBounds is null)
            {
                m_Logger.Error("Failed to detect Mining Hold entry");
                return new MiningAutomationStateTransition(
                    Kind,
                    MiningAutomationStateKind.Recovery,
                    MiningAutomationActionKind.Recover,
                    capturePath,
                    analysis);
            }

            m_Logger.Information("Select Mining Hold entry");
            context.ClickUiElement(Center(analysis.MiningHoldEntryBounds.Value), cancellationToken);
        }

        if (analysis.MiningHoldContent == MiningHoldContentState.ContainsOre)
        {
        }

        if (analysis.MiningHoldContent != MiningHoldContentState.Empty)
        {
            return new MiningAutomationStateTransition(
                Kind,
                MiningAutomationStateKind.Recovery,
                MiningAutomationActionKind.Recover,
                capturePath,
                analysis);
        }

        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Undocking,
            MiningAutomationActionKind.Undock,
            capturePath,
            analysis);
    }

    private static Point Center(Rect bounds) => new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
}
