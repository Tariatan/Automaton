using OpenCvSharp;

namespace Automaton.Tests;

internal static class SyntheticCommonImageFactory
{
    public static string GetPlayButtonScreenPath()
        => ScreenshotLoader.GetPathOrSkip("Common/play_button_screen.png");

    public static string GetLoginPilotSelectionScreenPath()
        => ScreenshotLoader.GetPathOrSkip("Common/pilot_selection_screen.png");

    public static Mat LoadPlayButtonScreenImage()
        => ScreenshotLoader.LoadOrSkip("Common/play_button_screen.png");

    public static Mat LoadLoginPilotSelectionScreenImage()
        => ScreenshotLoader.LoadOrSkip("Common/pilot_selection_screen.png");

    public static Mat LoadPilotAvatarImage(int pilotIndex)
        => ScreenshotLoader.LoadOrSkip($"Common/{pilotIndex}.png");

    public static Mat LoadFocusedPilotAvatarImage(int pilotIndex)
        => ScreenshotLoader.LoadOrSkip($"Common/{pilotIndex}_focused.png");

    public static void CopyPlayButtonScreenTo(string destinationPath)
        => File.Copy(GetPlayButtonScreenPath(), destinationPath, overwrite: true);

    public static void CopyLoginPilotSelectionScreenTo(string destinationPath)
        => File.Copy(GetLoginPilotSelectionScreenPath(), destinationPath, overwrite: true);

    public static void CopyPilotAvatarTemplatesTo(string pilotDirectory, int pilotIndex)
    {
        Directory.CreateDirectory(pilotDirectory);
        File.Copy(
            ScreenshotLoader.GetPathOrSkip($"Common/{pilotIndex}.png"),
            Path.Combine(pilotDirectory, $"{pilotIndex}.png"),
            overwrite: true);
        File.Copy(
            ScreenshotLoader.GetPathOrSkip($"Common/{pilotIndex}_focused.png"),
            Path.Combine(pilotDirectory, $"{pilotIndex}_focused.png"),
            overwrite: true);
    }
}
