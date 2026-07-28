using Automaton.Core.Helpers;
using Automaton.Core.Primitives;
using Serilog;

namespace Automaton.Helpers;

internal sealed class DiscoveryGameActions(IAutomationInputController inputController) : IDiscoveryGameActions
{
    private readonly ILogger m_Logger = Log.ForContext<DiscoveryGameActions>();
    private const int ProjectDiscoveryWindowToggleChordHoldMs = 3_000;

    public void ToggleProjectDiscoveryWindow(CancellationToken cancellationToken)
    {
        m_Logger.Information("Toggle Project Discovery window");
        inputController.PressKeyChordWithHold(
            VirtualKeys.Alt,
            VirtualKeys.L,
            cancellationToken,
            holdDelayMs: ProjectDiscoveryWindowToggleChordHoldMs);
        inputController.Delay(Delays.WindowActivationMs, cancellationToken);
    }
}