using OpenCvSharp;

namespace Automaton.MiningStates;

internal sealed record MiningAutomationContext(
    ScreenCaptureService ScreenCaptureService,
    IAutomationInputController AutomationInputController,
    IAutomationClock AutomationClock)
{
    private const int UiClickDelayMilliseconds = 300;
    private const int MouseParkingAreaLeft = 200;
    private const int MouseParkingAreaTop = 200;
    private const int MouseParkingAreaWidth = 100;
    private const int MouseParkingAreaHeight = 100;

    public void ClickUiElement(Point point, CancellationToken cancellationToken)
    {
        AutomationInputController.MoveTo(point);
        AutomationInputController.Delay(UiClickDelayMilliseconds, cancellationToken);
        AutomationInputController.LeftClick(cancellationToken);
        AutomationInputController.MoveTo(BuildRandomMouseParkingPoint());
    }

    private static Point BuildRandomMouseParkingPoint()
    {
        return new Point(
            Random.Shared.Next(MouseParkingAreaLeft, MouseParkingAreaLeft + MouseParkingAreaWidth),
            Random.Shared.Next(MouseParkingAreaTop, MouseParkingAreaTop + MouseParkingAreaHeight));
    }
}
