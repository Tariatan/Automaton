using Automaton.Helpers;
using OpenCvSharp;

namespace Automaton.Tests.Stubs;

internal sealed class StubScreenCaptureProvider(Func<Mat> captureFactory)
    : IScreenCaptureProvider
{
    public Mat CaptureScreen() => captureFactory();
}
