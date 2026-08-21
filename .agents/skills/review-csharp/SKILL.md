---
name: review-csharp
description: Provides best practices and guidelines for C# and .NET code regarding visibility, DI, nullable refs, async/concurrency, disposal, and unit test conventions. Use when reviewing C# or .NET changes.
---

# Review: C# / .NET

This skill is activated when the reviewer agent encounters C# or .NET code. It refines (but does not contradict) the universal review principles defined in the reviewer agent.

The guidance below is general, widely-recognized C# practice (informed by the .NET runtime's own analyzer rules, the Roslyn/StyleCop defaults, and common idioms) — not tied to any specific company or codebase. Always follow the repository's own coding guidelines (`.editorconfig`, `Directory.Build.props`, a documented style guide) when they specify something different.

## Best Practices

Prefer:

- the narrowest visibility that satisfies the actual callers — don't widen a type's or member's access modifier just to make it reachable from a test assembly; use `InternalsVisibleTo` instead
- dependency injection over static classes (static state is hard to mock and often blocks parallel test execution)
- immutable objects — avoid exposing setters unless mutation is genuinely needed; mutable shared state is a recurring source of concurrency bugs
- records for value objects (gives you value equality and read-only properties without hand-rolling `IEquatable<T>`)
- nullable reference types (`<Nullable>enable</Nullable>`) — treat the compiler's null-flow analysis as a review aid, not noise to suppress
- meaningful naming
- guard clauses on public and internal method/constructor arguments (skip only for constructors that exist solely for a DI container to call)
- constructor injection
- `IReadOnlyCollection<T>` for collections a type exposes to callers
- `IEnumerable<T>` for collections a member merely consumes
- catching specific exception types, not a bare `Exception`
- `var` where the right-hand side already makes the type obvious — it also means a later change to a method's return type doesn't ripple through every call site
- the null-conditional operator (`?.`) when raising events, to avoid a race between the null check and the invocation
- one attribute per line when a member has multiple attributes

Avoid:

- unnecessary public APIs
- `public const` fields on anything that ships as a library — a `const`'s value is baked into the *caller's* assembly at compile time, so a later change to the constant silently doesn't take effect for callers until they recompile; use `public static readonly` instead
- unnecessary mutable state
- hidden side effects
- swallowing exceptions without at least logging why

## Async and Concurrency

Pay particular attention to:

- `ConfigureAwait` usage where appropriate (library code awaiting without a synchronization-context dependency)
- cancellation tokens — accepted, threaded through, and actually observed
- thread safety of any shared/static state
- event ordering
- race conditions
- `Task.Run` misuse (wrapping already-async work, or using it to fire-and-forget without observing the result/exceptions)
- blocking on async code (`.Result`, `.Wait()`, `.GetAwaiter().GetResult()`) — a common source of deadlocks
- fire-and-forget operations (unobserved exceptions, no way to await completion during shutdown)
- Rx subscriptions and their disposal
- resource disposal in general, and subscription cleanup specifically

## Resource Management

Check:

- `IDisposable` / `IAsyncDisposable` implemented and actually invoked (`using`/`await using`)
- subscriptions
- timers
- streams
- file handles
- event handlers (a subscriber that never unsubscribes is a classic memory leak)

Resources should always have a clear owner responsible for disposing them.

## Design

- Avoid cyclic dependencies between types, namespaces, and assemblies — even an indirect cycle (A → B → C → A) is a design smell worth flagging, not just a direct one.
- Use documentation comments (`///`) on public types and members; keep `<inheritdoc/>` for interface implementations and overrides rather than re-stating the same summary.

## Unit Tests

Verify:

- Arrange / Act / Assert structure (comments marking each section are a cheap, high-value readability aid)
- meaningful assertions (not just "doesn't throw")
- proper mocking — mock only what you don't own or what's genuinely expensive/nondeterministic
- naming convention: `MethodUnderTest_Scenario_ExpectedOutcome`
- if the codebase already names mock fields `[nameOfMockedThing]Mock`, follow that convention for new tests rather than introducing a different one
