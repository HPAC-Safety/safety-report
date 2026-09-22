---
title: Core has no SharedKernel folder; cross-cutting types sit at the namespace root
description: Cross-cutting types move to the HpacSafety.Core namespace root rather than a named SharedKernel child folder, leaving feature folders unchanged.
type: adr
status: accepted
date: 2026-09-18
decision-makers: Chase Florell
keywords: Core architecture, namespaces, shared kernel
---

# ADR-0041: `Core` has no `SharedKernel` folder; cross-cutting types sit at the namespace root

**Status:** Accepted. Supersedes [ADR-0018](ADR-0018-feature-folders-in-core.md)'s
`SharedKernel/` folder. ADR-0018's feature-folder decision otherwise stands.
**Date:** 2026-09-18

## Context

ADR-0018 put genuinely cross-cutting types (`TinyId`, `Locale`,
`DomainRuleViolationException`, `IBlobStore`, `IFieldCipher`, and similar) in
`src/HpacSafety.Core/SharedKernel/`, namespace `HpacSafety.Core.SharedKernel`.

Every feature namespace is nested under `HpacSafety.Core`
(`HpacSafety.Core.Features.Reporting`, `HpacSafety.Core.Features.QuestionBank`,
…), but `HpacSafety.Core.SharedKernel` is a *sibling* of those feature
namespaces, not their parent. C# does not look at sibling namespaces without an
explicit `using`, so every feature file that touched a shared-kernel type
carried `using HpacSafety.Core.SharedKernel;` — one import, repeated across
nearly every file in `Core`, for types that are supposed to be the most
ordinary, load-bearing vocabulary in the domain.

## Decision

Cross-cutting types move to `src/HpacSafety.Core/`, namespace `HpacSafety.Core`
— the root, not a named child folder. Feature folders and their
`HpacSafety.Core.Features.*` namespaces are unchanged.

```
src/HpacSafety.Core/
  Features/
    Reporting/      HpacSafety.Core.Features.Reporting
    QuestionBank/    HpacSafety.Core.Features.QuestionBank
    Moderation/      HpacSafety.Core.Features.Moderation
    Outbox/          HpacSafety.Core.Features.Outbox
  TinyId.cs, Locale.cs, DomainRuleViolationException.cs,
  IBlobStore.cs, IFieldCipher.cs, IEmailSender.cs, ITranslator.cs,
  ITurnstileVerifier.cs, EnumCode.cs, MediaCompartment.cs,
  BlobKey.cs, BlobUrlLifetime.cs, FieldDecryptionException.cs   ← HpacSafety.Core
```

Because every `Features.*` namespace nests under `HpacSafety.Core`, C#
resolves an unqualified `TinyId` or `Locale` there without any `using` — the
same reachability ADR-0018 already relies on within a single feature. The
membership test for "does this type belong at the root" is unchanged from
ADR-0018's `SharedKernel` bar: genuinely used by more than one feature, not a
port for a retired feature, not something with exactly one caller.

## Consequences

- Every `using HpacSafety.Core.SharedKernel;` line is deleted; nothing
  replaces it, because root-namespace types no longer need importing from
  inside `Features.*`.
- The root folder holds only files that clear the shared-kernel bar. It is not
  a place to drop a type because it has no other obvious home — that is what
  `Features/` review already guards against, unchanged from ADR-0018.
- `docs/source-inventory.md` and any other doc that names a `SharedKernel/`
  path is stale and must be corrected to the flat `src/HpacSafety.Core/` path
  in the same change that lands this ADR.
- Existing ADRs (0019, 0022, 0026, 0033, 0034) that mention
  `HpacSafety.Core.SharedKernel` are historical snapshots of a design that has
  since moved; per `docs/decisions/README.md` they are not corrected for this,
  the same way ADR-0018 was never corrected for earlier layouts it replaced.

## Alternatives rejected

**Keep `SharedKernel/` and add `using HpacSafety.Core.SharedKernel;` as an
implicit/global using.** Works, but hides the dependency: a global using makes
every file in the project implicitly reach into `SharedKernel` whether or not
it actually uses it, and a reader checking a file's own `using` list stops
being able to tell whether the file touches shared-kernel types at all.

**Rename `SharedKernel/` to something nested under `Features/`, e.g.
`Features/Shared/`.** Fixes the sibling-namespace problem but stretches
`Features/` to include a "feature" that is not one — the folder becomes the
same kind of miscellaneous bucket ADR-0018 was written to avoid, just one
level deeper.

**Leave it as ADR-0018 specified.** The repeated `using` line is not fatal on
its own, but it is friction on the exact types meant to be the cheapest to
reach — the ones DDD's shared-kernel term already warns will be touched
everywhere.

## Related

- [ADR-0018](ADR-0018-feature-folders-in-core.md) — feature-folder decision
  this ADR narrows; the feature-folder/namespace-parity rule is unchanged.
- `docs/source-inventory.md`, `src/HpacSafety.Core/README.md`
