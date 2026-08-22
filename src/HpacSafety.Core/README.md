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

## Contents

| | |
|---|---|
| `Reports/` | `Report`, `ReportAnswer`, `ReportAircraft`, `ReportFile`, `Summary` |
| `Questions/` | `Question`, `QuestionVersion`, `QuestionOption`, `QuestionTranslation`, `QuestionOptionTranslation`, `QuestionRole`, `QuestionKey` |
| `Outbox/` | `OutboxMessage` — backoff and the poison threshold |
| `Administration/` | `AdminUser`, `AdminRole`, `AuditLogEntry`, `AuditAction` |
| `Enums/` | `ReportStatus`, `InjurySeverity`, `AircraftClass`, `Discipline`, `PilotRating`, `TimeOfDay`, `Province`, `QuestionType`, `SensitivityTier` — stable invariant codes, never display text |
| `Values/` | `Locale`, `EnumCode` |
| `Abstractions/` | `ISummarizer`, `IPiiAuditor`, `ITranslator`, `IAircraftClassifier`, `IMemberAuthenticator`, `IBlobStore`, `IEmailSender`, `ITurnstileVerifier`, `IPublicationChannel` |
| Logic | The deterministic scrub |

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
