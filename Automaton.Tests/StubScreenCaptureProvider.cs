using Automaton.Helpers;
using OpenCvSharp;

namespace Automaton.Tests;

internal sealed class StubScreenCaptureProvider(Func<Mat> captureFactory)
    : IScreenCaptureProvider
{
    public Mat CaptureScreen() => captureFactory();
}
