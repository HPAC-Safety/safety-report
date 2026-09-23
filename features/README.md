---
title: HPAC Safety system specification
description: "The canonical target design: the authority rules, the specification index, and the product contract."
type: spec
area: index
---

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
The Reqnroll suite skips a `@ui` scenario itself, so it reports as skipped
wherever that suite runs rather than failing for want of a C# step definition
it is never meant to have
([ADR-0073](../docs/decisions/ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md)).
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

7. Every scenario carries one stable claim ID as a tag, and a normative
   constraint on a canonical `docs/` page carries a `CON-*` ID naming what
   verifies it
   ([ADR-0084](../docs/decisions/ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md)).
   An ID is never reused or renumbered, and
   [the traceability matrix](../docs/traceability.md) is generated from these
   files rather than maintained by hand.
8. Behavior is specified before it is implemented, and a wrong behavior is
   corrected here rather than argued in a conversation
   ([ADR-0083](../docs/decisions/ADR-0083-specification-driven-development.md)).
   Each area page also records what **not** to build in that area, because a
   page that states only the target invites an implementation to over-deliver
   into territory nobody asked for. The global boundary in
   [system overview](../docs/system-overview.md) still holds; a per-area
   section narrows it, and never contradicts it.

Source, tests, historical ADRs, and issue history remain useful audit evidence.
Active READMEs, skills, and the Worker prompt are kept aligned with this
specification rather than preserving competing designs.

## Specification index

| Area | Canonical specification |
|---|---|
| Purpose, boundaries, and components | [System overview](../docs/system-overview.md) |
| Immutable bilingual questions and form assembly | [Question bank and form](question-bank-and-form/question-bank-and-form.feature) |
| Importing/exporting the question bank as Typeform JSON | [Typeform question import and export](typeform-question-import-export/typeform-question-import-export.feature) |
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

A reporter signs in as an HPAC member — which proves membership and is never
recorded against the report — sees the latest active immutable revision of each
bilingual database question in its configured order, may skip every ordinary
question, must make an explicit publication-consent choice, and submits the
answers once. Each optional attachment uploads as soon as it is attached, into
private quarantine, and the submission claims it. Every answer is stored as one string —
the words the reporter saw, in the language they saw them. The API saves the
report, exact question revisions, files, and
outbox work atomically. The Worker makes exactly one model call using one
versioned prompt to produce an anonymized English/French summary pair, using
private answers only as recognition context. A safety officer reviews that pair
and permitted attachments. Only a non-deleted, positively consented report
with a human-approved pair can appear in the public feed.

## Simplicity guardrails

The target deliberately writes no respondent report data server-side before the
one final submission, with one argued exception: an attachment uploads to
private quarantine when it is attached, names no member, has no database row,
and expires unless a submission claims it
([ADR-0096](../docs/decisions/ADR-0096-an-attachment-uploads-on-attach-and-is-claimed-at-submission.md)).
It has no server-side report drafts, resumable upload protocol,
deterministic text scrubber, separate PII-audit call, translation
call, specialized aircraft processing, outbound email, external publication
channels, application-layer field encryption, restore workflow, or automated
raw-report purge. New abstractions are justified by a real boundary or a second
implementation, not by a hypothetical future.

Authentication is the one external identity dependency, and it is deliberately
thin: an identity provider signs a token, the API validates it and reads two
claims, and **no user record is stored anywhere**
([ADR-0064](../docs/decisions/ADR-0064-jwt-bearer-authentication-with-three-roles.md),
[ADR-0065](../docs/decisions/ADR-0065-no-user-records-identity-is-the-token-subject.md)).
There is no allowlist, no user table, no session store, no CSRF machinery, no
password handling, and no Turnstile. Requiring a member to submit is what let
the last of those go
([ADR-0068](../docs/decisions/ADR-0068-the-member-token-replaces-turnstile-on-submission.md)).
