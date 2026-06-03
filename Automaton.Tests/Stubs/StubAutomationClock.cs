using Automaton.Helpers;

namespace Automaton.Tests.Stubs;

internal sealed class StubAutomationClock(DateTime? utcNow = null) : IAutomationClock
{
    private DateTime m_UtcNow = utcNow ?? new DateTime(2026, 5, 3, 12, 0, 0, DateTimeKind.Utc);

    public DateTime UtcNow => m_UtcNow;

    public void AdvanceBy(int milliseconds)
    {
        m_UtcNow = m_UtcNow.AddMilliseconds(milliseconds);
    }
}
