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

It is not an aspiration:
[`CoreDependencyTests`](../../tests/HpacSafety.Anonymization.Tests/CoreDependencyTests.cs)
fails the day this project grows a reference — it checks both the project file
and the assembly Core actually compiled against.

**"Zero package references" is shorthand, and it is not literally true.**
`Directory.Build.props` injects `Roslynator.Analyzers` into every project here,
so `dotnet list package` reports it for Core as well. It is analyzer-only with
`PrivateAssets=all` and contributes nothing to the compiled output. The precise
claim, and the one the test proves, is **zero runtime dependencies**: Core
compiles against nothing but the framework.
`Features/Anonymization/` has its own
[README](Features/Anonymization/README.md) — read it before changing anything
in there.

That same rule is why `ReportAircraft` does **not** classify the reporter's
certification answer. Every answer on the form, aircraft certification
included, is stored here exactly as submitted and nothing in `Core`
normalizes, infers, or otherwise derives a second value from it. Turning a raw
certification answer into a publishable class is the summarizer's job, done at
summarization time under a versioned prompt in `prompts/`, not a `Core`
concern. See
[ADR-0036](../../docs/decisions/ADR-0036-classification-moves-to-the-summarization-prompt.md).

## Layout

Organised by **feature**, not by language construct. Each feature owns its
entities, its enums, and the ports it calls out through — see
[ADR-0018](../../docs/decisions/ADR-0018-feature-folders-in-core.md).

```
Features/
  Anonymization/  DeterministicScrub, ScrubRequest, ScrubField, ScrubFieldKind,
                  ScrubbedReport, ScrubVocabulary, ScrubMarker,
                  Stages/ (internal — the chain of responsibility)
  Reporting/      Report, ReportAnswer, ReportAircraft, ReportFile, Summary,
                  ReportStatus, InjurySeverity, Discipline, PilotRating,
                  TimeOfDay, Province,
                  MediaType, MediaPolicy, MediaValidation,
                  MediaRejectionReason, MediaRejection, MediaKind,
                  MediaIngestor, MediaIngestOutcome, MediaIngestStatus,
                  MediaUploadSlot, ReviewerMediaLink,
                  ISummarizer, IPiiAuditor, IPublicationChannel,
                  IMediaSniffer, IExifStripper
  QuestionBank/   Question, QuestionVersion, QuestionOption,
                  QuestionTranslation, QuestionOptionTranslation,
                  QuestionRole, QuestionKey, QuestionType
  Moderation/     AdminUser, AdminRole, AuditLogEntry, AuditAction,
                  IMemberAuthenticator
  Outbox/         OutboxMessage
SharedKernel/     TinyId, Locale, EnumCode, SensitivityTier,
                  BlobKey, MediaCompartment, BlobUrlLifetime,
                  DomainRuleViolationException, FieldDecryptionException,
                  ITranslator, IBlobStore, IEmailSender, ITurnstileVerifier,
                  IFieldCipher
```

Namespaces match the folders exactly: `HpacSafety.Core.Features.Reporting`,
`HpacSafety.Core.SharedKernel`.

**Where does a new type go?** With the feature that owns it. A port called by one
feature lives with that feature; `ISummarizer` is reporting's, not a folder of
interfaces'. The shared kernel is for what more than one feature genuinely
shares, and it is deliberately small — two callers is the bar for adding to it.

`Reporting` depends on `QuestionBank`, because an answer is an answer *to a
question*. That dependency is one way.

`TinyId` is the identifier every entity in every feature carries — eleven
characters, unguessable, and carrying no timestamp, because this system
deliberately does not let a report be pinned to a moment. It is in the shared
kernel because every feature has rows, and because #16 builds a blob key out of
one. See [ADR-0034](../../docs/decisions/ADR-0034-tiny-ids.md).

`IFieldCipher` is in the shared kernel rather than with a feature because the
rule it carries belongs to the whole system: Restricted data is encrypted at
rest (`docs/data-handling.md`). The algorithm, the key, and the wiring into EF
Core are infrastructure. See
[ADR-0019](../../docs/decisions/ADR-0019-application-side-field-encryption.md).

## One concession to persistence

Every aggregate here carries a **private parameterless constructor**, marked as
existing for EF Core. The ORM materializes an entity by calling a constructor and
then setting the mapped properties, and these aggregates have none it can bind.

It is the only concession. Domain code still has to go through the real
constructor or factory, so no caller can reach a half-built aggregate, and this
project still references nothing.

## The question set is data

The form is not a fixed set of properties. `Question` and its versions are the
question bank an administrator edits without a deploy; `ReportAnswer` is one
answer to one **version** of a question, so rewording a question tomorrow cannot
change what an answer given today appears to mean.

A handful of answers additionally project onto typed properties on `Report`,
because logic reads them rather than only displaying them. Which answer projects
where comes from `QuestionRole`, and every role is optional except publication
consent. See [ADR-0016](../../docs/decisions/ADR-0016-data-driven-question-bank.md).

## Uploaded media

`MediaIngestor` is in `Core` for the same reason the scrub is: the order it runs
in — sniff, validate, *then* promote out of quarantine — is what makes "a
reviewer only ever sees stripped bytes" true, and it is provable in a plain unit
test with no bucket and no imaging library. `IMediaSniffer` and `IExifStripper`
are ports; Magick.NET lives in `Infrastructure`.

Three types carry rules that would otherwise be call-site discipline:

- **`BlobKey`** is in the shared kernel because a key is attacker-influenced and
  must be parsed, never accepted as a string — and because the storage layout is
  a rule. It parses exactly three shapes, all namespaced by a report id, so a key
  outside the layout is unrepresentable.
- **`MediaUploadSlot`** is the only thing that mints an upload URL, and it never
  names the compartment: an upload can only ever land in quarantine.
- **`ReviewerMediaLink`** is the only sanctioned way to mint a link to media.
  Storage will sign a URL for any key, including the original; this issues one
  only for `MediaCompartment.Stripped`.

`MediaIngestStatus` has three states, not two. A video is *accepted* — the
original is the Restricted record like any other upload — but nothing can strip
it yet, so there is no derivative and nothing viewable. Asking for one throws
rather than falling through to the unstripped original. See #65. See
[ADR-0025](../../docs/decisions/ADR-0025-magick-net-for-exif-stripping.md) and
[ADR-0026](../../docs/decisions/ADR-0026-presigned-urls-and-private-blob-storage.md).

Two rules this project enforces and nothing downstream may relax:

- **`consent_publish` cannot be deleted, deactivated, or retyped**, and
  `Report.ConsentPublish` is `bool?` — unanswered is not the same as no. A report
  cannot be submitted until it is answered, and `MarkPublished` refuses anything
  that is not an explicit yes with both languages approved.
- **A question cannot be activated with a missing translation.** A
  machine-translated counterpart is acceptable; an absent one is not.

The reporter gives a real date and a real clock time, and the coarse
`TimeOfDay` bucket is **derived** from the time by
`TimeOfDay.FromLocalTime(TimeOnly)` — the one place the boundaries are written
down, called by both the projection here and the scrub in #18. A time that was
never given is `TimeOfDay.Unknown`, a defined state rather than a midnight
nobody meant. See
[ADR-0019](../../docs/decisions/ADR-0019-application-side-field-encryption.md)
and #68.

## Tests

`tests/HpacSafety.Core.Tests` — pure unit tests.

The golden-file redaction suite lives separately in
`tests/HpacSafety.Anonymization.Tests`, because it is the suite people should
look at first when reviewing anything privacy-related.

## Related

- [`docs/architecture.md`](../../docs/architecture.md)
- [`skills/incident-domain-model/SKILL.md`](../../skills/incident-domain-model/SKILL.md)
