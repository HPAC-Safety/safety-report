# Data and persistence

## Persistence principles

PostgreSQL is authoritative for questions, reports, answers, moderation state,
and durable work. EF Core entities enforce write invariants; API and Worker
queries project directly into purpose-specific DTOs. Persistence entities are
not serialized over HTTP or passed wholesale to the model.

Tables and columns use `snake_case`; C# uses PascalCase. External identifiers
are opaque TinyIds. Instants are `timestamptz`. Every table below except
`audit_log` has `deleted timestamptz null` mapped from `Deleted` and is covered
by a default global query filter.

Database, backups, and object storage use AWS-managed encryption at rest and
TLS in transit. The application does not encrypt individual fields, carry an
AES key, use EF encryption converters, or maintain a second ciphertext format.
Database access, authorization, and public DTO minimization are the privacy
controls.

## Target records

The physical model may combine stable question identity and revision data where
constraints permit, but it must preserve these logical records:

| Record | Essential fields and relationships |
|---|---|
| `question_revisions` | ID, stable key, revision number, superseded revision ID, EN/FR label and help, type, sort order, section, privacy/active/system/required flags, created timestamp, Deleted. Unique stable key + revision number. |
| `question_revision_options` | ID, revision ID, stable option code, EN/FR label, sort order, Deleted. Unique revision + code. |
| `reports` | ID, language, status, nullable ConsentPublish projection, submitted/published timestamps, safe summary failure state, Deleted. No ordinary typed projections. |
| `report_answers` | ID, report ID, exact question revision ID, privacy snapshot, nullable scalar value or selected option-code representation, answered/recorded timestamp, Deleted. Includes skipped shown questions, including file-upload controls. |
| `report_files` | ID, report ID, file-upload report-answer ID, attachment kind, server-minted original and nullable derivative keys, detected/safe types and sizes, processing status/timestamps, safe error code, Deleted. The answer identifies the exact revision. Documents normally have no derivative. No client filename or extracted document text. |
| `summaries` | ID, report ID (unique), `ai_summary_en`, `ai_summary_fr`, model, prompt version, generated/updated timestamps, nullable ApprovedBy/ApprovedAt, Deleted. One row per report. |
| `admin_users` | ID, upstream member identifier (unique among live rows), role, active/created metadata, Deleted. Never credentials. |
| `outbox_messages` | ID, aggregate/report ID, work type, identifier-only payload, occurrence/claim/retry/processed/poison metadata, Deleted. |
| `audit_log` | ID, actor ID where applicable, action, target type/ID, timestamp, safe structured detail. Append-only; no Deleted column. |

Selected options may instead use immutable child rows when that gives stronger
constraints. Whichever representation is used must distinguish a skipped
selection from selected codes and validate codes against the exact revision.

## Constraints and indexes

Required database protection includes:

- unique question stable key + revision number and at most one stable-key
  revision chain link;
- unique option code within a revision;
- unique answer per report + question revision and, at the application layer,
  at most one revision of the same stable key per report;
- each report file belongs to exactly one file-upload answer on the same report;
- exactly one summary row per report;
- indexes for latest-revision lookup and active/live filtering, the live review
  queue, live public reports,
  report dependencies, unprocessed outbox rows, and question-reference deletion
  checks;
- check constraints for valid status/role/type codes and coherent nullable
  approval and processing fields; and
- foreign keys that prevent physical orphan rows while application code owns
  soft-delete stamping.

The question-deletion reference query uses `IgnoreQueryFilters` (or an
equivalent explicit unfiltered query). Database cascades do not implement
soft-delete timestamps because all dependent rows must receive one application
timestamp and the audit entry must share the transaction.

## Write transactions

Final submission writes the report, answer snapshot, file metadata, and all
initial outbox messages in one transaction. A committed report therefore
always has durable work; an uncommitted report never appears to the Worker.

The Worker claims eligible messages with PostgreSQL locking that permits
multiple workers without double-processing, such as `FOR UPDATE SKIP LOCKED`.
It rechecks report deletion and current work state, keeps claims short, records
bounded attempts/backoff, and marks poison work visibly. Model/network work does
not hold a database transaction open.

Question revision creation, summary edit/approval/publication, report deletion,
and admin changes each write their audit record in the same transaction as the
state change.

## Query DTOs

The application needs four primary read shapes:

1. Current form DTO: latest revision per key only when it is active/live, with
   both languages and all render/validation metadata.
2. Summarization DTO: answered fields partitioned into `report_content` and
   `private_context`, labeled in the report language.
3. Admin review DTO: exact asked questions and answers, privacy, attachment
   state/authorized links, status, summary pair, and provenance.
4. Public report DTO: only ID, both summary texts, and publication timestamp.

Each query selects only its required columns. In particular, public queries are
positive allowlists rather than entity projections with fields removed later.

## Migrations and seeding

Schema changes are explicit EF migrations run as a deployment step before new
API/Worker traffic; services never migrate on startup. A migration may seed the
initial Typeform-derived bilingual question revisions with deterministic IDs.
Production admin membership is configuration/data managed through the admin
flow, not a real identity embedded in a migration. Development may seed one
obviously synthetic admin under an environment guard.

The migration from the current schema must remove ordinary report projections,
one-locale summary rows, translation links, and field-encryption converters;
convert question data to complete revisions without losing references; add
Deleted consistently; and preserve audit/outbox history. Migration tests must
exercise both a fresh database and the supported upgrade path.
