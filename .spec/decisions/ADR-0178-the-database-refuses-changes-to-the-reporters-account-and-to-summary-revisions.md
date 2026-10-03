---
title: The database refuses changes to the reporter's account and to summary revisions
description: Column-scoped BEFORE UPDATE OR DELETE triggers on reports, report_answers, report_files, and summary_revisions make PostgreSQL refuse a rewrite of what the reporter said or what a reviewer saved. The locked and writable columns are listed, DELETE is refused, and a migration that must touch a locked column disables the trigger inside its own transaction.
type: adr
status: accepted
date: 2026-09-29
decision-makers: Chase Florell
keywords: immutability, trigger, report_answers, report_files, reports, summary_revisions, soft delete, migration, ADR-0073, ADR-0174, ADR-0177, ADR-0178
---

# ADR-0178 — The database refuses changes to the reporter's account and to summary revisions

**Status:** Accepted. Enforces, in the database, the immutability that
[ADR-0072](ADR-0072-every-answer-is-stored-as-a-string.md),
[ADR-0128](ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md),
[ADR-0130](ADR-0130-a-yes-or-no-answer-is-stored-as-a-boolean.md),
[ADR-0174](ADR-0174-an-answers-second-language-is-written-once-by-the-worker-only.md),
and [ADR-0177](ADR-0177-summaries-are-append-only-revisions-and-a-live-edit-publishes-itself.md)
already state for the domain. Part of
[#665](https://github.com/HPAC-Safety/safety-report/issues/665), decided by the
owner on 2026-09-29 in
[#669](https://github.com/HPAC-Safety/safety-report/issues/669). No earlier ADR
is superseded.

## Context

A reporter's account is immutable only because the domain says so: `private
init`, and no mutator. A stray `UPDATE`, a migration, or a raw-SQL path could
still rewrite what a reporter said, or a saved summary revision. It is the lesson
of [ADR-0073](ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md) again: a
guard that lives only in the caller is not a guard.
`AGENTS.md` invariant 8 already says no application record is physically
deleted; nothing but convention held that either.

## Decision

**PostgreSQL refuses the change.** Four functions and four column-scoped
`BEFORE UPDATE OR DELETE ... FOR EACH ROW` triggers —
`report_answers_immutable`, `report_files_immutable`, `reports_immutable`, and
`summary_revisions_immutable` — raise an exception (`SQLSTATE 23000`) that names
the table and the column, never a value.

- **The trigger fires only for an `UPDATE` that names a guarded column**, so a
  write to an unguarded column (a report's status, a file's `hidden_at`) never
  runs it. An `UPDATE` that names a guarded column but leaves its value as it
  was is not a change, and passes.
- **`DELETE` and `TRUNCATE` are refused on all four tables.** Retirement is the
  `deleted` stamp. `TRUNCATE` empties a table below any row trigger, so each
  table also has a statement-level `BEFORE TRUNCATE` trigger, which a
  `TRUNCATE … CASCADE` from another table fires too (owner, 2026-09-30).
  The report's soft-delete cascade stamps `deleted` and nothing else, so it
  commits.
- **No metadata table.** A report's metadata stays on `reports` and stays
  writable; its history is the audit log.

### The columns

| Table | Locked: any change is refused | Written once: null to a value, then locked | Writable |
|---|---|---|---|
| `report_answers` | `id`, `report_id`, `question_id`, `question_revision_id`, `question_key`, `is_private`, `value`, `value_boolean`, `choice_id`, `locale`, `translation_mode`, `answered_at` | `translated_value`, `translation_source` (ADR-0174), `deleted` | none |
| `report_files` | `id`, `report_id`, `report_answer_id`, `kind`, `blob_key`, `original_file_name`, `content_type`, `byte_size`, `uploaded_at` | none | `stripped_blob_key`, `exif_stripped_at`, `validated_at`, `processing_error_code`, `hidden_at`, `hidden_by_subject`, `deleted` |
| `reports` | `id`, `language`, `submitted_at`, `consent_publish`, `consent_media`, `consent_documents` | none | `status`, `published_at`, `unpublish_note`, `summary_error`, `deleted` |
| `summary_revisions` | `id`, `summary_id`, `sequence`, `ai_summary_en`, `ai_summary_fr`, `source_en`, `source_fr`, `model`, `prompt_version`, `author_subject`, `created_at`, `restored_from_id` | `deleted` | `approved_at`, `approved_by_subject` — set, and cleared back to null, because Unpublish clears the latest revision's approval ([ADR-0177](ADR-0177-summaries-are-append-only-revisions-and-a-live-edit-publishes-itself.md)) |

The lists were fixed against `ReportFile`, `ReportAnswer`, `Report`, and
`SummaryRevision`, and against every path that writes these tables: the
submission (which inserts every guarded column once and updates none), the
Worker's derivative and validation processing, a reviewer's hide and show,
the Worker's one write of an answer's second language, approval and unpublish,
and the report's soft-delete cascade. The identity keys are locked with the rest,
because nothing legitimate moves a row to another parent.

### A future migration that must change a locked column

A migration that has to rewrite a locked column disables the table's trigger
inside its own transaction, makes the one change, and enables it again before the
transaction ends:

```sql
ALTER TABLE report_answers DISABLE TRIGGER report_answers_immutable;
-- the one change
ALTER TABLE report_answers ENABLE TRIGGER report_answers_immutable;
```

Each such migration needs **its own ADR argument** for why a reporter's account or
a saved revision may change. **There is no session setting, role, or runtime flag
that bypasses a trigger**, and none may be added: a bypass the application could
reach is the guard-in-the-caller this decision removes. Tests that build an
impossible row on purpose, to prove a view still refuses it, do the same thing in
their own transaction.

### What is not built

- Row-level security, `REVOKE`, or a separate database role. The trigger's owner
  is the role that runs migrations; a guard against a hostile superuser is not the
  goal here.
- A guard on any other table. Questions and choices have their own revision rules
  ([ADR-0071](ADR-0071-an-answered-question-forks-instead-of-revising.md),
  [ADR-0095](ADR-0095-a-question-owns-its-choices-outside-its-revisions.md)).

## Considered options

Recorded inline above: each rejected option sits beside the part of the decision it bears on.

## Consequences

- A rewrite of a reporter's account, or of a saved revision, by any path fails
  loudly, in the transaction that tried it.
- Past migrations are unaffected: the triggers do not exist while they run, and
  the migration that creates them touches no row.
- A migration `Down` for an earlier migration that rewrites a locked column runs
  after this one's `Down` has removed the triggers.
- The raw SQL is `Persistence/Sql/20260930051851_RefuseChangesToTheReportersAccount.sql`,
  with a `.Down.sql`, and the squash baseline carries the functions and triggers
  forward with the views.
