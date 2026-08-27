# Persistence

This slice owns every PostgreSQL table, EF Core mapping, migration, transaction,
and purpose-built query DTO. The normative target is
[`../../../docs/data-and-persistence.md`](../../../docs/data-and-persistence.md).

## Target rules

- Complete immutable question revisions contain all bilingual render,
  validation, order, privacy, active, system, and required state.
- Reports store revision-bound answers; only consent projects onto `reports`.
- One summary row stores both texts, shared provenance, and one approval.
- Final submission stores report, answers, files, and outbox messages atomically.
- Every application table except append-only `audit_log` has an irreversible
  `deleted timestamptz` and a default live-row filter.
- Summary/review/public queries are explicit DTO projections. Public output is
  an exact allowlist.
- Use `DateOnly`, `TimeOnly`, and `DateTimeOffset`, never `DateTime`.
- Use database/storage-managed encryption and TLS; no application field cipher.

`MigrateCanonicalDomainAndPersistence` (issue #79, ADR-0040) aligned current
main with the rules above: complete bilingual `question_revisions`, a
consent-only `reports` projection, one bilingual `summaries` row per report,
`Deleted`/live-row filters everywhere except `audit_log`, and no application
field cipher. Query DTOs, deletion commands, and full attachment processing
remain later work — see `docs/implementation-status.md`.

Integration tests use PostgreSQL through Testcontainers and must cover schema,
transactions, deletion filters/cascades, query DTOs, and the migration's data
transform.
