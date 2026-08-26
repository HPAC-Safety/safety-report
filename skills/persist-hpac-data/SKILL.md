---
name: persist-hpac-data
description: Implement HPAC Safety EF Core records, migrations, transactions, soft deletion, and purpose-built query DTOs. Use for persistence or database changes.
---

# Persist HPAC Safety data

Follow [`features/data-and-persistence/data-and-persistence.md`](../../features/data-and-persistence/data-and-persistence.md).

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
