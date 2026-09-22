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
*Verified by: REQ-QB-001, REQ-QB-009, REQ-QB-016, REQ-SUB-004, REQ-SUB-005,
REQ-SUB-009, REQ-SUB-013, REQ-SUB-017, REQ-SUB-018.*

- every display-affecting edit creates a complete immutable revision;
- current-form query examines the latest revision per key, does not resurrect an
  older active revision, and orders included active/live revisions deterministically;
- locale toggle preserves answers and revision IDs;
- unfinished answers/revision IDs remain browser-only for 15 days, no file is
  restored, and no report/API/database/object-storage write occurs before final
  submission;
- only consent is required and it has no default;
- skips are persisted for all shown answer-producing revisions, and multipart
  file indexes map exactly once to their file-upload answers;
- known superseded revisions are accepted, while unknown/deleted revisions and
  invalid historical options are rejected;
- bearer-token validation, trusted-IP extraction, throttling, multipart
  count/size bounds, and safe localized errors fail closed; and
- report, answers, files, and all outbox work commit or roll back together.

### AI and privacy

**CON-TQ-005** These contracts are covered by test.
*Verified by: REQ-AI-001, REQ-AI-009, REQ-AI-010, REQ-AI-011, REQ-AI-012,
REQ-AI-013, REQ-AI-020, REQ-AI-021.*

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
REQ-MED-008, REQ-MED-010, REQ-MED-011, REQ-MED-014.*

- all allowed image, video, and document formats and declared-type agreement are exercised;
- configured default count and exact 50 MB boundary are covered with streaming
  tests that detect accidental whole-file buffering;
- client filenames cannot reach keys, rows, URLs, errors, or captured logs;
- image fixtures prove GPS/EXIF/profile removal after decode/re-encode;
- synthetic video fixtures prove container/device/location/timestamp metadata
  removal after remux/transcode;
- only verified image/video derivative keys yield preview URLs; validated
  document originals yield forced-download URLs only to authorized reviewers;
- document format-validation failures are inaccessible and safely logged;
- documents are never parsed into summary input, anonymized, or public, and
  active content is not inline-rendered; and
- failed database writes leave only lifecycle-expirable unreferenced quarantine
  blobs.

### Moderation, deletion, and publication

**CON-TQ-007** These contracts are covered by test.
*Verified by: REQ-MOD-024, REQ-MOD-029, REQ-MOD-032, REQ-MOD-033,
REQ-MOD-035, REQ-MOD-036, REQ-MOD-040, REQ-DOM-007.*

- a token that is unsigned, signed by an unknown key, tampered with, expired,
  or issued for another audience is refused, and `alg: none` is refused;
- a token carrying no recognized role authenticates as `User`, and no claim
  beyond the subject and the role is ever read;
- the development token endpoint does not exist outside Development;
- the three-role matrix is tested at every admin endpoint;
- a submitted report contains no reference to the member who filed it;
- editing either language clears pair approval and removes public visibility;
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
as a route, deploy OIDC roles, least privilege, migration task, identity
provider configuration, and absence of SES or long-lived keys.
*Verified by: none — a rule about the tests themselves, enforced by the suites
and the CI gates rather than by a scenario.*

## Repository quality gates

**CON-TQ-009** Required checks retain the repository's build, test, coverage floor plus added-
code ratchet, web asset/CSS checks, localization parity and hardcoded-string
lint, end-to-end tests, agent/skill validation, Terraform validation, and linked
issue enforcement. Two of them guard the specification itself: a behavior change
anywhere under `src/` or in an e2e spec fails unless it touches a
`features/**/*.feature` file or cites, from a closed category vocabulary, the
existing claims it leaves standing
([ADR-0090](decisions/ADR-0090-an-exemption-cites-the-claims-it-preserves.md)),
and a committed [traceability matrix](traceability.md) that no longer matches
the claims and constraints it summarizes fails the same way a stale generated
file does
([ADR-0083](decisions/ADR-0083-specification-driven-development.md),
[ADR-0084](decisions/ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md)). `DateTime` and assertion libraries other than Shouldly stay
banned through syntax-aware tests rather than fragile source grep.

Documentation changes run a local-link check, verify every tracked `src` path
is represented in [source inventory](source-inventory.md), and verify every
GitHub issue through #82 is represented in
[issue traceability](issue-traceability.md). No test fixture or specification
may contain a real reporter's personal information.
*Verified by: none — a rule about the tests themselves, enforced by the suites
and the CI gates rather than by a scenario.*
