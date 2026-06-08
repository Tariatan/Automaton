namespace Automaton.Helpers;

internal interface IAutomationClock
{
    DateTime LocalNow { get; }
    DateTime UtcNow { get; }
}
