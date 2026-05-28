using OpenCvSharp;

namespace Automaton.Detectors;

internal interface IFirstAsteroidWithinReachDetector
{
    bool Detect(Mat screen, Rect mineOverviewBounds, Rect firstAsteroidRowBounds, bool drawDebugOverlay = true);

    bool Detect(
        Mat screen,
        Rect mineOverviewBounds,
        Rect firstAsteroidRowBounds,
        out DistanceUnitDetectionTelemetry telemetry,
        bool drawDebugOverlay = true);
}
