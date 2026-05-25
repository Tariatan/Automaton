using OpenCvSharp;

namespace Automaton.Detectors;

internal enum PopupState
{
    None,
    MaxSubmissions,
    SlowDown,
    ConnectionLost,
    Unknown
}

internal readonly record struct PopupDetection(PopupState State, Rect Bounds);