using Automaton.Helpers;
using OpenCvSharp;

namespace Automaton.Tests;

internal sealed class StubScreenCaptureProvider(Func<Mat> captureFactory)
    : ScreenCaptureService.IScreenCaptureProvider
{
    public Mat CaptureScreen() => captureFactory();
}
