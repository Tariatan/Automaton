---
name: check-logging
description: Language-agnostic logging review checklist based on general structured-logging best practice. Use for every review; language-specific idioms are layered on top by the matching language skill.
---

# Check: Logging

Invoked by the reviewer agent for every review, regardless of language. Refines the reviewer agent's "Logging" section into a concrete checklist, rather than a one-line reminder to "log responsibly."

This isn't tied to a specific logging framework or company convention — it's the set of checks that hold for any reasonably-sized service or application, informed by widely-shared operational logging practice (e.g. the reasoning behind the standard syslog/Serilog/structured-logging severity levels).

Language-specific idioms (e.g. a language's structured-logging API, source-generated logging) are layered on top by the matching language skill — this skill covers the universal checklist only.

## What to check

- **Log level selection** — does the severity match the situation (Trace/Debug for diagnostics, Info for normal flow, Warning for recoverable anomalies, Error for failures, Critical/Fatal for unrecoverable)? Flag both over- and under-escalation. A rule of thumb: an **expected** situation (e.g. "not enough input to start this operation") is Information, not a Warning or Error just because it's a failure path — reserve Warning/Error for genuinely **unexpected** situations.
- **Consistency with surrounding logs** — same level, same phrasing style, same use of structured fields as nearby code doing similar work.
- **Coherent story** — read the sequence of log statements a request/operation would emit; can a reader reconstruct what happened without a debugger? When reviewing a change to logging, don't just review the code — mentally run the log output and check it still reads as a coherent narrative.
- **Start/end logging for long-running operations** — operations with meaningful duration (external calls, batch jobs, background work) should log both entry and completion/failure, so a stuck or failed run is visible. A common, readable pattern: log the start as "Doing X ..." and the end as "X finished" (or "X finished with error: ...").
- **Requests/events/responses** — externally visible interactions (incoming requests, published/consumed events, outgoing calls) should be logged with enough detail to trace them, both on the sending and the handling side.
- **Relevant contextual data** — correlation/operation IDs, entity identifiers, and other data needed to trace an issue across log lines; flag logs that are technically present but not actionable without it.
- **Right amount of data per level** — Information logs should carry as little as necessary (they're frequent; too much clutters the output), while Warning/Error logs should carry as much as is available (they're rare and someone will have to investigate).
- **Excessive logging** — logging inside hot loops, or logging that duplicates what's already captured a line above/below.
- **Missing logging** — silent failure paths, swallowed exceptions, or state transitions with no trace at all.
- **Structured logging** — parameters passed as structured fields/named placeholders, not string-interpolated into the message body (string interpolation defeats log aggregation/searching and, for user-controlled input, risks log injection).
- **Consistent formatting of values** — if the codebase has an established convention for quoting/separating values in log messages, follow it rather than inventing a new style in the same file.
- **Exclude bulky or sensitive data** — binary blobs, huge collections, secrets, and PII should not be logged wholesale; exclude or summarize them instead of spamming (or leaking through) the log.

## Reporting

Report logging issues under the severity that matches their consequence, per the reviewer agent's Priorities: missing logging around error handling or silent failures is usually **Major** (affects reliability/troubleshooting); level misuse, inconsistency, or excessive logging are usually **Minor**. Judge by the concrete impact on someone debugging this in production, not by the number of lines.
