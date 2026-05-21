using Automaton.Helpers;
using OpenCvSharp;

namespace Automaton.MiningStates;

internal sealed record MiningAutomationContext(
    ScreenCaptureService ScreenCaptureService,
    IAutomationInputController AutomationInputController,
    IAutomationClock AutomationClock)
{
    private const int MouseParkingAreaLeft = 200;
    private const int MouseParkingAreaTop = 200;
    private const int MouseParkingAreaWidth = 100;
    private const int MouseParkingAreaHeight = 100;
    private const int BeltBoundsTolerance = 8;
    private readonly List<Rect> m_BlacklistedAsteroidBelts = [];
    private Rect? m_CurrentAsteroidBeltBounds;

    public MiningAutomationActionKind LastAction { get; set; }

    public int BlacklistedAsteroidBeltCount => m_BlacklistedAsteroidBelts.Count;

    public void ClickUiElement(Point point, CancellationToken cancellationToken)
    {
        AutomationInputController.MoveTo(point);
        AutomationInputController.LeftClick(cancellationToken);
        AutomationInputController.MoveTo(BuildRandomMouseParkingPoint());
    }

    public void BlacklistAsteroidBelt(Rect beltBounds)
    {
        if (m_BlacklistedAsteroidBelts.Any(existingBounds => AreSimilarBounds(existingBounds, beltBounds)))
        {
            return;
        }

        m_BlacklistedAsteroidBelts.Add(beltBounds);
    }

    public bool IsAsteroidBeltBlacklisted(Rect beltBounds)
    {
        return m_BlacklistedAsteroidBelts.Any(existingBounds => AreSimilarBounds(existingBounds, beltBounds));
    }

    public void SetCurrentAsteroidBelt(Rect beltBounds)
    {
        m_CurrentAsteroidBeltBounds = beltBounds;
    }

    public bool TryGetCurrentAsteroidBelt(out Rect beltBounds)
    {
        if (m_CurrentAsteroidBeltBounds is null)
        {
            beltBounds = default;
            return false;
        }

        beltBounds = m_CurrentAsteroidBeltBounds.Value;
        return true;
    }

    private static Point BuildRandomMouseParkingPoint()
    {
        return new Point(
            Random.Shared.Next(MouseParkingAreaLeft, MouseParkingAreaLeft + MouseParkingAreaWidth),
            Random.Shared.Next(MouseParkingAreaTop, MouseParkingAreaTop + MouseParkingAreaHeight));
    }

    private static bool AreSimilarBounds(Rect first, Rect second)
    {
        return Math.Abs(first.X - second.X) <= BeltBoundsTolerance &&
               Math.Abs(first.Y - second.Y) <= BeltBoundsTolerance &&
               Math.Abs(first.Width - second.Width) <= BeltBoundsTolerance &&
               Math.Abs(first.Height - second.Height) <= BeltBoundsTolerance;
    }
}
