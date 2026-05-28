using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using OpenCvSharp;
using Serilog;

namespace Automaton.CommonAutomationStates;

internal sealed class CommonStartGameState(
    IAutomationInputController automationInputController,
    PlayNowButtonDetector playNowButtonDetector)
{
    private readonly ILogger m_Logger = Log.ForContext<CommonStartGameState>();

    public bool TryStartGame(string capturePath, CancellationToken cancellationToken, out Rect playButtonBounds)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!playNowButtonDetector.Detect(capturePath, out var playButtonLocation))
        {
            playButtonBounds = default;
            return false;
        }

        var delay = TimeSpan.FromMilliseconds(Delays.LauncherStartupMs);
        m_Logger.Information("Starting Game for {DelaySeconds:0.###} seconds...", delay.TotalSeconds);
        playButtonBounds = playButtonLocation.Bounds;
        automationInputController.MoveTo(GeometryHelper.Center(playButtonLocation.Bounds));
        automationInputController.LeftClick(cancellationToken);
        automationInputController.Delay(delay, cancellationToken);

        m_Logger.Information("Hide any active window on login screen first");
        automationInputController.PressKeyChord(VirtualKeys.Control, VirtualKeys.W, cancellationToken);

        return true;
    }
}
