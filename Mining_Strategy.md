# Mining automation strategy

## Terminology

Mining automation: the EVE mining workflow run by `Automaton.exe -miner`.
Mining cycle: one full loop from docked station state through undock, warp, mining, return, unload, and repeat.
Docked: station screen is visible and the `Undock` button can be detected.
Mining Hold: ship cargo hold used for ore.
Item Hangar: station inventory destination for unloading ore.
Undocking: transitional state after clicking `Undock`.
Location change timer: the stable upper-left UI marker that confirms undock completion.
Overview: the EVE overview panel used to select belts, asteroids, and future navigation targets.
BELT tab: overview tab containing asteroid belt entries.
MINE tab: overview tab containing mineable asteroids after landing in a belt.
Asteroid belt row: one selectable asteroid belt entry in the BELT tab.
Asteroid row: one selectable asteroid entry in the MINE tab.
Recovery: a safe non-progress state used when expected screen evidence is missing.

## Main rules

- Keep mining automation separate from Project Discovery automation. Shared infrastructure belongs in small common services; workflow logic stays in mining states.
- Treat mining as a state machine. Each state should own one screen assumption and one narrow transition.
- Put all mining states and state contracts under `Automaton/MiningStates`.
- Capture before acting. State transitions should carry the capture path and detector result when possible so logs explain why the state moved.
- Prefer bounded, stable evidence over whole-screen guesses. Every detector should start from the smallest reliable ROI.
- Prefer keyboard shortcuts over visual button detection when the shortcut is known and safer. The `S` key replaced `warp_to_button` for belt warp.
- Wait 300 ms before clicking any mining UI element. Use `MiningAutomationContext.ClickUiElement` for mouse clicks so the delay stays centralized.
- On missing or ambiguous evidence, transition to `Recovery` instead of blind clicking.
- Keep state execution synchronous and cancellation-aware. Long waits must use `IAutomationInputController.Delay` so cancellation can interrupt them.
- Keep pending states explicit. A `PendingMiningAutomationState` is better than pretending an unfinished workflow step is implemented.
- Keep tests generated-fixture based. Real screenshots are useful for local smoke checks, but do not make permanent tests depend on `bin`, local captures, or user-specific folders.
- Follow `AGENTS.md`: Arrange/Act/Assert comments, behavior-style test names, simple code, no empty blank line at EOF.

## Current Implementation

- `ApplicationStartupOptions` selects Mining mode with `-miner` or `--miner`; selects Project Discovery mode with '-discovery' or '--discovery'; default mode starts with no automation.
- `MainWindow` starts `MiningAutomationService` in Mining mode, changes the title to `Automaton - Miner`, and disables Project Discovery-only pilot/sample controls.
- `MiningAutomationService` owns the loop, startup delay, step delay, state factory, and state transition logging.
- `UndockingState` waits 15 seconds, then polls once per second for 15 attempts for the resource-backed `location_change_timer` template in the fixed upper-left ROI.
- `SelectAsteroidBeltAndWarp` locates the Overview BELT tab with the resource-backed `overview_belt` template, chooses a random detected belt row, clicks it, then presses `S` to warp.
- `LandedOnAsteroidBeltState` polls once per second for the lower-center asteroid belt label, locates the MINE overview to its right, clicks the first asteroid row, and presses `A` to approach it.
- `ScreenCaptureService` normalizes mining and discovery detectors to the left `2560x2160` game viewport at `(0,0)`. This is an intentional current constraint for the target setup.

## Rejected Or Avoided

- Do not extend `ProjectDiscoveryAutomationService` with mining behavior. Its domain is sample analysis and polygon submission.
- Do not keep `warp_to_button` detection. Pressing `S` after selecting a belt is simpler and removes a fragile template dependency.
- Do not scan the full virtual desktop for mining UI. The current design assumes the game viewport is captured at `(0,0,2560,2160)`.
- Do not add broad multi-monitor support until the fixed viewport assumption stops being true in production.
- Do not write permanent tests against local runtime screenshots, downloaded images, or files under `bin`.
- Do not implement future states as a large monolithic mining method. The workflow will become too complex to review or recover safely.

a## Known Pain Points

- Recovery is only a placeholder. The next real recovery design should distinguish retryable screen drift, bad overview tab state, lost focus, and hard-stop conditions.
- The fixed capture viewport is practical now but is a known environmental assumption.

## Detector Guidance

- Template resources are appropriate for tiny, stable, high-contrast UI glyphs in known locations.
- Relative or absolute ROIs should be documented in the detector and represented in synthetic fixtures.
- Use current resource dimensions in tests instead of hardcoding old template sizes.
- Prefer multi-scale template checks around `1.0`, `0.95`, and `1.05` when the UI element may shift slightly with capture or scaling.
- Keep row detection based on stable icon/row structure where possible, not text recognition.
- Return analysis records with nullable bounds and lists so states can decide whether to act or recover.

## State Transition Target

Undocking:
wait fixed undock delay, then poll for location change timer.

SelectAsteroidBeltAndWarp:
open/select BELT overview, choose random asteroid belt, press `S`.

LandedOnAsteroidBelt:
poll for lower-center asteroid belt arrival signal, locate MINE overview, select first asteroid, press `A` to approach.

Mining:
lock or activate mining once in range, monitor cargo fullness.

Dock:
warp to home station using.

UnloadCargo:
move ore from Mining Hold to Item Hangar, then undock.

Recovery:
stop or perform a bounded, logged retry depending on the failure type.
