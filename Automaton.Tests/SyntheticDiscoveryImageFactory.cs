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

    public static Mat CreateSparseLowerClusterImage()
        => ScreenshotLoader.LoadOrSkip("Discovery/active_playfield_sparse_lower_cluster.png");

    public static Mat CreateMultiSizeClusterImage()
        => ScreenshotLoader.LoadOrSkip("Discovery/active_playfield_multi_size_clusters.png");

    public static Mat CreateMaximumSubmissionsPopupImage()
        => ScreenshotLoader.LoadOrSkip("Discovery/maximum_submissions_popup.png");

    public static Mat CreateMaximumSubmissionsPopupImageWithPlayfield()
        => ScreenshotLoader.LoadOrSkip("Discovery/maximum_submissions_popup_with_playfield.png");

    public static Mat CreateSlowDownPopupImage()
        => ScreenshotLoader.LoadOrSkip("Discovery/slow_down_popup.png");

    public static Mat CreateConnectionLostPopupImage()
        => ScreenshotLoader.LoadOrSkip("Discovery/connection_lost_popup.png");

    public static Mat CreateWideScreenMaximumSubmissionsPopupImage()
        => ScreenshotLoader.LoadOrSkip("Discovery/wide_screen_maximum_submissions_popup.png");

    public static void WriteSingleClusterImage(string outputPath)
        => ScreenshotLoader.CopyOrSkip("Discovery/active_playfield_single_cluster.png", outputPath);

    public static void WriteTwoClusterImage(string outputPath)
        => ScreenshotLoader.CopyOrSkip("Discovery/active_playfield_two_clusters.png", outputPath);

    public static void WriteFourClusterImage(string outputPath)
        => ScreenshotLoader.CopyOrSkip("Discovery/active_playfield_four_clusters.png", outputPath);

    public static void WriteSparseLowerClusterImage(string outputPath)
        => ScreenshotLoader.CopyOrSkip("Discovery/active_playfield_sparse_lower_cluster.png", outputPath);

    public static void WriteMultiSizeClusterImage(string outputPath)
        => ScreenshotLoader.CopyOrSkip("Discovery/active_playfield_multi_size_clusters.png", outputPath);

    public static void WriteMaximumSubmissionsPopupImage(string outputPath)
        => ScreenshotLoader.CopyOrSkip("Discovery/maximum_submissions_popup.png", outputPath);

    public static void WriteMaximumSubmissionsPopupImageWithPlayfield(string outputPath)
        => ScreenshotLoader.CopyOrSkip("Discovery/maximum_submissions_popup_with_playfield.png", outputPath);

    public static void WriteSlowDownPopupImage(string outputPath)
        => ScreenshotLoader.CopyOrSkip("Discovery/slow_down_popup.png", outputPath);

    public static void WriteConnectionLostPopupImage(string outputPath)
        => ScreenshotLoader.CopyOrSkip("Discovery/connection_lost_popup.png", outputPath);
}
