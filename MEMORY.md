# Automaton project memory

Last reviewed: 2026-10-08. Repository handoff context; verify dated observations before resuming work.

## Handoff

- **2026-10-08:** The working tree was clean before this memory cleanup. The
  earlier note that daily gift/login changes were pending is obsolete: gift
  handling, the version footer, and the .NET 10 migration are now recorded in
  commits `0624ebf`, `7b53d78`, and `355d1d8`, respectively.
- The old .NET 9 baseline and README/MSTest mismatch are obsolete. Current
  project files and [README.md](README.md) agree on .NET 10 and xUnit; use
  [Automaton.slnx](Automaton.slnx) and [global.json](global.json) for tooling.
- **Verification limit:** The 2026-10-07 memory initialization established no
  build, test, or native interaction results for the daily gift changes. This
  cleanup checked repository files and commit history only; it does not establish
  application correctness or live client behavior.

## Documentation history

- **2026-10-07:** Operational guidance was aligned and
  [CHANGELOG.md](CHANGELOG.md) introduced during the original memory setup.
  Behavior and architecture remain in the
  [Technical Design](docs/Technical_Design_Description.md); duplicated guidance
  has been removed from this handoff.

## Next step

- **2026-10-08:** The user-owned [PLAN.md](docs/PLAN.md) has an empty ToDO list.
  No implementation task is established. For the next requested change, inspect
  the current diff and validate the affected workflow using [AGENTS.md](AGENTS.md).
