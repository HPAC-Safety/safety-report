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
| Entities | `Report`, `ReportAircraft`, `ReportFile`, `Summary`, `OutboxMessage`, `AdminUser`, `AuditLogEntry` |
| Enums | `ReportStatus`, `InjurySeverity`, `AircraftClass`, `Discipline` — stable invariant codes, never display text |
| Interfaces | `ISummarizer`, `IPiiAuditor`, `ITranslator`, `IAircraftClassifier`, `IMemberAuthenticator`, `IBlobStore`, `IEmailSender`, `ITurnstileVerifier`, `IPublicationChannel` |
| Logic | The deterministic scrub |

## Tests

`tests/HpacSafety.Core.Tests` — pure unit tests.

The golden-file redaction suite lives separately in
`tests/HpacSafety.Anonymization.Tests`, because it is the suite people should
look at first when reviewing anything privacy-related.

## Related

- [`docs/architecture.md`](../../docs/architecture.md)
- [`skills/incident-domain-model/SKILL.md`](../../skills/incident-domain-model/SKILL.md)
