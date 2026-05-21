using OpenCvSharp;

namespace Automaton.Primitives;

internal static class Settings
{
    // MINE overview
    public const int MineOverviewWidth = 185;
    public const int MineOverviewHeight = 210;

    // Approaching asteroid
    public const string ApproachingAsteroidCaptureSuffix = ".mining-approaching-asteroid";
    public const int ApproachingAsteroidDistancePollingAttemptCount = 60;

    // Unloading cargo
    public const string UnloadingCargoCaptureSuffix = ".mining-unlading-cargo";

    // Inventory
    public static readonly Rect ItemHangarFirstRowBounds = new(75, 205, 300, 30);
    public static readonly Rect MiningHoldFirstRowBounds = new(75, 495, 300, 30);

}