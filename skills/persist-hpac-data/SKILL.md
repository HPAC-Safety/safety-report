---
name: persist-hpac-data
description: Implement HPAC Safety EF Core records, migrations, transactions, soft deletion, and purpose-built query DTOs. Use for persistence or database changes.
---

# Persist HPAC Safety data

- Target schema: [`docs/data-and-persistence.md`](../../docs/data-and-persistence.md).
- Schema changes, raw SQL, and how migrations are applied:
  [`manage-hpac-migrations`](../manage-hpac-migrations/SKILL.md)
  ([ADR-0055](../../docs/decisions/ADR-0055-ef-core-migrations-sql-files-stored-procedures.md)).
- Tables and sensitivity tiers: [`incident-domain-model`](../incident-domain-model/SKILL.md).

## Records

- PostgreSQL is `snake_case`; C# is PascalCase.
- Store complete immutable question revisions and revision-bound answers. Only
  consent projects onto the report.
- One summary row per report: English/French text, shared provenance, pair
  approval.
- Save report, answers, file rows, and typed outbox messages in one
  transaction.
- No user, member, or session entity — identity lives in the token.

## Queries

- Query purpose-built DTOs holding exactly the fields a use case needs.
- The summary DTO returns exact revision labels, answers, and privacy flags.
- The public DTO cannot carry raw answers or attachments.

## Soft deletion

- `deleted timestamptz` and a default filter on every table except append-only
  `audit_log`.
- Cascade soft deletion explicitly, with one timestamp.
- Reference checks for question deletion include answers beneath deleted
  reports.
- Never physically delete records or add restore behavior (`AGENTS.md`
  invariant 8 lists the carved exceptions).

## Encryption

- AWS-managed encryption at rest and TLS.
- Remove application AES keys, ciphertext converters, and field-cipher ports.
