# ADR-0018: `Core` is organised by feature, with a shared kernel

**Status:** Accepted
**Date:** 2026-08-22

## Context

`HpacSafety.Core` grew a layout that mixed two ways of dividing code:

```
Questions/  Reports/  Administration/  Outbox/     ← features
Enums/  Values/  Abstractions/                     ← technical layers
```

Both conventions are defensible on their own. Together they leave every file
with two plausible homes and no rule that says which. `InjurySeverity` is an
enum and it is part of reporting; `ISummarizer` is an abstraction and it is part
of reporting. Whichever folder it lands in, the next person looks in the other
one first.

The technical folders also grow without bound and describe nothing. `Enums/`
holds nine unrelated types whose only shared property is a language keyword.
Opening it tells you what C# feature they use, not what the system does.

## Decision

Every feature is a folder under `Features/`, and it owns everything it needs:
entities, enums, and the ports it calls out through. Genuinely cross-cutting
types live in `SharedKernel/`.

```
src/HpacSafety.Core/
  Features/
    Reporting/      Report, ReportAnswer, ReportAircraft, ReportFile, Summary,
                    ReportStatus, InjurySeverity, AircraftClass, Discipline,
                    PilotRating, TimeOfDay, Province,
                    ISummarizer, IPiiAuditor, IAircraftClassifier,
                    IPublicationChannel
    QuestionBank/   Question, QuestionVersion, QuestionOption,
                    QuestionTranslation, QuestionOptionTranslation,
                    QuestionRole, QuestionKey, QuestionType
    Moderation/     AdminUser, AdminRole, AuditLogEntry, AuditAction,
                    IMemberAuthenticator
    Outbox/         OutboxMessage
  SharedKernel/     Locale, EnumCode, SensitivityTier,
                    DomainRuleViolationException,
                    ITranslator, IBlobStore, IEmailSender, ITurnstileVerifier
```

Folders and namespaces match exactly: `HpacSafety.Core.Features.Reporting`,
`HpacSafety.Core.SharedKernel`. A file's namespace is derivable from its path
and vice versa, with no exceptions to remember.

**`SharedKernel` is DDD's own term** for the small set of types more than one
part of the domain agrees to share, and the name carries a warning with it: a
shared kernel is expensive to change because everything depends on it. Four
things qualify today —

- `Locale`, because every feature is bilingual.
- `SensitivityTier`, because the data-handling tiers are a property of the whole
  system.
- `DomainRuleViolationException`, because every aggregate throws it.
- `ITranslator`, `IBlobStore`, `IEmailSender`, `ITurnstileVerifier` — ports with
  no single owning feature. `ITranslator` in particular is called by both
  summarization and the question builder.

A port used by exactly one feature lives *with* that feature. `ISummarizer`
belongs to reporting, not to a folder of interfaces.

## Consequences

- Where a new type goes has one answer: with the feature that owns it. If two
  features own it, it is a shared-kernel candidate and worth a moment's thought.
- Deleting a feature is deleting a folder.
- `Features/` in the namespace is three extra characters in a `using`. The
  alternative — folders that do not match namespaces — costs more, every day,
  in a repository where an agent is often navigating by path.
- The shared kernel will attract things that do not belong in it. Adding to it
  should be as visible as this ADR made it; two callers is the bar, not one
  caller and a hunch.
- Cross-feature references are allowed and real: `Reporting` depends on
  `QuestionBank` because an answer is an answer *to a question*. That direction
  is one-way, and worth keeping that way.

## Alternatives rejected

**Leave it as it was.** Two conventions, no rule, and a folder called `Enums/`
that grows forever.

**Fully technical layout** — `Entities/`, `Enums/`, `Interfaces/`. Conventional,
and it tells a reader nothing about what the system does. It also spreads one
change across three folders, which is exactly what feature folders exist to
stop.

**Feature folders at the project root, no `Features/` parent.** Shorter
namespaces, but the root would still mix features with the shared folder — a
smaller version of the problem being fixed.

**`Shared/` instead of `SharedKernel/`.** One word shorter and says less.
`SharedKernel` names a known pattern, and the pattern comes with the caution
about growth that a folder called `Shared` never conveys.

## Related

- [ADR-0016](ADR-0016-data-driven-question-bank.md)
- `docs/architecture.md`, `src/HpacSafety.Core/README.md`
- The `ddd` skill, and `AGENTS.md` — "Domain-driven design, and test-first"
