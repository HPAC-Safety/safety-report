# HpacSafety.Core

The domain. Entities, enums, and the interfaces every other project depends on.

**Not deployable.** A class library, referenced by Infrastructure, Api, and
Worker.

## The rule that matters

**This project depends on nothing.** No EF Core, no HTTP client, no Anthropic
SDK, no configuration. If a dependency arrow would point out of `Core`, the
abstraction belongs here and the implementation belongs in `Infrastructure`.

That constraint exists for one reason: **the deterministic PII scrub lives
here**, and it must be provable in a plain unit test with no database, no
network, and no model. It is the first line of defence in the anonymization
pipeline and the only stage that is fully deterministic.

## Layout

Organised by **feature**, not by language construct. Each feature owns its
entities, its enums, and the ports it calls out through — see
[ADR-0018](../../docs/decisions/ADR-0018-feature-folders-in-core.md).

```
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

Namespaces match the folders exactly: `HpacSafety.Core.Features.Reporting`,
`HpacSafety.Core.SharedKernel`.

**Where does a new type go?** With the feature that owns it. A port called by one
feature lives with that feature; `ISummarizer` is reporting's, not a folder of
interfaces'. The shared kernel is for what more than one feature genuinely
shares, and it is deliberately small — two callers is the bar for adding to it.

`Reporting` depends on `QuestionBank`, because an answer is an answer *to a
question*. That dependency is one way.

## The question set is data

The form is not a fixed set of properties. `Question` and its versions are the
question bank an administrator edits without a deploy; `ReportAnswer` is one
answer to one **version** of a question, so rewording a question tomorrow cannot
change what an answer given today appears to mean.

A handful of answers additionally project onto typed properties on `Report`,
because logic reads them rather than only displaying them. Which answer projects
where comes from `QuestionRole`, and every role is optional except publication
consent. See [ADR-0016](../../docs/decisions/ADR-0016-data-driven-question-bank.md).

Two rules this project enforces and nothing downstream may relax:

- **`consent_publish` cannot be deleted, deactivated, or retyped**, and
  `Report.ConsentPublish` is `bool?` — unanswered is not the same as no. A report
  cannot be submitted until it is answered, and `MarkPublished` refuses anything
  that is not an explicit yes with both languages approved.
- **A question cannot be activated with a missing translation.** A
  machine-translated counterpart is acceptable; an absent one is not.

## Tests

`tests/HpacSafety.Core.Tests` — pure unit tests.

The golden-file redaction suite lives separately in
`tests/HpacSafety.Anonymization.Tests`, because it is the suite people should
look at first when reviewing anything privacy-related.

## Related

- [`docs/architecture.md`](../../docs/architecture.md)
- [`skills/incident-domain-model/SKILL.md`](../../skills/incident-domain-model/SKILL.md)
