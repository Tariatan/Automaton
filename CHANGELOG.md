# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Versioning rule:

- **Major** - behavior visible to other system components changes (breaking)
- **Minor** - new backward-compatible feature
- **Patch** - backward-compatible bug fix only

## Automaton [1.1.3] - 2026-10-07

### Added

- Display the application version.

## Automaton [1.1.2] - 2026-10-07

### Added

- Saved daily gift captures outline the detected window and Claim/Close button, using the login-screen overlay style.
- Login logs report gift window detection, claiming or already-claimed status, and closing.

## Automaton [1.1.1] - 2026-10-07

### Fixed

- Locate the daily gift window by its title within the expanded search region, then verify anchors and return button bounds relative to the detected window origin. Reject clipped windows safely.

## Automaton [1.1.0] - 2026-10-07

### Added

- `DailyGiftDetector` identifies the daily login campaign window and returns Claim or Close button bounds.
- `LoginState` claims available daily gifts, waits five seconds, detects and clicks Close, and verifies dismissal before pilot login. Persistent windows trigger recovery after bounded checks; waits respect cancellation.

## Automaton.Core [1.0.1] - 2026-10-07

### Fixed

- Removed pre-login window closing from `CommonLoginState` so callers can handle login-screen windows before selecting a pilot. Post-login window cleanup remains active.