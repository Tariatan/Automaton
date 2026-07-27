namespace Automaton.Primitives;

internal static class Delays
{
    // Input
    public const int MouseDownMs = 300;
    public const int MinimumClickMs = 250;
    public const int KeyChordTransitionMs = 120;
    public const int KeyChordHoldMs = 300;
    public const int HideUiMs = 1_000;

    // General automation
    public const int AutomationStartupDelayMs = 3_000;
    public const int StateMachineNextStepDelayMs = 500;
    public const int LauncherStartupMs = 60_000;
    public const int LoadWindowMs = 3_000;

    // Pilot login / logout
    public const int PilotLoginDebounceMs = 20_000;
    public const int PilotLogoutDebounceMs = 5_000;
    public const int PilotLoginPollingMs = 5_000;
    public const int PilotLoginTimeoutMs = 180_000;
    public const int PilotLogoutPollingMs = 5_000;
    public const int PilotLogoutTimeoutMs = 120_000;

    // Quit game
    public const int QuitGamePollingMs = 5_000;
    public const int QuitGameTimeoutMs = 120_000;

    // Recovery
    public const int ClientIsRunningButtonVisibleBeforeClickMs = 5_000;
    public const int ClientIsRunningButtonVisibleAfterClickMs = 30_000;
    public const int ConnectionLostExitMs = 1_000;
    public const int RecoveryMs = 60_000;

    // Window control
    public const int WindowActivationMs = 2_000;
    public const int CloseGameClientChordHoldMs = 3_000;
}