namespace Automaton.Primitives;

internal static class Delays
{
    // Input
    public const int MouseDownMs = 300;
    public const int MinimumClickMs = 250;
    public const int HoverMs = 200;
    public const int HideUiMs = 1000;

    // General automation
    public const int AutomationStartupDelayMs = 3_000;
    public const int StateMachineNextStepDelayMs = 500;
    public const int LauncherStartupMs = 60_000;
    public const int PilotLoginMs = 40_000;
    public const int PilotLogoutMs = 40_000;
    public const int LoadWindowMs = 3_000;
    
    public const int ConnectionLostExitMs = 1_000;

    public const int RecoveryMs = 60_000;

    // Quit game / window control
    public const int QuitGameConfirmMs = 2_000;
    public const int WindowActivationMs = 1_000;

    // Project Discovery
    public const int SubmissionWindowMs = 70_000;
    public const int SubmitActivationMs = 1_500;
    public const int SubmitResultMs = 5_000;

    // Mining: docking
    public const int BeforeDockMs = 1_000;
    public const int DockedPollingMs = 5_000;
    public const int DockedBounceMs = 25_000;

    // Mining: undocking
    public const int InitialUndockMs = 15_000;
    public const int UndockingBounceMs = 2_000;
    public const int LocationChangeTimerPollingMs = 1_000;

    // Mining: select belt & warp
    public const int LandingPollingMs = 1_000;

    // Mining: approaching asteroid
    public const int ApproachAsteroidDistancePollingMs = 1000;
    public const int LockAsteroidMs = 3_000;

    // Mining: active mining
    public const int MiningPollingMs = 5_000;
    public static readonly TimeSpan MiningLoopDuration = TimeSpan.FromMinutes(15);
}
