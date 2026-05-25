using Automaton.Helpers;

namespace Automaton.CommonAutomationStates;

internal sealed class CommonExitState(IAutomationInputController automationInputController)
{
    public void QuitGame(CancellationToken cancellationToken)
    {
        automationInputController.QuitGame(cancellationToken);
    }
}
