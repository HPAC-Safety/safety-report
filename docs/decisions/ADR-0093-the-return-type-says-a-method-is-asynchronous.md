---
title: The return type says a method is asynchronous, so the name does not
description: A method this repository declares is named for what it does, with no Async suffix; a member implementing an external contract keeps the name that contract gives it.
type: adr
status: accepted
date: 2026-09-22
decision-makers: Chase Florell
keywords: naming, async, Task, ValueTask, conventions, csharp
---

# ADR-0093 — The return type says a method is asynchronous, so the name does not

## Status

Accepted.

## Context

Every asynchronous method this repository owned carried an `Async` suffix —
100 distinct names across 126 files. The suffix restates what the signature
already says. A method returning `Task`, `Task<T>`, `ValueTask` or
`ValueTask<T>` is awaitable, and the compiler, the IDE, and the reader all
learn that from the return type before they reach the name.

The convention is inherited from .NET's `Begin*`/`End*` and
`TAP-alongside-sync` era, when a type commonly offered `Foo()` **and**
`FooAsync()` and the two needed distinct names. That is a real problem and the
suffix is a real solution to it — for a library with synchronous twins. This
codebase has none: every one of those hundred methods is asynchronous, with no
synchronous counterpart anywhere. The suffix therefore distinguishes nothing.
It is noise on every call site, in every stack trace, and in every name an
agent reads while looking for the method it wants.

The installed upstream [`csharp-async`](https://github.com/github/awesome-copilot/tree/main/skills/csharp-async)
skill teaches the suffix, as a general .NET audience would expect. `Skillfile`
already says repository-local guidance overrides an upstream skill's generic
details, but nothing wrote down what this repository wanted, so the upstream
default won by silence.

## Decision

**A method this repository declares is named for what it does. No `Async`
suffix.** `Create`, `Delete`, `ClaimNext`, `EnsureMigrated`. The return type
states that it is asynchronous, which is the only thing the suffix was saying.

**A member that implements or overrides a contract this repository does not own
keeps the name that contract gives it.** This is not a style preference; the
name is fixed by somebody else's interface or base class, and changing it means
not implementing the contract. In this repository that is:

| Kept | Because |
|---|---|
| `DisposeAsync` | `IAsyncDisposable`, and xunit's `IAsyncLifetime` |
| `InitializeAsync` | xunit's `IAsyncLifetime` |
| `Worker.ExecuteAsync` | `BackgroundService` |
| `SaveChangesAsync` | `DbContext` |
| `ReadAsync` on a `Stream` subclass | `Stream` |
| `SendAsync` on an `HttpMessageHandler` subclass | `HttpMessageHandler` |
| every EF Core and BCL call we make | not ours to name |

A method that merely *resembles* a framework name is still ours: the contract
tests' `CreateStore`, `TryRead` and `TryUpload` override this repository's own
abstract base class, so they follow this record.

## Consequences

- Call sites read as the operation rather than the mechanism:
  `await questions.Create(...)` instead of `await questions.CreateAsync(...)`.
- `skills/hpac-safety-conventions` carries the rule, and `AGENTS.md` records
  that it overrides `csharp-async` on this one point. The rest of that skill —
  `ConfigureAwait`, cancellation tokens, avoiding `async void`, not blocking on
  a `Task` — is unaffected and still applies.
- A reviewer seeing an `Async` suffix on new code has one question: does this
  implement an external contract? If not, it is a rename.
- The rename is mechanical and the compiler is the check: a missed call site
  fails the build. The dangerous cases are the handful of names that collide
  with a framework method having a synchronous twin — renaming a call to
  `Stream.ReadAsync` into `Stream.Read` would compile and quietly turn
  asynchronous work synchronous. Those were changed by hand, not by pattern.

## Alternatives

- **Keep the suffix.** Rejected: it is a solution to a problem this codebase
  does not have, and it is the majority of every async method name here.
- **Keep it only on public API surface.** Rejected: the split has to be
  remembered, and it puts the noisiest names where they are read most.
- **Adopt it only for new code and leave the existing hundred.** Rejected: a
  convention that describes half a codebase teaches nothing, and the rename is
  a compiler-verified afternoon rather than a risk.
- **Drop the upstream `csharp-async` skill entirely.** Rejected: its other
  guidance is sound and followed. This record overrides one point of it, which
  is exactly what `Skillfile` says local guidance is for.

## Related

- [ADR-0069](ADR-0069-scannable-given-when-then-test-names.md)
- [ADR-0075](ADR-0075-tabs-over-spaces-for-indentation.md)
