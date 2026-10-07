# Automaton project memory

Last updated: 2026-10-07. Scope: this repository only.

## Durable context

- Windows desktop application for Project Discovery automation in EVE Online.
  The solution contains the WPF application `Automaton`, shared infrastructure
  `Automaton.Core`, and the xUnit suite `Automaton.Tests`.
- All three projects target `net9.0-windows`. Image analysis uses OpenCvSharp;
  dependency injection uses Microsoft.Extensions.DependencyInjection;
  logging and telemetry use Serilog. Directory.Build.props enables Windows
  targeting. Verify the installed SDK and native runtime before running checks.
- `Automaton.Core` must remain project-agnostic. Game-, discovery-, and
  domain-specific constants, settings, and configuration belong in `Automaton`.
- The documented workflow captures the playfield, detects cell clusters,
  constructs polygons, submits results, rotates up to three pilots, and handles
  rate limits and recovery. Offline sample processing supports detection work.
- [Technical Design](docs/Technical_Design_Description.md) owns behavior and architecture;
  [Polygon Detection Heuristics](docs/Polygon-Detection-Heuristics.md) owns
  detailed detection guidance. [README.md](README.md) provides the overview.
  [PLAN.md](docs/PLAN.md) is maintained exclusively by the user.
- [AGENTS.md](AGENTS.md) owns operational rules and versioning. Follow the
  common harness and the repository's detector skill when applicable.
  Record notable implementation changes in [CHANGELOG.md](CHANGELOG.md).
- Use sanitized screenshots, offline samples, and disposable configuration for
  verification. Keep credentials and private telemetry out of committed context.
  Offline tests do not establish native UI or live client behavior.

## Current evidence

- 2026-10-07: repository inspection confirms Automaton version 1.1.0 and
  Automaton.Core version 1.0.1 in their project files. These are working-tree
  metadata, not evidence of a published release.
- 2026-10-07: the working-tree changelog and Technical Design describe
  `DailyGiftDetector` and pre-pilot daily gift handling in `LoginState`: Claim,
  a five-second wait, Close, dismissal verification, and bounded recovery.
  Related application, Core, tests, and resources have pending local changes.
  No build, test, or native interaction result for those changes was established
  during this memory initialization.
- 2026-10-07: AGENTS.md was aligned with the shared harness structure and
  CHANGELOG.md introduced. This memory uses the examples' durable context,
  current evidence, and resume structure without importing their product rules
  or historical test results.
- README.md labels the test framework as MSTest, but the current test project
  references xUnit. Use the project configuration as evidence for tooling.

## Resume here

- Read this handoff, then the Technical Design and the user's PLAN.md.
  At initialization, PLAN.md contains an empty ToDO list; it establishes no
  additional implementation task.
- Inspect the current diff before continuing: daily gift/login work is pending
  locally. Preserve unrelated edits and verify its status with the user or
  current task context before treating it as accepted or complete.
- For the next implementation task, run checks appropriate to the affected
  workflow and record actual commands, results, evidence date, and remaining
  acceptance limits. Native Windows mouse/keyboard checks are needed for UI
  changes; unavailable checks remain unverified.

## Context maintenance

Keep this handoff compact and repository-specific. Update it when the user
requests memory maintenance, with durable facts, dated evidence, limitations,
and the next concrete step. Behavior and technical reasoning belong in the
Technical Design; upcoming tasks belong in the user-owned PLAN.md. Distinguish
implemented, verified, planned, and blocked work. Do not accumulate transcripts,
secrets, private telemetry, or unverified completion claims.
