using OpenCvSharp;

namespace Automaton.Tests;

internal static class SyntheticMiningImageFactory
{
    public static Mat CreateDockedItemHangarAndMiningHoldVisibleImage()
        => ScreenshotLoader.LoadOrSkip("Mining/docked_item_hangar_and_mining_hold_visible.png");

    public static Mat CreateUndockedWithoutLocationChangeTimerImage()
        => ScreenshotLoader.LoadOrSkip("Mining/undocked_without_location_change_timer.png");

    public static Mat CreateUndockedCompleteImage()
        => ScreenshotLoader.LoadOrSkip("Mining/undocked_with_location_change_timer.png");

    public static Mat CreateWarpToAsteroidFieldImage()
        => ScreenshotLoader.LoadOrSkip("Mining/warp_to_asteroid_field_overview_visible.png");

    public static Mat CreateWarpDriveActiveImage()
        => ScreenshotLoader.LoadOrSkip("Mining/warp_drive_active.png");

    public static Mat CreateLandedOnAsteroidBeltImage()
        => ScreenshotLoader.LoadOrSkip("Mining/landed_on_asteroid_belt.png");

    public static Mat CreateLandedOnEmptyAsteroidBeltImage()
        => ScreenshotLoader.LoadOrSkip("Mining/landed_on_empty_asteroid_belt.png");

    public static Mat CreateLandedOnAsteroidBeltImageWithMetersDistance()
        => ScreenshotLoader.LoadOrSkip("Mining/landed_on_asteroid_belt_meters_distance.png");

    public static void WriteDockedItemHangarAndMiningHoldVisibleImage(string outputPath)
        => ScreenshotLoader.CopyOrSkip("Mining/docked_item_hangar_and_mining_hold_visible.png", outputPath);

    public static void WriteUndockedCompleteImage(string outputPath)
        => ScreenshotLoader.CopyOrSkip("Mining/undocked_with_location_change_timer.png", outputPath);

    public static void WriteWarpToAsteroidFieldImage(string outputPath)
        => ScreenshotLoader.CopyOrSkip("Mining/warp_to_asteroid_field_overview_visible.png", outputPath);

    public static void WriteLandedOnAsteroidBeltImage(string outputPath)
        => ScreenshotLoader.CopyOrSkip("Mining/landed_on_asteroid_belt.png", outputPath);

    public static void WriteLandedOnEmptyAsteroidBeltImage(string outputPath)
        => ScreenshotLoader.CopyOrSkip("Mining/landed_on_empty_asteroid_belt.png", outputPath);

    public static void WriteLandedOnAsteroidBeltImageWithMetersDistance(string outputPath)
        => ScreenshotLoader.CopyOrSkip("Mining/landed_on_asteroid_belt_meters_distance.png", outputPath);
}
