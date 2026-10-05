---
name: persist-hpac-data
description: Implement HPAC Safety EF Core records, migrations, transactions, soft deletion, and purpose-built query DTOs. Use for persistence or database changes.
---

# Persist HPAC Safety data

Extends the `design-ef-core-model` skill for how an
entity, relationship, or query is mapped; read that first. This skill wins
where they differ.

- Target schema: the data-and-persistence constraint page.
- Schema changes, raw SQL, and how migrations are applied:
  [`manage-hpac-migrations`](../manage-hpac-migrations/SKILL.md) (the
  migrations, SQL files, and stored procedures decision).
- Tables and sensitivity tiers: [`hpac-domain-model`](../hpac-domain-model/SKILL.md).

## Records

- PostgreSQL is `snake_case`; C# is PascalCase.
- Store complete immutable question revisions and revision-bound answers. Only
  consent projects onto the report: `consent_publish`, `consent_media`
  (the published-photos-and-video decision), and `consent_documents`, which is `consent_media` when it
  answered the question's current wording (the published-documents decision).
- One summary row per report: English/French text, shared generation
  provenance (model, prompt version), a per-language source (the reviewer-may-machine-translate decision), and
  pair approval.
- Save report, answers, file rows, and typed outbox messages in one
  transaction.
- No user, member, or session entity — identity lives in the token.

## Queries

- The summary DTO returns exact revision labels, answers, and privacy flags.
- The public DTO never carries raw answers or an image or video original.
  `public_report_media` is the one file-shaped public read: opaque id, kind,
  and a document's download format. A published document's unchanged original
  is reachable only as a short-lived forced download through `PublicMediaLink`
  (the published-photos-and-video and published-documents decisions).

## Soft deletion

- `deleted timestamptz` and a default filter on every table except append-only
  `audit_log` and hard-deleted `pending_import_logic` (the Typeform import decision).
  `question_choices` and `question_choice_parents` have the column but no
  default filter, because their aggregate reads removed rows (the question-owns-its-choices and shared-dependent-choice decisions).
- Reference checks for question deletion include answers beneath deleted
  reports.
- Never physically delete records or add restore behavior (`AGENTS.md`
  invariant 8 lists the carved exceptions).

## Encryption

- AWS-managed encryption at rest and TLS.
