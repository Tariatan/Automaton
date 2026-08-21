---
name: check-changelog-versioning
description: Deterministic file-diff check that a change includes a changelog entry (Keep a Changelog format) and a correct Semantic Versioning bump. Language-agnostic — run for every review.
---

# Check: Changelog & Semantic Versioning

Deterministic check invoked by the reviewer agent for every review, regardless of language. It refines the reviewer agent's "Changelog and Versioning" section into a file-diff procedure instead of a narrative judgment call, so it isn't skipped under context pressure.

Authoritative sources: [Keep a Changelog](https://keepachangelog.com/) for changelog format and tags, [Semantic Versioning](https://semver.org/) for version-bump rules. These are public, stable, widely-adopted conventions — not tied to any specific project.

If the project visibly uses a different changelog format or versioning scheme (e.g. Conventional Commits + auto-generated changelog, calendar versioning), follow that project's own convention instead of forcing Keep a Changelog / SemVer onto it.

## Step 1 — Was CHANGELOG.md touched?

- Check the diff's changed-files list for `CHANGELOG.md` (or `CHANGES.md`/`HISTORY.md`, whichever the repo already uses).
- Not present → missing changelog entry. Flag it, but still continue with Step 4 (version bump) independently — the two failures are reported separately.
- If the repository has no changelog file at all and no history of maintaining one, this check does not apply — say nothing.

## Step 2 — Compare source vs target CHANGELOG.md

- Fetch `CHANGELOG.md` at both the source and target/base revision.
- Extract the first `##` heading (or the contents of an `## [Unreleased]` section) from each. If they're identical, the top of the file wasn't actually updated (e.g. only older entries changed) → flag as missing.

## Step 3 — Validate the new entry

- The new entry must use one of the Keep a Changelog categories: `Added`, `Changed`, `Deprecated`, `Removed`, `Fixed`, `Security`.
- The entry text must describe the actual change in the diff, not be a placeholder or copy of an unrelated entry.

## Step 4 — Version bump

- Identify the version file(s) for this repo's stack — check for `package.json`, a `.csproj`/`Directory.Build.props` `<Version>`, `Cargo.toml`, `pyproject.toml`, a plain `VERSION` file, or pipeline/config YAML `version:`, depending on what's present.
- Compare the value at source vs target/base revision.
- Validate the bump against SemVer:
  - **Major** — behavior visible to consumers changes in a breaking way
  - **Minor** — new backward-compatible feature
  - **Patch** — backward-compatible bug fix only
- The new version must be strictly greater than the base version, and must match the changelog's new top entry if the changelog uses versioned headings.
- If the project has no formal version file and doesn't appear to cut releases from this repo (common for small personal projects, scripts, or apps deployed by tag/commit rather than semver), skip this step — flag only a genuinely missing changelog entry (Step 1–3), not a missing version bump.

## Reporting

Report any failure from Steps 1–4 as a single **Major** issue (per the reviewer agent's rule), naming the specific file and what's missing or incorrect — e.g. "CHANGELOG.md not updated" or "version bumped to 2.1.0 in Cargo.toml but the change is breaking, requires a Major bump to 3.0.0".
