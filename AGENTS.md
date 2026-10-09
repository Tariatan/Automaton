# Automaton agent instructions

## Start here

Read [README.md](README.md), then
[docs/Technical_Design_Description.md](docs/Technical_Design_Description.md) for product behavior,
architecture, and technical decisions. Load other context only as needed.
If repository MEMORY.md or docs/PLAN.md exists, read it for handoff context or
remaining/upcoming tasks respectively.

Apply the global instructions supplied with the session and follow the coding
guidelines defined by the common harness; this file adds repository-specific rules.

The user's current instructions take precedence. The Technical Design owns
product behavior and technical decisions; memory is a compact handoff, not a
second specification. Proposals are not user-confirmed requirements. Resolve
contradictions explicitly instead of silently choosing an old memory entry.

## Product boundaries

Automaton is a Windows C#/.NET WPF application for Project Discovery automation,
with OpenCvSharp image analysis, a state machine, and telemetry. Preserve the
existing detection, pilot rotation, rate limiting, and recovery behavior.
Use offline samples and disposable configuration for verification.

## Architecture

- `Automaton.Core` is project-agnostic. It must not contain any game-, discovery-, or domain-specific logic or settings.
- Domain-specific constants, settings, and configuration belong in the `Automaton` project, not in `Automaton.Core`.
- Keep workflow changes consistent with the state and detection boundaries in the Technical Design.

## Prompts

Reusable prompt skills live under `.agents/skills/`. When a request matches,
read and follow the corresponding skill file, substituting the target class
name in place of `{DetectorName}`.

- [.agents/skills/create-detector/SKILL.md](.agents/skills/create-detector/SKILL.md) — prescriptive rules for implementing a new `*Detector` class. Use when asked to create a new detector.

## Work and verification

Implement one usable slice at a time. Before changing code, identify the affected
workflow in the Technical Design. Preserve existing behavior through
characterization tests before broader refactoring. Consult
[docs/Polygon-Detection-Heuristics.md](docs/Polygon-Detection-Heuristics.md)
when changing polygon detection.

Use the actual solution and project configuration for restore/build/test/run
commands for Automaton.slnx; do not assume another project's SDK, dependency
locks, or commands apply here. Automaton.Tests uses xUnit. Close running
Automaton instances before rebuilding.

Test meaningful detection, state transitions, rate limiting, cancellation, and
recovery behavior relevant to the change. UI changes need native Windows
mouse/keyboard checks in the affected workflow. Use sanitized screenshots and
disposable directories; keep credentials and private telemetry out of committed
examples and diagnostics. Offline tests do not prove live client behavior.
Do not claim planned commands, unavailable checks, or documentation as verified
implementation.

## Versioning

Bump versions for projects whose implementation changed. Whenever any project's
version changes, also bump the main project Automaton, even if its implementation
is unchanged. Do not bump other unchanged projects merely because they reference
or ship with a changed project.

Record notable implementation changes in [CHANGELOG.md](CHANGELOG.md) under the
affected project's version, newest first, with a date and the appropriate Keep
a Changelog category. Use Major for breaking behavior, Minor for compatible
features, and Patch for compatible bug fixes. Keep version metadata and changelog
entries aligned; planned rework belongs in the user's plan until implemented.

## Editing Rules

- docs/PLAN.md is maintained exclusively by the user. Read it for context, but never modify it, including task status, completion notes, or formatting.
- Never add an empty line at the end of a file.
- If a file ends with an empty line, remove that empty line.
- Keep documentation links repository-relative. Do not publish machine-specific absolute paths or references to local agent libraries.

## Maintain context

Keep this file short and operational. Update
[docs/Technical_Design_Description.md](docs/Technical_Design_Description.md)
only when a significant application design decision changes, such as architecture,
component responsibilities or boundaries, or a core workflow contract. Keep the
TDD concise and focused on durable design rationale. Routine features, bug fixes,
UI adjustments, implementation details, test results, and progress notes belong
in CHANGELOG.md, focused documentation, or repository memory when requested;
they do not require a TDD update unless they materially change application design.
Leave docs/PLAN.md unchanged. Link focused documents from the TDD only when they
support an important design decision.

If repository MEMORY.md exists, update it only when requested by the user, with
durable facts, evidence dates, limitations, and the next concrete step. Do not
accumulate transcripts, secrets, private telemetry, or unverified claims.
This concerns repository memory only, not global agent memory.

At handoff, distinguish implemented, verified, planned, and blocked work. Record
checks actually run. Do not mark a milestone complete merely because documents
describe it. No automatic delegation, remote publication, or recurring automation
is established by this file.
