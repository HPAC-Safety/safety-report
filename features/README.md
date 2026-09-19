# HPAC Safety system specification

This directory and `docs/` together are the canonical specification for the
target HPAC Safety Occurrence Reporting system. `features/` holds every
behavior-describing page as a Cucumber-compliant `.feature` file
(Given/When/Then), each with a `README.md` alongside it for supporting detail
— tables, rationale, or current-implementation divergence notes — that
doesn't fit Gherkin. A `features/<name>/` subfolder always contains a
`.feature` file; a page with no testable scenarios belongs in `docs/`
instead. It describes the deliberately small system the repository is
intended to become.

Scenarios without `@ui` execute as xUnit tests via Reqnroll
([`tests/HpacSafety.Acceptance.Tests`](../tests/HpacSafety.Acceptance.Tests),
[ADR-0049](../docs/decisions/ADR-0049-reqnroll-for-executable-gherkin-scenarios.md)).
A scenario tagged `@ui` asserts browser-observable behavior and executes
instead through `playwright-bdd`
([`tests/e2e/steps`](../tests/e2e/steps)), which reads these same `.feature`
files directly — Reqnroll has no browser to assert against, so it is never
used for a `@ui` scenario
([ADR-0045](../docs/decisions/ADR-0045-ui-changes-require-playwright-and-server-tests.md),
[ADR-0050](../docs/decisions/ADR-0050-ui-tag-for-scenarios-needing-playwright.md),
[ADR-0053](../docs/decisions/ADR-0053-ui-scenarios-execute-via-playwright-bdd.md)).
An unimplemented scenario carries an `@ignore` tag; implementing it means
writing its step definitions — Reqnroll or `playwright-bdd`, whichever this
scenario's tag calls for — and removing that tag in the same PR.
It was derived from a file-by-file audit of the 135
tracked paths under `src/`, all 69 tracked paths under `tests/`, the
repository guidance and runtime prompts, and every open and closed GitHub issue
through issue #82. The audited implementation baseline is main at
`5f7340415e88706035a713bd8322e3dda466e821` on 2026-08-23.

## Authority and conflict rules

1. This specification defines the target design.
2. Source and tests show what is implemented today; they do not silently
   override this target.
3. Issues and ADRs preserve history and rationale. A contradictory issue, ADR,
   README, prompt, skill, test, or implementation is superseded until it is
   aligned with this specification. This resolves *inherited* drift; it is not
   license to introduce new drift — a feature file must never contradict an
   accepted ADR, and a change to one that affects the other updates both in
   the same pull request
   ([ADR-0047](../docs/decisions/ADR-0047-feature-files-must-not-contradict-adrs.md)).
4. [Implementation status](../docs/implementation-status.md) records gaps explicitly.
   A documented target feature must not be described as already working merely
   because its domain scaffold exists.
5. A future decision that changes the design must update the canonical page,
   implementation-status matrix, issue traceability, and affected tests in the
   same pull request.
6. Every user-facing requirement gets a `.feature` scenario; every durable
   architectural decision gets an ADR under `docs/decisions/`. Neither is
   optional, and neither substitutes for the other: a `.feature` file never
   argues why a technology or pattern was chosen, and an ADR never restates
   acceptance criteria.

Source, tests, historical ADRs, and issue history remain useful audit evidence.
Active READMEs, skills, and the Worker prompt are kept aligned with this
specification rather than preserving competing designs.

## Specification index

| Area | Canonical specification |
|---|---|
| Purpose, boundaries, and components | [System overview](../docs/system-overview.md) |
| Immutable bilingual questions and form assembly | [Question bank and form](question-bank-and-form/question-bank-and-form.feature) |
| Browser continuity, multipart API, DTOs, and validation | [Report submission](report-submission/report-submission.feature) |
| Report states, invariants, deletion, and retention | [Domain and lifecycle](domain-and-lifecycle/domain-and-lifecycle.feature) |
| One-call bilingual summarization and anonymization | [AI anonymization](ai-anonymization/ai-anonymization.feature) |
| Images, videos, documents, quarantine, and derivatives | [Attachments](media/media.feature) |
| Member authentication, authorization, review, and public feed | [Moderation, authentication, and publication](moderation-authentication-and-publication/moderation-authentication-and-publication.feature) |
| Target records, naming, transactions, and query DTOs | [Data and persistence](../docs/data-and-persistence.md) |
| HTTP surfaces, ports, and end-to-end data flow | [Interfaces and data flow](../docs/interfaces-and-data-flow.md) |
| React/TypeScript sites, bilingual behavior, design, and accessibility | [Web, localization, and design](web-localization-and-design/web-localization-and-design.feature) |
| Minimal AWS topology, deployment, secrets, and operations | [Infrastructure and operations](../docs/infrastructure-and-operations.md) |
| Required tests and quality gates | [Testing and quality](../docs/testing-and-quality.md) |
| Target-to-main gap analysis | [Implementation status](../docs/implementation-status.md) |
| Every audited path under `src/` | [Source inventory](../docs/source-inventory.md) |
| Every GitHub issue and its relationship to this design | [Issue traceability](../docs/issue-traceability.md) |
| Shared terms | [Glossary](../docs/glossary.md) |

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
