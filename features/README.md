# HPAC Safety system specification

This directory is the canonical specification for the target HPAC Safety
Occurrence Reporting system. It describes the deliberately small system the
repository is intended to become. It was derived from a file-by-file audit of
the 135 tracked paths under `src/`, all 69 tracked paths under `tests/`, the
repository guidance and runtime prompts, and every open and closed GitHub issue
through issue #82. The audited implementation baseline is main at
`5f7340415e88706035a713bd8322e3dda466e821` on 2026-08-23.

## Authority and conflict rules

1. This specification defines the target design.
2. Source and tests show what is implemented today; they do not silently
   override this target.
3. Issues and ADRs preserve history and rationale. A contradictory issue, ADR,
   README, prompt, skill, test, or implementation is superseded until it is
   aligned with this specification.
4. [Implementation status](implementation-status/implementation-status.md) records gaps explicitly.
   A documented target feature must not be described as already working merely
   because its domain scaffold exists.
5. A future decision that changes the design must update the canonical page,
   implementation-status matrix, issue traceability, and affected tests in the
   same pull request.

Source, tests, historical ADRs, and issue history remain useful audit evidence.
Active READMEs, skills, and the Worker prompt are kept aligned with this
specification rather than preserving competing designs.

## Specification index

| Area | Canonical specification |
|---|---|
| Purpose, boundaries, and components | [System overview](system-overview/system-overview.md) |
| Immutable bilingual questions and form assembly | [Question bank and form](question-bank-and-form/question-bank-and-form.feature) |
| Browser continuity, multipart API, DTOs, and validation | [Report submission](report-submission/report-submission.feature) |
| Report states, invariants, deletion, and retention | [Domain and lifecycle](domain-and-lifecycle/domain-and-lifecycle.feature) |
| One-call bilingual summarization and anonymization | [AI anonymization](ai-anonymization/ai-anonymization.feature) |
| Images, videos, documents, quarantine, and derivatives | [Attachments](media/media.feature) |
| Member authentication, authorization, review, and public feed | [Moderation, authentication, and publication](moderation-authentication-and-publication/moderation-authentication-and-publication.feature) |
| Target records, naming, transactions, and query DTOs | [Data and persistence](data-and-persistence/data-and-persistence.md) |
| HTTP surfaces, ports, and end-to-end data flow | [Interfaces and data flow](interfaces-and-data-flow/interfaces-and-data-flow.md) |
| Static sites, bilingual behavior, design, and accessibility | [Web, localization, and design](web-localization-and-design/web-localization-and-design.feature) |
| Minimal AWS topology, deployment, secrets, and operations | [Infrastructure and operations](infrastructure-and-operations/infrastructure-and-operations.md) |
| Required tests and quality gates | [Testing and quality](testing-and-quality/testing-and-quality.md) |
| Target-to-main gap analysis | [Implementation status](implementation-status/implementation-status.md) |
| Every audited path under `src/` | [Source inventory](source-inventory/source-inventory.md) |
| Every GitHub issue and its relationship to this design | [Issue traceability](issue-traceability/issue-traceability.md) |
| Shared terms | [Glossary](glossary/glossary.md) |

Behavior-describing pages under `features/` are Cucumber-compliant
`.feature` files (Given/When/Then); each has a `README.md` alongside it for
supporting detail — tables, rationale, or current-implementation divergence
notes — that doesn't fit Gherkin. Pure reference/audit pages stay `.md`.

## Product contract in one paragraph

A reporter sees the latest active immutable revision of each bilingual database
question in its configured order, may skip every ordinary question, must make
an explicit publication-consent choice, and submits the answers and optional
attachments once. The API saves the report, exact question revisions, files, and
outbox work atomically. The Worker makes exactly one model call using one
versioned prompt to produce an anonymized English/French summary pair, using
private answers only as recognition context. A safety officer reviews that pair
and permitted attachments. Only a non-deleted, positively consented report
with a human-approved pair can appear in the public feed.

## Simplicity guardrails

The target deliberately writes no respondent report data server-side before the
one final submission. It has no server-side report drafts, pre-submit upload
sessions, deterministic text scrubber, separate PII-audit call, translation
call, specialized aircraft processing, outbound email, external publication
channels, application-layer field encryption, restore workflow, or automated
raw-report purge. New abstractions are justified by a real boundary or a second
implementation, not by a hypothetical future.
