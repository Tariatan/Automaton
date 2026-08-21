---
name: review-rust
description: Provides best practices and guidelines for Rust code regarding naming, ownership/borrowing, error handling, Send/Sync and async lock-across-await hazards, Drop/RAII cleanup, dispatch, and unit test conventions. Use when reviewing Rust changes.
---

# Review: Rust

This skill is activated when the reviewer agent encounters Rust code. It refines (but does not contradict) the universal review principles defined in the reviewer agent.

The guidance below is general Rust community practice — the official Rust API Guidelines, the reasoning behind widely-used crates, and idioms enforced by `rustfmt`/`clippy` — not tied to any specific company or codebase. Always follow the repository's own conventions (its `rustfmt.toml`, `clippy.toml`, or a documented style guide) when they specify something different.

## Naming

Follow the [Rust API Guidelines](https://rust-lang.github.io/api-guidelines/naming.html) casing conventions (originally RFC 430):

- `snake_case` for functions, variables, modules, and crate names
- `UpperCamelCase` for types (structs, enums, traits)
- `SCREAMING_SNAKE_CASE` for constants and statics
- `kebab-case` is conventional for the package/directory name on disk, even though the crate name used in `use` statements is `snake_case`

## Best Practices

Prefer:

- ownership and borrowing over cloning
- `Result`/`Option` over panicking
- enums with data over stringly-typed values
- iterators and combinators over manual loops
- derive macros for common traits
- the newtype pattern for type safety
- exhaustive pattern matching (let a new enum variant fail to compile at every match site instead of silently falling through a wildcard arm)

Avoid:

- unnecessary `unwrap()`/`expect()` in library code — prefer propagating a `Result`; reserve `unwrap()`/`expect()` for cases the code has already proven can't fail, and prefer `expect()` with a message explaining why
- unnecessary `clone()` or `Arc` where a borrow would suffice
- `unsafe` without a `// SAFETY:` comment explaining which invariant the caller/implementation is upholding
- overly broad trait bounds (bound on exactly what the function body uses, not "whatever might be convenient later")

## Error Handling

- Use a typed, matchable error (e.g. via the `thiserror` crate) at any boundary where a caller needs to inspect or react differently per failure — a public crate API, or an internal module interface with distinct handling per variant.
- Use an opaque, propagation-only error (e.g. via the `anyhow` crate, with `.context()`/`.with_context()` to attach information as it propagates) for code whose only job is to bubble the error up to a logging/reporting boundary (`main`, a CLI handler, a test helper).
- Never return an opaque/type-erased error from an API callers are expected to `match`/`if let` on — that erases exactly the information they need. Return the typed error instead.
- A typed error that implements `std::error::Error` converts into an opaque error automatically via `?`, so the two compose cleanly: typed at the boundary that must react, opaque above it.

```rust
#[derive(Debug, thiserror::Error)]
pub enum SensorError {
    #[error("communication timeout after {timeout_ms} ms")]
    Timeout { timeout_ms: u64 },
    #[error("I/O error")]
    Io(#[from] std::io::Error),
}

fn run() -> anyhow::Result<()> {
    let temperature = read_sensor().context("failed to read temperature sensor")?; // SensorError -> anyhow::Error
    Ok(())
}
```

## Async and Concurrency

Pay particular attention to:

- `Send`/`Sync` bounds on anything crossing a thread or task boundary
- proper use of async runtime primitives
- deadlocks from holding a lock across an `.await` point
- for synchronous, short-held locks (even inside async code), a non-poisoning lock (e.g. `parking_lot::Mutex`/`RwLock`) is usually a better fit than `std::sync` — a panic while holding a `std::sync` lock poisons it for every future user, which is rarely what you want
- switch to an async-aware lock (e.g. `tokio::sync::Mutex`/`RwLock`) only when the lock genuinely must be held across an `.await`; never call a blocking variant of it (e.g. `blocking_lock()`) from inside an async task — it blocks the executor thread and risks starvation/deadlock
- prefer a read-write lock over a plain mutex when reads significantly outnumber writes
- cancellation safety — what state does a future leave behind if it's dropped mid-`.await`?
- proper error propagation with `?`

## Resource Management

Check:

- `Drop` implementations — correct, and never panicking inside `drop()`
- RAII patterns used for guaranteed cleanup instead of "remember to call `close()`"
- proper cleanup in error paths (not just the happy path)
- file handle and connection lifetime, and that it's obvious from the type who owns them

## Dispatch and Structure

- Prefer static dispatch (generics/`impl Trait`) on hot paths, where monomorphization and inlining matter; reach for dynamic dispatch (`dyn Trait`) when you genuinely need a heterogeneous collection or a plugin-style boundary, and accept the small indirection cost there.
- Keep a type's inherent `impl` block(s) near its definition; group trait implementations logically rather than scattering them across the file.
- Run `cargo fmt` and `cargo clippy` (ideally `-D warnings` in CI) as a baseline — don't spend review time on things a formatter/linter would already catch; instead flag `#[allow(clippy::...)]` that lacks a comment explaining why the lint doesn't apply.

## Unit Tests

Verify:

- unit tests live in a `#[cfg(test)] mod tests` block next to the code they test (`src/`); tests that exercise the crate's public API end-to-end belong in the separate `tests/` integration-test directory — don't blur the two.
- `#[cfg(test)]` (or a dedicated Cargo feature for more advanced mock/integration setups) is the default way to swap a real dependency for a test double — it's zero-cost (the test-only code doesn't exist in the release build) and doesn't require a runtime DI container. Reach for trait-object dependency injection only when the code genuinely needs to swap the implementation at runtime, not just in tests.
- meaningful assertions
- proper use of `#[should_panic]` vs. a `Result`-returning test (prefer the latter — a `Result`-returning test still reports *why* it failed)
- naming convention consistent with the codebase
