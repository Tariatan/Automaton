namespace Automaton.Detectors;

internal sealed class DowntimeDetector(TimeOnly downtimeAlert, TimeSpan threshold)
{
    public DowntimeDetector()
        : this(new TimeOnly(11, 00), TimeSpan.FromMinutes(20))
    {
    }

    public bool IsDowntimeImminent(DateTime currentTime)
    {
        var todayDowntime = currentTime.Date + downtimeAlert.ToTimeSpan();
        var nextDowntime = currentTime <= todayDowntime
            ? todayDowntime
            : todayDowntime.AddDays(1);
        var remainingTime = nextDowntime - currentTime;
        return remainingTime >= TimeSpan.Zero && remainingTime <= threshold;
    }
}
