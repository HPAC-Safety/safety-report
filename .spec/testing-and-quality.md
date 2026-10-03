---
title: Testing and quality
description: The canonical test strategy, required contract coverage, and quality gates.
type: spec
area: testing-and-quality
---

# Testing and quality

## Test strategy

**CON-TQ-001** Tests protect user-visible privacy and lifecycle contracts, not obsolete
internal architecture.
*Verified by: none — a rule about what the suites are for, not about what the
system does.* Use fast Core unit tests for invariants, shared contract
suites for genuine ports, PostgreSQL integration tests for schema/query/
transaction behavior, API tests for HTTP and authorization, Worker tests for
outbox/model/attachment orchestration, and browser tests for the two-language end-to-
end journey.

**CON-TQ-002** All .NET tests use xUnit, Shouldly, and Given/When/Then structure. Integration
tests use the actual supported PostgreSQL major version through Testcontainers.
JavaScript uses `node:test`; browser journeys use Playwright. Tests must use
synthetic people, locations, reports, and attachments.
*Verified by: none — a rule about the tests themselves, enforced by the suites
and the CI gates rather than by a scenario.*

**CON-TQ-003** *Verified by: none — a delivery rule, enforced by the `feature-coverage` job
and review.*
A UI behavior change ships with a Playwright test and, when it touches or
relies on API behavior, a server-side test covering that behavior
([ADR-0045](decisions/ADR-0045-ui-changes-require-playwright-and-server-tests.md)).

## Required contract coverage

### Questions and submission

**CON-TQ-004** These contracts are covered by test.
*Verified by: REQ-QB-001, REQ-QB-009, REQ-QB-016, REQ-SUB-078, REQ-SUB-005,
REQ-SUB-010, REQ-SUB-013, REQ-SUB-017, REQ-SUB-018, REQ-SUB-124, REQ-SUB-125.*

- every display-affecting edit to an unanswered question creates a complete
  immutable revision, an edit to an answered one forks it (ADR-0071), and an
  edit to choices alone changes them in place (ADR-0095); an Administrator
  fixes a picker option in place or replaces it (ADR-0128);
- current-form query examines the latest revision per key, does not resurrect an
  older active revision, and orders included active/live revisions deterministically;
- locale toggle preserves answers and revision IDs;
- unfinished answers/revision IDs remain browser-only for 15 days, finished
  uploads are restored with the saved report, and nothing but an attach-time
  upload to quarantine is written before final submission (ADR-0096,
  ADR-0100);
- consent is always required and has no default, and any other question is
  required only when its revision says so (ADR-0061);
- skips are persisted for all shown answer-producing revisions, and each upload
  ID a submission names maps exactly once to its file-upload answer;
- a submission names only current revisions: unknown, deleted, and superseded
  revisions are rejected, and so are invalid options; the browser drops a saved
  answer to a non-current revision when it restores a saved report, and says so
  once (ADR-0185);
- bearer-token validation, trusted-IP extraction, throttling, attachment
  count and size bounds, and safe localized errors fail closed; and
- report, answers, files, and all outbox work commit or roll back together.

### AI and privacy

**CON-TQ-005** These contracts are covered by test.
*Verified by: REQ-AI-001, REQ-AI-009, REQ-AI-011, REQ-AI-020, REQ-AI-021,
REQ-AI-024. What the model writes is the reviewer's checklist in the
[AI anonymization detail](features/ai-anonymization/README.md#reviewer-checklist).*

- partitioning never puts a private field in `report_content` and never treats
  private-only facts as summary facts;
- the model adapter is invoked exactly once per attempt with one prompt version;
- strict output accepts exactly the two required strings and rejects fences,
  extra/missing keys, nulls, blank text, and malformed JSON;
- bilingual golden cases remove full identities and use exact role phrases such
  as “the pilot” / “le pilote,” with no identity fragment;
- make/model and precise identifying detail are absent while safety-relevant
  facts remain;
- bounded failures reach `SummaryFailed` and manual authoring recovers; and
- logs/exceptions never contain report content, private context, prompts,
  responses, or tokens.

Model behavior tests use a deterministic fake at the application boundary.
Prompt evaluation/golden cases may run separately and must not make CI depend on
a live third-party provider or send real incident data.

### Attachments

**CON-TQ-006** These contracts are covered by test.
*Verified by: REQ-MED-001, REQ-MED-002, REQ-MED-003, REQ-MED-006, REQ-MED-007,
REQ-MED-008, REQ-MED-010, REQ-MED-011, REQ-MED-025, REQ-MED-026, REQ-MED-037,
REQ-MED-039.*

- all allowed image, video, and document formats and declared-type agreement are exercised;
- configured default count and each kind's exact size boundary (250 MB video,
  25 MB image or document) are covered, the claim's re-check against the
  detected kind included, with streaming tests that detect accidental
  whole-file buffering;
- client filenames are sanitized, stored only on the report file, and reach only a
  reviewer's download link — never keys, errors, captured logs, model input, or
  public DTOs;
- image fixtures prove GPS/EXIF/profile removal after decode/re-encode;
- synthetic video fixtures prove container/device/location/timestamp metadata
  removal after remux into MP4 (ADR-0094, ADR-0122);
- only verified image/video derivative keys yield preview URLs; validated
  document originals yield forced-download URLs only to authorized reviewers,
  or to anyone once the document is public, under a server-minted name;
- document format-validation failures are inaccessible and safely logged;
- documents are never parsed into summary input or anonymized, and active
  content is not inline-rendered; and
- failed database writes leave only lifecycle-expirable unreferenced quarantine
  blobs.

### Moderation, deletion, and publication

**CON-TQ-007** These contracts are covered by test.
*Verified by: REQ-MOD-024, REQ-MOD-029, REQ-MOD-032, REQ-MOD-033,
REQ-MOD-035, REQ-MOD-036, REQ-DOM-007.*

- a token that is unsigned, signed by an unknown key, tampered with, expired,
  or issued for another audience is refused, and `alg: none` is refused;
- a token carrying no recognized role authenticates as `User`, and no claim
  beyond the subject and the role is ever read;
- the development token endpoint does not exist outside Development, unless
  the temporary staging interim issuer is enabled (ADR-0172);
- the three-role matrix is tested at every admin endpoint;
- a submitted report contains no reference to the member who filed it;
- an edit to a report that is not live is an unapproved draft and is never
  public; an edit to a Published report is approved by its author and public at
  once, and the public reads only the latest approved revision (ADR-0177);
- every positive publication prerequisite and every negative case is tested at
  both domain and public-query boundaries;
- public DTO serialization is an exact allowlist;
- report deletion cascade-stamps all dependents identically and stops Worker/
  public/admin flows;
- question deletion counts answers beneath deleted reports; and
- audit rows cannot be changed or deleted.

## Migration and infrastructure tests

**CON-TQ-008** A fresh PostgreSQL database and the supported migration from the current main
schema must both match the target model. Tests assert column types, names,
constraints, indexes, global filters, lack of application-encrypted columns,
and no `deleted` on `audit_log`.

Terraform CI runs formatting, validation, static/security checks, and a plan
without AWS credentials where possible. Assertions cover Canadian region,
private/encrypted attachments, RDS backups, one website with the admin surface
as a route, deploy OIDC roles, least privilege, identity
provider configuration, and absence of SES or long-lived keys.
*Verified by: none — a rule about the tests themselves, enforced by the suites
and the CI gates rather than by a scenario.*

## Repository quality gates

**CON-TQ-009** Required checks retain the repository's build, test, coverage floor plus added-
code ratchet, web asset/CSS checks, localization parity and hardcoded-string
lint, end-to-end tests, agent/skill validation, Terraform validation, and linked
issue enforcement. Two of them guard the specification itself: a behavior change
anywhere under `src/` or in an e2e spec fails unless it changes a scenario in an
area its code maps to, or cites, from a closed category vocabulary, existing
claims of those areas that it leaves standing
([ADR-0090](decisions/ADR-0090-an-exemption-cites-the-claims-it-preserves.md),
[CONV-001](conventions/CONV-001-a-scenario-counts-only-in-its-own-area-and-an-ignored-one-names-its-issue.md)),
and a committed [traceability matrix](traceability.md) or
[`claims.json`](claims.json) that no longer matches the claims and constraints
it summarizes fails the same way a stale generated
file does
([ADR-0083](decisions/ADR-0083-specification-driven-development.md),
[ADR-0084](decisions/ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md)). `System.DateTime` and `Xunit.Assert` stay
banned through the BannedApiAnalyzers analyzer and `tests/BannedSymbols.txt`,
not through source grep (ADR-0013, ADR-0035).

[Source inventory](../docs/source-inventory.md) maps every `src/` project and
directory; `tools/docs/check-inventories.ts` fails the required `docs` job when it
drifts. [Issue traceability](../docs/issue-traceability.md) lists every open issue,
generated from GitHub by `tools/spec/generate-issue-traceability.ts`;
`tools/spec/check-issue-traceability.ts` compares it with what the generator
would write daily and on every push to `main` from its own non-required
workflow, and keeps one drift issue open instead of failing a pull request,
because open issues change without any commit
([ADR-0143](decisions/ADR-0143-issue-traceability-drift-opens-an-issue-and-gates-nothing.md),
[ADR-0191](decisions/ADR-0191-each-rule-is-stated-once-and-no-status-page-is-written-by-hand.md)).
No test fixture or specification
may contain a real reporter's personal information.
*Verified by: none — a rule about the tests themselves, enforced by the suites
and the CI gates rather than by a scenario.*

**CON-TQ-010** Every step of a built claim — a scenario not tagged `@ignore` —
is bound to a step definition in its engine: Reqnroll for a claim without
`@ui`, playwright-bdd for one with it. The specification is the authority, so a
step no definition matches fails the required `docs` job, and the generated
[`claims.json`](claims.json) records which files bind each claim's steps, the
ambiguous steps, the `@ignore` claims whose steps are already bound, and the
step definitions no scenario uses
([ADR-0184](decisions/ADR-0184-a-generated-map-binds-every-claim-to-its-step-definitions.md),
[ADR-0193](decisions/ADR-0193-the-claims-are-generated-as-json-a-graph-fragment-and-one-slim-matrix.md)).
*Verified by: none — a rule about the specification and the tests together,
enforced by `tools/spec/generate-traceability.ts` rather than by a scenario.*

**CON-TQ-011** A built claim counts only when its scenario passed in the run:
every pickle (each Examples row of an outline) of a claim not tagged `@ignore`
ran and passed in its engine — Reqnroll in `test`, playwright-bdd in `e2e` —
and the required `coverage` job fails a built claim of an engine that ran with
a failing, skipped, or unexecuted scenario. Each claim's per-run result is in
that job's summary and its `claim-results` artifact, never in a committed file;
[`claims.json`](claims.json) keeps only the static status, `Planned` or `Built`
([ADR-0195](decisions/ADR-0195-a-built-claim-counts-only-when-its-scenario-passed-in-the-run.md)).
*Verified by: none — a rule about the tests and the specification together,
enforced by `tools/spec/check-claim-results.ts` rather than by a scenario.*

**CON-TQ-012** Every behavior-bearing path maps to at least one feature area in
[`area-paths.json`](area-paths.json), checked by the required `docs` job; a
scenario edit or an exemption counts toward `feature-coverage` only in an area
the changed files map to, and a whitespace or comment edit counts as none. A
scenario may merge `@ignore` ahead of its code only while it carries one
`@issue-<N>` tag naming an open issue, which no pull request closes while the
scenario is still `@ignore`
([CONV-001](conventions/CONV-001-a-scenario-counts-only-in-its-own-area-and-an-ignored-one-names-its-issue.md)).
*Verified by: none — a delivery rule, enforced by `tools/spec/check-area-paths.ts`,
`tools/spec/check-feature-coverage-diff.ts`, and `tools/spec/check-ignored-claims.ts`
rather than by a scenario.*
