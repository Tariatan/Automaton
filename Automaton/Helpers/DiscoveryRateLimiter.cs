namespace Automaton.Helpers;

internal sealed class DiscoveryRateLimiter
{
    private const int MaximumSubmissionsPerWindow = 5;
    private const int SubmissionWindowMs = 70_000;
    private readonly Queue<DateTime> m_SubmittedAtLocal = new();

    public TimeSpan GetDelayBeforeNextSubmit(DateTime localNow)
    {
        RemoveExpiredSubmissions(localNow);
        if (m_SubmittedAtLocal.Count < MaximumSubmissionsPerWindow)
        {
            return TimeSpan.Zero;
        }

        var elapsed = localNow - m_SubmittedAtLocal.Peek();
        var remaining = TimeSpan.FromMilliseconds(SubmissionWindowMs) - elapsed;
        return remaining <= TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    public void RecordSubmit(DateTime localNow)
    {
        RemoveExpiredSubmissions(localNow);
        m_SubmittedAtLocal.Enqueue(localNow);
    }

    private void RemoveExpiredSubmissions(DateTime localNow)
    {
        while (m_SubmittedAtLocal.Count > 0 &&
               (localNow - m_SubmittedAtLocal.Peek()).TotalMilliseconds >= SubmissionWindowMs)
        {
            m_SubmittedAtLocal.Dequeue();
        }
    }
}