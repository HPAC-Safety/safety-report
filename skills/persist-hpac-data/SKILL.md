---
name: persist-hpac-data
description: Implement HPAC Safety EF Core records, migrations, transactions, soft deletion, and purpose-built query DTOs. Use for persistence or database changes.
---

# Persist HPAC Safety data

Follow [`../../docs/data-and-persistence.md`](../../docs/data-and-persistence.md).

- Use PostgreSQL `snake_case`; C# uses PascalCase. Use `DateOnly`, `TimeOnly`,
  and `DateTimeOffset`, never `DateTime`.
- Store complete immutable question revisions and revision-bound answers. Only
  consent projects onto the report.
- Store one summary row per report with English/French text, shared provenance,
  and pair approval.
- Save report, answers, file rows, and typed outbox messages in one transaction.
- Query purpose-built DTOs containing exactly the fields a use case needs. The
  summary DTO returns exact revision labels, answers, and privacy flags; the
  public DTO cannot carry raw answers or attachments.
- Add `deleted timestamptz` and default filters everywhere except append-only
  `audit_log`. Cascade soft deletion explicitly with one timestamp. Reference
  checks for question deletion include answers beneath deleted reports.
- Use AWS-managed encryption at rest and TLS. Remove application AES keys,
  ciphertext converters, and field-cipher ports.

Migrations must support both a fresh database and the current-main upgrade
path. Do not physically delete records or add restore behavior — the sole
carved exception is dropping `admin_users`
([ADR-0065](../../docs/decisions/ADR-0065-no-user-records-identity-is-the-token-subject.md)).
Do not add a user, member, or session entity: identity lives in the token.

## Schema changes, raw SQL, and migration application (ADR-0055)

- Every schema change is an EF Core migration reviewed under
  `src/HpacSafety.Infrastructure/Persistence/Migrations/`. No hand-written
  DDL, and no other means of changing the schema.
- Plain LINQ is fine for simple queries. A query that is hard to express or
  maintain as LINQ (multi-table aggregation, a cross-cutting report) becomes a
  PostgreSQL view or stored procedure instead of a large LINQ expression.
- Any raw SQL — a view, a stored procedure, or a migration data-transform —
  lives in its own `.sql` file under
  `src/HpacSafety.Infrastructure/Persistence/Sql/`, not as an inline C#
  string. Existing inline SQL in past migrations is not rewritten
  retroactively.
- There is no dedicated deploy-time migrate job. Both `HpacSafety.Api` and
  `HpacSafety.Worker` call `HpacSafetyDbContext.EnsureMigratedAsync` at
  startup, which takes a PostgreSQL advisory lock, re-checks pending
  migrations after acquiring it, and applies them if still pending. This is
  what makes "the Worker started before the API" after a deploy safe.

## Document schema changes with a Mermaid diagram

Any PR that adds, removes, or restructures a table, column, relationship, or
constraint must include a Mermaid `erDiagram` showing the resulting shape of
the affected tables (not the whole database) in the PR description. Put the
diagram in the ADR too when the change is significant enough to warrant one.
Show table names in `snake_case`, primary/foreign keys, and relationship
cardinality; omit unaffected tables and columns that don't help the reviewer
see what changed.
