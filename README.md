# Automaton — Project Discovery Automation

A Windows desktop tool that automates the *Project Discovery* citizen-science mini-game in *EVE Online*. The game presents scatter plots of real biological cell data and asks players to draw polygon annotations around visible cell clusters. Automaton captures the screen, identifies the clusters using computer vision, draws the polygons, and submits the result — unattended.

![Platform](https://img.shields.io/badge/platform-Windows-blue)
![Framework](https://img.shields.io/badge/.NET-9.0-purple)
![Language](https://img.shields.io/badge/language-C%23-239120)

---

> **Disclaimer**
>
> This project was built purely for learning purposes: exploring computer vision with OpenCV, practicing WPF, and designing a non-trivial state machine. It is not intended to be run against live EVE Online servers.
>
> Using automation tools in EVE Online violates the [EVE Online End User License Agreement](https://community.eveonline.com/support/policies/eve-eula/). Running this software risks permanent account suspension. The author takes no responsibility for any consequences arising from its use.

---

## What it does

1. Launches the game client and logs in with a configured pilot.
2. For each round: captures the screen, locates the playfield, detects cluster regions, builds polygon annotations, clicks them in, and submits.
3. Recovers automatically from server popups (slow-down, max-submissions, connection-lost) and restarts the game if needed.
4. Cycles through up to three pilot accounts when one exhausts its daily quota.
5. Escalates from soft recovery → game restart → OS reboot when repeated failures indicate an unrecoverable state.

Detection uses an HSV color mask as the primary signal (cell clusters are vivid and saturated; the surrounding game UI is dark). A known-sample matching shortcut can bypass re-detection for scatter-plot layouts that have been seen before, using pre-annotated gold-standard masks for higher accuracy.

## UI overview

The window has three tabs:

- **Home** — one large Start/Stop button and a live status line. The same action is bound to `Shift+Alt+F11` so the window can stay minimized.
- **Setup** — choose which automation state to start from and which pilot account (1–3) to use first. Also exposes a *Process Samples* action for running the detection pipeline offline against a folder of screenshots.
- **Settings** — configure paths for the settings file, telemetry output, pilot avatar images, and known-sample templates.

## Tech stack

- **.NET 9 / WPF** (Windows only)
- **OpenCvSharp** — image processing and computer vision
- **Serilog** — structured logging and telemetry
- **Microsoft.Extensions.DependencyInjection** — DI container
- **MSTest** — unit and integration tests

## Project structure

| Project | Purpose |
|:--------|:--------|
| `Automaton.Core` | Project-agnostic library: screen capture, input simulation, telemetry, common state implementations |
| `Automaton` | Domain logic and WPF UI: image analysis pipeline, automation state machine, detectors |
| `Automaton.Tests` | Test suite (unit + integration) |

## Documentation

- [`Discovery_Strategy.md`](Discovery_Strategy.md) — polygon identification best practices and design decisions for the detection pipeline
- [`Documentation/Technical_Design_Description.md`](Documentation/Technical_Design_Description.md) — full technical design description covering architecture, key decisions, and context diagrams