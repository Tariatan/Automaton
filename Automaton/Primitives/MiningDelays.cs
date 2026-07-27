namespace Automaton.Primitives;

internal static class MiningDelays
{
    // Docking
    public const int BeforeDockMs = 1_000;
    public const int DockedBounceMs = 25_000;

    // Undocking
    public const int InitialUndockMs = 15_000;
    public const int UndockingBounceMs = 2_000;
    public const int LocationChangeTimerPollingMs = 1_000;

    // Select belt & warp
    public const int LandingPollingMs = 1_000;

    // Approaching asteroid
    public const int ApproachAsteroidDistancePollingMs = 1_000;
    public const int LockAsteroidMs = 3_000;

    // Active mining
    public const int MiningPollingMs = 5_000;
    public static readonly TimeSpan MiningLoopDuration = TimeSpan.FromMinutes(15);
}