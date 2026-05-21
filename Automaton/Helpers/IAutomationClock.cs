namespace Automaton.Helpers;

internal interface IAutomationClock
{
    DateTime UtcNow { get; }
}
