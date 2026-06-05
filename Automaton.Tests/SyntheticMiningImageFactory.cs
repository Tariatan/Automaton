using OpenCvSharp;

namespace Automaton.Tests;

internal static class SyntheticMiningImageFactory
{
    public static Mat LoadDockedItemHangarAndMiningHoldVisibleImage()
        => ScreenshotLoader.LoadOrSkip("Mining/docked_item_hangar_and_mining_hold_visible.png");

    public static Mat LoadDockedWithoutInventoryVisibleImage()
        => ScreenshotLoader.LoadOrSkip("Mining/docked_without_inventory.png");

    public static Mat LoadUndockedWithoutLocationChangeTimerImage()
        => ScreenshotLoader.LoadOrSkip("Mining/undocked_without_location_change_timer.png");

    public static Mat LoadUndockedWithoutBeltOverviewImage()
        => ScreenshotLoader.LoadOrSkip("Mining/in_space_without_belt_overview.png");

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

    public static Mat LoadLandedOnBusyAsteroidBeltImage()
        => ScreenshotLoader.LoadOrSkip("Mining/landed_on_busy_asteroid_belt.png");

    public static Mat LoadLandedOnAsteroidBeltImageWithMetersDistance()
        => ScreenshotLoader.LoadOrSkip("Mining/landed_on_asteroid_belt_meters_distance.png");

    public static Mat LoadMiningGtfoImage()
        => ScreenshotLoader.LoadOrSkip("Mining/mining_gtfo.png");

    public static Mat LoadMiningAsteroidDepletedImage()
        => ScreenshotLoader.LoadOrSkip("Mining/mining_asteroid_depleted.png");

    public static Mat LoadMiningLasersDeactivatedImage()
        => ScreenshotLoader.LoadOrSkip("Mining/mining_lasers_deactivated.png");

    public static Mat LoadInSpaceBeltOverviewWithoutHomeStationImage()
        => ScreenshotLoader.LoadOrSkip("Mining/in_space_belt_overview_without_home_station.png");

    public static Mat LoadInSpaceWithDefaultOverviewActiveImage()
        => ScreenshotLoader.LoadOrSkip("Mining/in_space_with_default_overview_active.png");
}
