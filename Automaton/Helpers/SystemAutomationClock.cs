namespace Automaton.Helpers;

internal sealed class SystemAutomationClock : IAutomationClock
{
    public DateTime LocalNow => DateTime.Now;
    public DateTime UtcNow => DateTime.UtcNow;
}
