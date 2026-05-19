namespace Automaton.Detectors;

internal sealed class DowntimeDetector
{
    private readonly TimeOnly m_DowntimeAlert;
    private readonly TimeSpan m_Threshold;

    public DowntimeDetector()
        : this(new TimeOnly(11, 00), TimeSpan.FromMinutes(20))
    {
    }

    internal DowntimeDetector(TimeOnly downtimeAlert, TimeSpan threshold)
    {
        m_DowntimeAlert = downtimeAlert;
        m_Threshold = threshold;
    }

    public bool IsDowntimeImminent(DateTime currentTime)
    {
        var todayDowntime = currentTime.Date + m_DowntimeAlert.ToTimeSpan();
        var nextDowntime = currentTime <= todayDowntime
            ? todayDowntime
            : todayDowntime.AddDays(1);
        var remainingTime = nextDowntime - currentTime;
        return remainingTime >= TimeSpan.Zero && remainingTime <= m_Threshold;
    }
}
