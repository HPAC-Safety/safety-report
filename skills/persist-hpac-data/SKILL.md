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
  consent projects onto the report: `consent_publish`, `consent_media`
  (ADR-0117), and `consent_documents`, which is `consent_media` when it
  answered the question's current wording (ADR-0119).
- One summary row per report: English/French text, shared generation
  provenance (model, prompt version), a per-language source (ADR-0108), and
  pair approval.
- Save report, answers, file rows, and typed outbox messages in one
  transaction.
- No user, member, or session entity — identity lives in the token.

## Queries

- Query purpose-built DTOs holding exactly the fields a use case needs.
- The summary DTO returns exact revision labels, answers, and privacy flags.
- The public DTO never carries raw answers or an image or video original.
  `public_report_media` is the one file-shaped public read: opaque id, kind,
  and a document's download format. A published document's unchanged original
  is reachable only as a short-lived forced download through `PublicMediaLink`
  (ADR-0117, ADR-0119).

## Soft deletion

- `deleted timestamptz` and a default filter on every table except append-only
  `audit_log` and hard-deleted `pending_import_logic` (ADR-0077).
  `question_choices` has the column but no default filter, because its
  aggregate reads removed rows (ADR-0095).
- Cascade soft deletion explicitly, with one timestamp.
- Reference checks for question deletion include answers beneath deleted
  reports.
- Never physically delete records or add restore behavior (`AGENTS.md`
  invariant 8 lists the carved exceptions).

## Encryption

- AWS-managed encryption at rest and TLS.
