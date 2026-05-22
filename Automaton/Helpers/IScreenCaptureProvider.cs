using OpenCvSharp;

namespace Automaton.Helpers;

internal interface IScreenCaptureProvider
{
    Mat CaptureScreen();
}
