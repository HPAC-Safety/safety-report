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
path. Do not physically delete records, add restore behavior, or hide a schema
change in runtime startup.

## Document schema changes with a Mermaid diagram

Any PR that adds, removes, or restructures a table, column, relationship, or
constraint must include a Mermaid `erDiagram` showing the resulting shape of
the affected tables (not the whole database) in the PR description. Put the
diagram in the ADR too when the change is significant enough to warrant one.
Show table names in `snake_case`, primary/foreign keys, and relationship
cardinality; omit unaffected tables and columns that don't help the reviewer
see what changed.
