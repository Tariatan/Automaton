using OpenCvSharp;

namespace Automaton.Tests;

internal static class SyntheticMiningImageFactory
{
    public static Mat LoadDockedItemHangarAndMiningHoldVisibleImage()
        => ScreenshotLoader.LoadOrSkip("Mining/docked_item_hangar_and_mining_hold_visible.png");

    public static Mat LoadUndockedWithoutLocationChangeTimerImage()
        => ScreenshotLoader.LoadOrSkip("Mining/undocked_without_location_change_timer.png");

    public static Mat LoadUndockedCompleteImage()
        => ScreenshotLoader.LoadOrSkip("Mining/undocked_with_location_change_timer.png");

    public static Mat LoadWarpToAsteroidFieldImage()
        => ScreenshotLoader.LoadOrSkip("Mining/warp_to_asteroid_field_overview_visible.png");

    public static Mat LoadWarpDriveActiveImage()
        => ScreenshotLoader.LoadOrSkip("Mining/warp_drive_active.png");

    public static Mat LoadLandedOnAsteroidBeltImage()
        => ScreenshotLoader.LoadOrSkip("Mining/landed_on_asteroid_belt.png");

    public static Mat LoadLandedOnEmptyAsteroidBeltImage()
        => ScreenshotLoader.LoadOrSkip("Mining/landed_on_empty_asteroid_belt.png");

    public static Mat LoadLandedOnAsteroidBeltImageWithMetersDistance()
        => ScreenshotLoader.LoadOrSkip("Mining/landed_on_asteroid_belt_meters_distance.png");

    public static Mat LoadMiningGtfoImage()
        => ScreenshotLoader.LoadOrSkip("Mining/mining_gtfo.png");
}
