# AGENTS.md

## Prompts

Reusable prompt skills live under `.agents/skills/`. When a request matches, read and follow the corresponding skill file, substituting the target class name in place of `{ClassName}` / `{DetectorName}`.

- `.agents/skills/optimize-class/SKILL.md` — SRP and efficiency analysis for a single class. Use when asked to optimize or review a class.
- `.agents/skills/create-detector/SKILL.md` — prescriptive rules for implementing a new `*Detector` class. Use when asked to create a new detector.

## Architecture

- `Automaton.Core` is project-agnostic. It must not contain any game-, discovery-, or domain-specific logic or settings.
- Domain-specific constants, settings, and configuration belong in the `Automaton` project, not in `Automaton.Core`.

## C# Conventions

Before writing any C# code, fetch and apply these guidelines:

```
gh api repos/Tariatan/Sharpbox/contents/best-practices-csharp.md --jq '.content' | base64 -d
```

**Project-level override:** Private instance fields use the `m_` prefix (e.g., `m_CachedCandidates`), not plain camelCase as stated in the shared file.

## Editing Rules

- Never add an empty line at the end of a file.
- If a file ends with an empty line, remove that empty line.

## Unit Tests

- Prefer `new Mock<T>()` and `.Object` over `Mock.Of<T>()` and `Mock.Get()`.
- Name mocks `[mockedObjectName]Mock`.
- Name unit test classes `[ClassUnderTest]Tests`.
- Name unit test methods `[MethodUnderTest]_[Precondition]_[ExpectedOutcome]`.
- Structure unit tests as `Arrange - Act - Assert` and include `// Arrange`, `// Act`, and `// Assert` comments.
- Prefer `[TestInitialize]` for complicated test setup. Use the test class constructor only for simple setup.
- Before broader refactoring, prefer characterization tests that cover existing workflow behavior over low-value tests for trivial methods.

## Merge Requests

- All changes land on `main` through a pull request (merge request) — never push directly to `main`.
- PR titles, and commit messages within the PR, must follow [Conventional Commits v1.0.0](https://www.conventionalcommits.org/en/v1.0.0/): `<type>[optional scope]: <description>`.
  - Allowed types: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`.
  - Mark breaking changes with `!` after the type/scope (e.g. `feat!:`) or a `BREAKING CHANGE:` footer.
- The PR description should summarize the change and its rationale; reference related issues where applicable.
