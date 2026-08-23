# Testing and quality

## Test strategy

Tests protect user-visible privacy and lifecycle contracts, not obsolete
internal architecture. Use fast Core unit tests for invariants, shared contract
suites for genuine ports, PostgreSQL integration tests for schema/query/
transaction behavior, API tests for HTTP and authorization, Worker tests for
outbox/model/attachment orchestration, and browser tests for the two-language end-to-
end journey.

All .NET tests use xUnit, Shouldly, and Given/When/Then structure. Integration
tests use the actual supported PostgreSQL major version through Testcontainers.
JavaScript uses `node:test`; browser journeys use Playwright. Tests must use
synthetic people, locations, reports, and attachments.

## Required contract coverage

### Questions and submission

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
- Turnstile, trusted-IP extraction, throttling, multipart count/size bounds, and
  safe localized errors fail closed; and
- report, answers, files, and all outbox work commit or roll back together.

### AI and privacy

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

- all allowed image, video, and document formats and declared-type agreement are exercised;
- configured default count and exact 50 MB boundary are covered with streaming
  tests that detect accidental whole-file buffering;
- client filenames cannot reach keys, rows, URLs, errors, or captured logs;
- image fixtures prove GPS/EXIF/profile removal after decode/re-encode;
- synthetic video fixtures prove container/device/location/timestamp metadata
  removal after remux/transcode;
- only verified image/video derivative keys yield preview URLs; validated
  malware-cleared document originals yield forced-download URLs only to
  authorized reviewers;
- document format/malware failures are inaccessible and safely logged;
- documents are never parsed into summary input, anonymized, or public, and
  active content is not inline-rendered; and
- failed database writes leave only lifecycle-expirable unreferenced quarantine
  blobs.

### Moderation, deletion, and publication

- credentials are not stored/logged and the adapter permits only the hardcoded
  TLS HPAC host; kill switch, timeout, cookie, CSRF, lockout, and revocation are
  tested;
- role matrix is tested at every admin endpoint;
- editing either language clears pair approval and removes public visibility;
- every positive publication prerequisite and every negative case is tested at
  both domain and public-query boundaries;
- public DTO serialization is an exact allowlist;
- report deletion cascade-stamps all dependents identically and stops Worker/
  public/admin flows;
- question deletion counts answers beneath deleted reports; and
- audit rows cannot be changed or deleted.

## Migration and infrastructure tests

A fresh PostgreSQL database and the supported migration from the current main
schema must both match the target model. Tests assert column types, names,
constraints, indexes, global filters, lack of application-encrypted columns,
and no `deleted` on `audit_log`.

Terraform CI runs formatting, validation, static/security checks, and a plan
without AWS credentials where possible. Assertions cover Canadian region,
private/encrypted attachments, RDS backups, separate public/admin hosting, OIDC roles,
least privilege, migration task, Turnstile configuration, and absence of SES or
long-lived keys.

## Repository quality gates

Required checks retain the repository's build, test, coverage floor plus added-
code ratchet, web asset/CSS checks, localization parity and hardcoded-string
lint, end-to-end tests, agent/skill validation, Terraform validation, and linked
issue enforcement. `DateTime` and assertion libraries other than Shouldly stay
banned through syntax-aware tests rather than fragile source grep.

Documentation changes run a local-link check, verify every tracked `src` path
is represented in [source inventory](source-inventory.md), and verify every
GitHub issue through #82 is represented in
[issue traceability](issue-traceability.md). No test fixture or specification
may contain a real reporter's personal information.
