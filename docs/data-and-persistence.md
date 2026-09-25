---
title: Data and persistence
description: The canonical target records, naming, transactions, constraints, and query DTOs.
type: spec
area: data-and-persistence
---

# Data and persistence

## Persistence principles

**CON-DP-001** PostgreSQL is authoritative for questions, reports, answers, moderation state,
and durable work. EF Core entities enforce write invariants; API and Worker
queries project directly into purpose-specific DTOs. Persistence entities are
not serialized over HTTP or passed wholesale to the model.
*Verified by: REQ-MOD-036, REQ-AI-009.*

**CON-DP-002** Tables and columns use `snake_case`; C# uses PascalCase. External identifiers
are opaque TinyIds. Instants are `timestamptz`. Every table below except
`audit_log` has `deleted timestamptz null` mapped from `Deleted` and is covered
by a default global query filter.
*Verified by: REQ-DOM-007, REQ-DOM-011.*

**CON-DP-003** Database, backups, and object storage use AWS-managed encryption at rest and
TLS in transit. The application does not encrypt individual fields, carry an
AES key, use EF encryption converters, or maintain a second ciphertext format.
Database access, authorization, and public DTO minimization are the privacy
controls.
*Verified by: none — managed encryption is an infrastructure property, not
something a scenario can observe through the application.*

## Target records

**CON-DP-004** The physical model may combine stable question identity and revision data where
constraints permit, but it must preserve these logical records.
*Verified by: REQ-QB-019, REQ-QB-026, REQ-SUB-009.*

| Record | Essential fields and relationships |
|---|---|
| `question_revisions` | ID, question ID, revision number, type, EN/FR label, help, and placeholder, display order, private/active/system/required/translatable flags, nullable parent question ID and required parent option code the question is conditional on, nullable group question ID it renders under, created timestamp, Deleted. Unique question + revision number. Required state is authored (ADR-0061). A parent is a `yes_no` question, or a `single_select` question naming one of its live choices (ADR-0060, ADR-0074). Grouping is ADR-0076; translatable is ADR-0112. |
| `question_choices` | ID, question ID, stable choice code, nullable EN/FR label (at least one), sort order, reporter-added marker, the locale a reporter typed it in (null for an administrator's choice), Deleted. Unique question + code, including across a removed choice, so writing a removed choice again revives that row rather than creating a rival — except from a reporter, which never revives a removed choice. **Mutable and outside the revision chain** — editing choices never revises or forks the question, and a fork copies every row to the replacement. Only a reporter-added choice may lack a label, and one that does is waiting for an Administrator to supply it (ADR-0095). |
| `reports` | ID, language, status, nullable consent projections (`consent_publish`, `consent_media`, and `consent_documents`, ADR-0117, ADR-0119), submitted/published timestamps, safe summary failure state, nullable reviewer rejection note, row version (`xmin`), Deleted. No ordinary typed projections. |
| `questions` | ID, stable key, system marker, role, created timestamp, Deleted. Unique key **among live rows only** — a retired question keeps its key so a fork chain shares one (ADR-0071). |
| `report_answers` | ID, report ID, question ID, exact question revision ID, privacy snapshot, nullable string value, the locale it was given in, nullable second-language value and its source (`auto`, `human`, `choice`, or `fixed`), the translation mode fixed at submission (`none`, `choice`, `machine`, or `fixed`, ADR-0112, ADR-0127), answered/recorded timestamp, Deleted. **Every answer of every type is one string** — a select answer holds the label as shown, a boolean holds the reporter's word, `yes`/`no` or `oui`/`non` (ADR-0127), a date/time holds ISO 8601 (ADR-0072). Includes skipped shown questions, including file-upload controls. |
| `report_files` | ID, report ID, file-upload report-answer ID, attachment kind, server-minted original and nullable derivative keys, detected/safe types and sizes, processing timestamps (derivative written, document validated, ADR-0119), safe error code, nullable reviewer hide timestamp and subject (ADR-0117), Deleted. The answer identifies the exact revision. Documents normally have no derivative. The reporter's sanitized original filename, nullable, used only as a reviewer's download name ([ADR-0097](decisions/ADR-0097-a-reviewer-downloads-an-attachment-under-its-sanitized-original-name.md)); never in a key. No extracted document text. |
| `summaries` | ID, report ID (unique), `ai_summary_en`, `ai_summary_fr`, model, prompt version, how each language was produced (`source_en`, `source_fr`, ADR-0108), generated/updated timestamps, nullable ApprovedBySubject/ApprovedAt, row version (`xmin`), Deleted. One row per report. |
| `outbox_messages` | ID, aggregate/report ID, work type, identifier-only payload, occurrence/claim/retry/processed/poison metadata, Deleted. |
| `report_comments` | ID, report ID, the author's token subject (opaque, no foreign key), created timestamp, nullable hidden timestamp and hiding reviewer's subject, Deleted. A member's comment on a published report ([ADR-0114](decisions/ADR-0114-members-may-comment-on-a-published-report.md)). |
| `report_comment_revisions` | ID, comment ID, revision number (unique per comment), text, the locale it was written in, nullable machine translation and its source (`auto`), created timestamp, Deleted. Immutable once written, except that its translation is filled in once. The comment's current text is its highest revision. |
| `audit_log` | ID, acting token subject where applicable, action, target type/ID, timestamp, safe structured detail. Append-only; no Deleted column. |

**CON-DP-005** **There is no user table.** Identity and role come from claims on a validated
token, per request, and are never written down
([ADR-0065](decisions/ADR-0065-no-user-records-identity-is-the-token-subject.md)).
`summaries.approved_by_subject`, `report_comments.author_subject`,
`report_comments.hidden_by_subject`, and `audit_log.actor_subject` hold the token's
`sub` claim as an opaque `varchar(256)` string with **no foreign key** — there
is nothing to reference.
*Verified by: REQ-MOD-018.*

Audit rows written before `admin_users` was dropped keep the identifiers they
were created with: `DropAdminUsersForJwtIdentity` renames and widens the column
rather than replacing it, so an existing eleven-character tiny id survives
verbatim as a string. Those values no longer resolve to anything, and they are
left that way — the audit log is append-only, and rewriting historic
attribution would be a destructive transform that discards the only attribution
those rows ever had.

**CON-DP-006** No report, answer, file, or outbox row records who submitted a report
([ADR-0067](decisions/ADR-0067-a-reporter-must-be-a-member-and-is-not-recorded.md)).
*Verified by: REQ-SUB-020, REQ-SUB-021.*

## Constraints and indexes

**CON-DP-007** Required database protection includes the following.
*Verified by: REQ-QB-005, REQ-QB-030, REQ-QB-031.*

- unique question + revision number;
- unique question stable key among live questions only — `UNIQUE (key) WHERE
  deleted IS NULL` — so a fork chain shares one key with one live member
  (ADR-0071);
- unique choice code per question, including a removed choice (ADR-0095);
- answers indexed by report + question, deliberately not unique: a
  multi-select writes one row per chosen value (ADR-0072);
- each report file belongs to exactly one file-upload answer on the same report;
- exactly one summary row per report;
- indexes for latest-revision lookup and active/live filtering, the
  translation-pending answer queue, the live review
  queue, live public reports,
  report dependencies, unprocessed outbox rows, and question-reference deletion
  checks;
- check constraints for valid status/type codes and coherent nullable
  approval and processing fields; and
- foreign keys that prevent physical orphan rows while application code owns
  soft-delete stamping.

The question-deletion reference query uses `IgnoreQueryFilters` (or an
equivalent explicit unfiltered query). Database cascades do not implement
soft-delete timestamps because all dependent rows must receive one application
timestamp and the audit entry must share the transaction.

## Write transactions

**CON-DP-008** Final submission writes the report, answer snapshot, file metadata, and all
initial outbox messages in one transaction.
*Verified by: REQ-SUB-013, REQ-SUB-014.* A committed report therefore
always has durable work; an uncommitted report never appears to the Worker.

**CON-DP-009** The Worker claims eligible messages with PostgreSQL locking that permits
multiple workers without double-processing, such as `FOR UPDATE SKIP LOCKED`.
It rechecks report deletion and current work state, keeps claims short, records
bounded attempts/backoff, and marks poison work visibly. Model/network work does
not hold a database transaction open.
*Verified by: REQ-AI-008.*

**CON-DP-010** Question revision creation, summary edit/approval/publication, report deletion,
and admin changes each write their audit record in the same transaction as the
state change.
*Verified by: REQ-DOM-013, REQ-MOD-029.*

## Query DTOs

**CON-DP-011** The application needs four primary read shapes, and a public query is a
positive allowlist rather than an entity projection with fields removed later.
*Verified by: REQ-MOD-031, REQ-MOD-036.*


1. Current form DTO: latest revision per key only when it is active/live, with
   both languages and all render/validation metadata.
2. Summarization DTO: answered fields partitioned into `report_content` and
   `private_context`, labeled in the report language.
3. Admin review DTO: exact asked questions and answers, privacy, attachment
   state/authorized links, status, summary pair, and provenance.
4. Public report DTO: only ID, both summary texts, publication timestamp, and
   the number of visible comments. A report's own page also lists each public
   file's opaque id, kind, and, for a document only, its coarse format, read
   from `public_report_media` (ADR-0117, ADR-0119). A public comment carries its ID, current
   text, language, machine translation, timestamps, and whether it was edited,
   and never its author.

Each query selects only its required columns. In particular, public queries are
positive allowlists rather than entity projections with fields removed later.

The public report DTO is read from the `public_reports` view, never from the
tables. The view states the whole publication invariant in SQL, including
nonblank summary texts. Its columns are the allowlist itself: `id`,
`ai_summary_en`, `ai_summary_fr`, `published_at`, and `comment_count`. So a
public query cannot reach a column the view does not carry. Comments are read
from `public_report_comments`, which joins to `public_reports`, so a report's
comments are public exactly while the report is. Its one non-public column,
`author_subject`, is compared on the server to compute `isMine` and is never
serialized
([ADR-0055](decisions/ADR-0055-ef-core-migrations-sql-files-stored-procedures.md)).
A report's public files are read from `public_report_media`, which also joins
to `public_reports`. It lists a live, unhidden image or video with a verified
derivative when `consent_media` is true, and a live, unhidden, validated
document when `consent_documents` is true. Its key columns are for the server
to mint a link and are never serialized
([ADR-0117](decisions/ADR-0117-a-published-report-shows-the-reporters-photos-and-video.md),
[ADR-0119](decisions/ADR-0119-a-published-report-offers-its-documents-for-download.md)).

The admin side reads its rules from views in the same way
([ADR-0116](decisions/ADR-0116-a-read-rule-lives-in-a-view.md)):

- `admin_report_queue` is every live report, with `is_stuck` and
  `needs_action` computed in SQL.
- `answers_awaiting_translation` is every live answer still waiting for its
  machine-translated second language.
- `admin_pending_counts` is one row counting the reports that need action and
  the answers awaiting translation.

The report list and its filters, the translation queue, and the Admin menu's
counts all read these views. So they share one definition of "stuck", "needs
action", and "awaiting".

## Migrations and seeding

**CON-DP-012** Schema changes are EF migrations. The API and the Worker each
apply pending migrations at startup, under a PostgreSQL advisory lock that
re-checks after it is taken, before serving traffic or polling the outbox.
There is no migration deploy step
([ADR-0055](decisions/ADR-0055-ef-core-migrations-sql-files-stored-procedures.md)).
*Verified by: none — a startup property no running scenario observes;
`MigrationRunner` and its tests are its check.* A migration may seed the
initial Typeform-derived bilingual question revisions with deterministic IDs.
Admin access is a role claim on the identity provider's token; no migration
seeds any person or role
([ADR-0064](decisions/ADR-0064-jwt-bearer-authentication-with-three-roles.md),
[ADR-0065](decisions/ADR-0065-no-user-records-identity-is-the-token-subject.md)).

The canonical-schema migration removed the ordinary report projections,
one-locale summary rows, translation links, and field-encryption converters,
converted question data to complete revisions without losing references, added
Deleted consistently, and preserved audit and outbox history
([ADR-0040](decisions/ADR-0040-migrate-canonical-domain-and-persistence.md)).
Migration tests exercise both a fresh database and the supported upgrade path.
