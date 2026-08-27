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

Current main has a normalized partial question-version model, typed report
projections, per-language summary rows, application AES converters, and no
universal soft deletion. The alignment migration must support both a fresh
database and an upgrade from that schema.

Integration tests use PostgreSQL through Testcontainers and must cover schema,
transactions, deletion filters/cascades, query DTOs, and both migration paths.
