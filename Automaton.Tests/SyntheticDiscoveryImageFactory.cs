using OpenCvSharp;

namespace Automaton.Tests;

internal static class SyntheticDiscoveryImageFactory
{
    public static Mat CreateSingleClusterImage()
        => ScreenshotLoader.LoadOrSkip("Discovery/active_playfield_single_cluster.png");

    public static Mat CreateTwoClusterImage()
        => ScreenshotLoader.LoadOrSkip("Discovery/active_playfield_two_clusters.png");

    public static Mat CreateFourClusterImage()
        => ScreenshotLoader.LoadOrSkip("Discovery/active_playfield_four_clusters.png");

    public static Mat CreateMaximumSubmissionsPopupImage()
        => ScreenshotLoader.LoadOrSkip("Discovery/maximum_submissions_popup.png");

    public static Mat CreateSlowDownPopupImage()
        => ScreenshotLoader.LoadOrSkip("Discovery/slow_down_popup.png");

    public static Mat CreateConnectionLostPopupImage()
        => ScreenshotLoader.LoadOrSkip("Discovery/connection_lost_popup.png");

    public static string GetSingleClusterImagePath()
        => ScreenshotLoader.GetPathOrSkip("Discovery/active_playfield_single_cluster.png");

    public static string GetTwoClusterImagePath()
        => ScreenshotLoader.GetPathOrSkip("Discovery/active_playfield_two_clusters.png");

    public static string GetMaximumSubmissionsPopupImagePath()
        => ScreenshotLoader.GetPathOrSkip("Discovery/maximum_submissions_popup.png");

    public static string GetSlowDownPopupImagePath()
        => ScreenshotLoader.GetPathOrSkip("Discovery/slow_down_popup.png");

    public static string GetConnectionLostPopupImagePath()
        => ScreenshotLoader.GetPathOrSkip("Discovery/connection_lost_popup.png");
}
