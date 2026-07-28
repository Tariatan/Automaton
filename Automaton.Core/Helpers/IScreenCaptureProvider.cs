using OpenCvSharp;

namespace Automaton.Core.Helpers;

internal interface IScreenCaptureProvider
{
    Mat CaptureScreen();
}
