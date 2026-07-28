# Create a new Detector class

Target: Implement a new `{DetectorName}` class following the established patterns and lessons learned from prior detector implementations in this codebase.

---

## Mandatory structure

- **Single public method only:** `bool Detect(Mat screen, out {DetectorName}Detection detection)`. No other public methods. All helpers are `private static` or `private`.
- **Return type:** A `readonly record struct` named `{DetectorName}Detection` defined in the same file. Include only the data the caller needs.
- **Sealed class:** Always `internal sealed class`.
- **`IDisposable`** if the detector caches `Mat` templates or holds OpenCV resources. Use a `m_` prefix for instance fields.

## Entry point guard (early exit)

The first lines of `Detect` must guard against invalid input before any OpenCV work:

```csharp
public bool Detect(Mat screen, out {DetectorName}Detection detection)
{
    detection = default;
    if (screen.Empty() || !IsRegionAvailable(screen.Size()))
        return false;
    // ...
}
```

Implement `IsRegionAvailable` as a `private static bool` that checks the screen is large enough to contain the detection region.

## Threshold constants

Always define two named threshold constants:

```csharp
private const double MinimumMatchScore = 0.XX;  // minimum to accept as a match
private const double EarlyExitScore    = 0.XX;  // high-confidence shortcut
```

Apply `EarlyExitScore` inside loops to break early when a definitive match is already found. Never use inline magic numbers for thresholds.

## Scale: test 1.0 first

When iterating multiple scales (or candidates), always try scale 1.0 (or the most likely candidate) first. This enables early exit at the high-confidence threshold without exhausting the full search space.

## Size guards before classification

Before passing a candidate region to any classifier or digit recognizer:

- Check **minimum width** (e.g., `MinimumDigitGlyphWidth`)
- Check **minimum height** (e.g., `MinimumDigitGlyphHeight`)
- Check **maximum width** (`MaximumDigitGlyphWidth`) to reject noise artifacts

Reject silently with `continue` or `return false` — never let noise fall through to the classifier.

## Classifier must have a rejection path

Any `TryRecognize*` method **must** be able to return `false`. A catch-all branch that always returns `true` will silently misclassify noise. If the glyph does not match any known pattern, return `false`.

## Minimum quality threshold on results

After scoring candidates, reject the result if the best score is below `MinimumMatchScore`. Do not return a low-quality match as "found" just because it is the only candidate.

## Performance: prefer Cv2.Reduce over per-pixel Mat loops

When computing column or row pixel projections, use `Cv2.Reduce` to project the entire region in one call, then iterate the resulting 1D array in managed code:

```csharp
// Column projection (non-zero pixels per column):
using var colProjection = new Mat();
Cv2.Reduce(window, colProjection, ReduceDimension.Row, ReduceTypes.Max, MatType.CV_8U);

// Row projection (non-zero pixels per row):
using var rowProjection = new Mat();
Cv2.Reduce(glyph, rowProjection, ReduceDimension.Column, ReduceTypes.Max, MatType.CV_8U);
```

Never allocate a sub-`Mat` + `CountNonZero` inside a column or row loop.

## Materialize LINQ sequences that are enumerated more than once

If a LINQ query feeds two operations (e.g., `UnionBounds` + `Sum`), call `.ToList()` before the first use.

## Named constants for all magic numbers

Every numeric literal with domain meaning must be a named constant:

| Category | Examples |
|---|---|
| Region bounds | `PortraitX`, `PortraitY`, `PortraitSize` |
| Thresholds | `MinimumMatchScore`, `EarlyExitScore`, `MinimumCandidateScore` |
| Glyph sizes | `MinimumDigitGlyphWidth`, `ExpectedTextHeight` |
| Penalties | `HeightPenaltyWeight` |
| Tolerances | `DecimalPointVerticalTolerance` |

## Resource management

- Every `Mat` created inside the method must be in a `using` statement or disposed in a `try/finally`.
- Template caches (when `IDisposable` is implemented) must dispose every cached `Mat` in `Dispose()` and clear the dictionary.

## SRP checklist before committing

- [ ] Does `Detect` delegate all sub-work to `private` helpers?
- [ ] Is there any UI formatting, logging of UI state, or configuration reading inside this class? (Move it out.)
- [ ] Does this class know about anything outside its detection region? (It shouldn't.)
- [ ] Are there any public methods beyond `Detect` and `Dispose`? (Remove or make private.)