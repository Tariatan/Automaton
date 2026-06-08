using OpenCvSharp;

namespace Automaton.Tests;

internal static class SyntheticDiscoveryImageFactory
{
    public static Mat LoadSingleClusterImage()
        => ScreenshotLoader.LoadOrSkip("Discovery/active_playfield_single_cluster.png");

    public static Mat LoadTwoClusterImage()
        => ScreenshotLoader.LoadOrSkip("Discovery/active_playfield_two_clusters.png");

    public static Mat LoadMaximumSubmissionsPopupImage()
        => ScreenshotLoader.LoadOrSkip("Discovery/maximum_submissions_popup.png");

    public static Mat LoadSlowDownPopupImage()
        => ScreenshotLoader.LoadOrSkip("Discovery/slow_down_popup.png");

    public static string GetSingleClusterImagePath()
        => ScreenshotLoader.GetPathOrSkip("Discovery/active_playfield_single_cluster.png");

    public static string GetTwoClusterImagePath()
        => ScreenshotLoader.GetPathOrSkip("Discovery/active_playfield_two_clusters.png");

    public static string GetFourClusterImagePath()
        => ScreenshotLoader.GetPathOrSkip("Discovery/active_playfield_four_clusters.png");

    public static string GetManyClusterImagePath()
        => ScreenshotLoader.GetPathOrSkip("Discovery/active_playfield_many_clusters.png");

    public static string GetSparseLowerClusterImagePath()
        => ScreenshotLoader.GetPathOrSkip("Discovery/active_playfield_sparse_lower_cluster.png");

    public static string GetMaximumSubmissionsPopupImagePath()
        => ScreenshotLoader.GetPathOrSkip("Discovery/maximum_submissions_popup.png");

    public static string GetSlowDownPopupImagePath()
        => ScreenshotLoader.GetPathOrSkip("Discovery/slow_down_popup.png");
}
