---
title: The HPAC Safety database
description: One PostgreSQL database, one DbContext, and the one way to change the schema.
type: readme
---

# The HPAC Safety database

One PostgreSQL database, one `DbContext`
([`HpacSafetyDbContext`](../HpacSafetyDbContext.cs)), and one way to change the
schema: an EF Core migration in this folder. There is no second context for a
"worker schema" or a "reporting schema," and no hand-written DDL anywhere ([ADR-0055](../../../../docs/decisions/ADR-0055-ef-core-migrations-sql-files-stored-procedures.md)).

This file describes the database. The target *design* lives in
[`docs/data-and-persistence.md`](../../../../docs/data-and-persistence.md) and
[`/features`](../../../../features/README.md); where they disagree with this
page, they win and this page is stale.

## Four conventions, applied everywhere

**Every key is an eleven-character tiny id.** `char(11)` over
`A-Za-z0-9-_`, case-sensitive, cryptographically random. One column type in
every table, so there are no mixed-type joins, and — the actual reason — an
identifier encodes no creation time and cannot be enumerated. A report id ends
up in URLs, blob keys, and logs, and this system narrows a published occurrence
to a month and a year on purpose; a UUIDv7 would hand the timestamp back ([ADR-0034](../../../../docs/decisions/ADR-0034-tiny-ids.md)).

**Nothing is physically deleted.** Every table except `audit_log` carries
`deleted timestamptz null` and a default query filter limiting reads to live
rows, applied by
[`SoftDeleteFilters`](../Conventions/SoftDeleteFilters.cs). `audit_log` is
append-only and has no such column. A reference check that must see deleted
rows — "has any answer, on any report including deleted ones, used this
revision?" — uses `IgnoreQueryFilters` explicitly.

**Names are `snake_case` in the database and PascalCase in C#**, translated by
[`SnakeCaseNames`](../Conventions/SnakeCaseNames.cs) last in
`OnModelCreating`, so anything named explicitly keeps the name it was given.

**Enums are stored as invariant codes, never integers** — `long_text`, not
`1` — via [`EnumCodeConverter`](../Conversions/EnumCodeConverter.cs), with a
`CHECK` constraint listing the valid codes. A row is readable in `psql` without
the enum beside it, and reordering an enum member cannot silently reinterpret
history.

Dates and times follow
[ADR-0035](../../../../docs/decisions/ADR-0035-dateonly-datetimeoffset-timeonly-datetime-is-banned.md):
`DateOnly` when the time does not matter, `TimeOnly` when the date does not,
`DateTimeOffset` for an instant. `DateTime` is banned and the build enforces it.

## The schema

```mermaid
erDiagram
    questions ||--o{ question_revisions : "versions"
    questions ||--o{ question_choices : "its own choices"
    question_choices |o--o{ question_choices : "merged into"
    question_revisions }o--o| questions : "conditional on"
    question_revisions }o--o| question_choices : "requires"
    question_choices |o--o| question_choices : "replaced by"

    reports ||--o{ report_answers : "answers"
    reports ||--o{ report_files : "attachments"
    reports ||--|| summaries : "one summary"
    question_revisions ||--o{ report_answers : "answered under"
    question_choices |o--o{ report_answers : "named by"
    report_answers ||--o{ report_files : "uploaded for"

    questions {
        char(11) id PK
        varchar(128) key UK "stable, invariant, never changes"
        varchar(64) role "none | consent_publish"
        boolean is_system "true only for publication consent"
        timestamptz created_at
        timestamptz deleted
    }

    question_revisions {
        char(11) id PK "what an answer points at"
        char(11) question_id FK
        int revision_number "1, 2, 3 … unique per question"
        varchar(64) type
        boolean is_system
        boolean is_required "authored; forced true for consent"
        boolean is_private "answers are recognition context only"
        boolean is_translatable "free text machine-translated for reviewers"
        boolean is_active
        int display_order
        char(11) depends_on_question_id FK "nullable; a yes_no or single_select question"
        char(11) depends_on_choice_id FK "nullable; the single_select parent's required choice, followed through replacements"
        text label_en
        text label_fr
        text help_text_en
        text help_text_fr
        timestamptz created_at
        timestamptz deleted
    }

    question_choices {
        char(11) id PK
        char(11) question_id FK
        varchar(128) code UK "unique per question, removed rows included"
        int display_order
        text label_en "null only on a reporter choice typed in French, or a removed one made for an old answer"
        text label_fr "null only on a reporter choice typed in English, or a removed one made for an old answer"
        boolean added_by_reporter "typed into a type-ahead"
        varchar(8) reporter_locale "the language a reporter typed it in"
        varchar(16) label_en_source "human or auto; null while label_en is (ADR-0129)"
        varchar(16) label_fr_source "human or auto; null while label_fr is (ADR-0129)"
        boolean needs_review "a type-ahead value awaiting a reviewer (ADR-0129)"
        timestamptz reviewed_at "last approved, corrected, or removed"
        varchar(256) reviewed_by "the reviewer's token subject; joins to nothing"
        timestamptz created_at "when a reporter added it; null before this was recorded"
        char(11) replaced_by_choice_id FK "nullable; the picker option that replaced this one"
        char(11) merged_into_choice_id FK "the value a merged one reads as; set only on a removed value (ADR-0129)"
        timestamptz deleted "removed; hidden from the form, never erased"
    }

    reports {
        char(11) id PK
        varchar(8) language "the locale the reporter wrote in"
        varchar(64) status
        boolean consent_publish "the only projected answer"
        timestamptz submitted_at
        timestamptz published_at
        timestamptz deleted
    }

    report_answers {
        char(11) id PK
        char(11) report_id FK
        char(11) question_revision_id FK "the exact revision shown"
        boolean is_private "privacy as it was at the time"
        char(11) choice_id FK "the choice a select or type-ahead answer names (ADR-0128)"
        text value "null for a choice, yes/no, or checkbox answer; a select answer from before ADR-0128 keeps its label"
        boolean value_boolean "yes/no and checkbox (ADR-0130)"
        text translated_value "the other language, when it has one"
        varchar(64) translation_mode "none, choice, or machine (ADR-0112)"
        timestamptz deleted
    }

    report_files {
        char(11) id PK
        char(11) report_id FK
        char(11) report_answer_id FK
        varchar(64) attachment_kind
        text blob_key "named by the file's own id; never a client filename"
        varchar(255) original_file_name "sanitized; a reviewer's download name only"
        timestamptz deleted
    }

    summaries {
        char(11) id PK
        char(11) report_id FK "unique: one summary per report"
        text ai_summary_en
        text ai_summary_fr
        text prompt_version
        timestamptz approved_at "one approval covers the pair"
        timestamptz deleted
    }

    outbox_messages {
        char(11) id PK
        char(11) aggregate_id "names a row with no foreign key"
        varchar(64) type
        text payload "identifiers only, never report content"
        timestamptz processed_at
        timestamptz deleted
    }

    audit_log {
        char(11) id PK
        varchar(256) actor_subject "token subject; opaque, no foreign key"
        varchar(64) action
        char(11) target_id "names a row with no foreign key"
        timestamptz occurred_at
    }
```

The columns above are the ones worth knowing to read the schema, not every
column in it. The generated migrations are exhaustive; this is a map.

### What the shape is protecting

**A question is not a row — it is a chain of complete revisions.**
`questions` holds only what never changes: the stable key, whether it is the
system question, and its role. Everything a reporter could see — wording, type,
order, section, privacy, required state, and conditionality — lives on
`question_revisions`, and an edit inserts a new one rather than updating the
old. `report_answers` points at a revision, never at a question, so
a report filed two years ago still renders exactly what it asked ([ADR-0016](../../../../docs/decisions/ADR-0016-data-driven-question-bank.md)).

**Choices belong to the question, not to a revision.** `question_choices` is
edited in place: adding, rewording, reordering, or removing a choice creates no
revision and never forks the question. A single-select, multi-select, or
type-ahead answer names its choice through `report_answers.choice_id` and reads
both labels there, so a choice an answer names is never erased
([ADR-0128](../../../../docs/decisions/ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md)).
A removed choice keeps its row with `deleted` stamped, and is loaded with its
question — the one table without the live-row filter — because a fork copies
it, an old answer still names it, and a reporter must not revive it. A type-ahead's reporter-added choice may hold one language until an
administrator supplies the other ([ADR-0095](../../../../docs/decisions/ADR-0095-a-question-owns-its-choices-outside-its-revisions.md)).

**One rule is deliberately not in the database.** "A conditional question's
parent must be a `yes_no` question" depends on the parent's *current* revision —
a different row — so a `CHECK` cannot express it and a trigger would hide a
domain rule from everyone reading the C#. It is enforced in
`QuestionDependencies` and at the API instead ([ADR-0060](../../../../docs/decisions/ADR-0060-conditional-questions-depend-on-a-boolean-question.md)).

**`outbox_messages.aggregate_id` and `audit_log.target_id` have no foreign
key**, on purpose: each names more than one kind of row. Because EF cannot fix
those up, `HpacSafetyDbContext` rewrites them explicitly when a tiny-id
collision forces a retry.

## Who applies a migration

The application does, at startup, and no deploy job exists for it. Both the API
and the Worker call `HpacSafetyDbContext.EnsureMigratedAsync`, which takes a
PostgreSQL advisory lock, re-checks for pending migrations *after* acquiring it,
and applies them only if any remain. Whichever process starts first after a
deploy does the work; the other blocks briefly and finds nothing to do. That is
what makes "the Worker booted before the API" safe by construction rather than
by deployment ordering ([ADR-0055](../../../../docs/decisions/ADR-0055-ef-core-migrations-sql-files-stored-procedures.md)).

## Adding a migration

See [`skills/manage-hpac-migrations`](../../../../skills/manage-hpac-migrations/SKILL.md)
for the rules a migration has to satisfy before it is reviewable. The command:

```sh
dotnet ef migrations add <Name> \
  --project src/HpacSafety.Infrastructure \
  --startup-project src/HpacSafety.Infrastructure \
  --output-dir Persistence/Migrations
```

`HpacSafety.Api` is not usable as the startup project — it does not reference
`Microsoft.EntityFrameworkCore.Design`. `HpacSafety.Infrastructure` supplies
[`HpacSafetyDbContextFactory`](../HpacSafetyDbContextFactory.cs) for exactly
this.

## The migrations, in order

| Migration                                              | What it did                                                                                                                                                                                                                                                                                                                                                                                                                                       |
|--------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `20260823001528_InitialSchema`                         | The first schema: questions, reports, answers, files, summaries, admin users, audit log, outbox.                                                                                                                                                                                                                                                                                                                                                  |
| `20260823022839_ReplaceSensitivityWithQuestionPrivacy` | Replaced a three-tier sensitivity field with the private/eligible split the model actually needs (ADR-0038).                                                                                                                                                                                                                                                                                                                                      |
| `20260827013637_MigrateCanonicalDomainAndPersistence`  | Moved to complete immutable revisions, removed the application-side field cipher, and reached the current baseline (ADR-0040).                                                                                                                                                                                                                                                                                                                    |
| `20260921021720_AddQuestionAuthoring`                  | Added `option_sets`/`option_set_items`, the conditional-question and option-set provenance columns, and the `time` and `autocomplete` question types.                                                                                                                                                                                                                                                                                             |
| `20260921034154_AddReporterAddedChoices`               | Added `option_set_items.added_by_reporter` and an index on `(option_set_id, added_by_reporter)`, which is the curation query.                                                                                                                                                                                                                                                                                                                     |
| `20260921152356_DropAdminUsersForJwtIdentity`          | Dropped `admin_users` and renamed/widened its two referencing columns to opaque token subjects — `audit_log.actor_subject` and `summaries.approved_by_subject` (ADR-0065).                                                                                                                                                                                                                                                                        |
| `20260921192412_ForkAnsweredQuestionsAndStringAnswers` | Narrowed the unique index on `questions.key` to live rows so a fork chain can share one (ADR-0071). Replaced `report_answers.selected_option_codes` with `locale`, `translated_value`, and `needs_translation` alongside the existing `value`, dropped the uniqueness of `(report_id, question_id)` so a multi-select records one row per chosen value, indexed the translation queue, and added `option_set_items.needs_translation` (ADR-0072). |
| `20260921205551_RemoveStatementGroupSectionKey`        | Dropped `question_revisions.section_key` after removing the `statement` and `group` question types it existed to support — neither had a built renderer, and nothing distinguished them from each other in code.                                                                                                                                                                                                                                  |
| `20260921224859_AddDependsOnOptionCode`                | Added `question_revisions.depends_on_option_code`, the required option a `single_select` parent must be answered with (ADR-0074). Null for a `yes_no` parent, whose condition stays the invariant "yes".                                                                                                                                                                                                                                          |
| `20260922222239_RecordReporterChoiceLocale`           | Added nullable `option_set_items.reporter_locale`, the language a reporter typed a type-ahead choice in (ADR-0063). Null for every choice an administrator authored, which is every row that already existed. |
| `20260923010810_GiveEachQuestionItsOwnChoices`        | Added `question_choices`, copied every question's current choices onto it (a type-ahead backed by a live shared list takes that list's items, reporter marks and removals kept; a reporter item awaiting its other language keeps only the language typed), then dropped `option_sets`, `option_set_items`, `question_revision_options`, `question_revisions.option_set_id`, and `question_revisions.allows_reporter_additions` (ADR-0095). The copy is `Sql/20260923010810_CopyChoicesOntoQuestions.sql`, the first migration SQL kept in its own file (ADR-0055). |
| `20260923205108_AddReportFileOriginalFileName`        | Added nullable `report_files.original_file_name`, the reporter's sanitized filename, used only as a reviewer's download name (ADR-0097). Null for every file that already existed, which keeps its server-minted download name. |
| `20260923224129_WordAttachmentQuestionForSeveralFiles` | No schema change. Rewords the seeded attachment question for several files, only where it still reads exactly as seeded: an unanswered question gets a new revision, an answered one forks (ADR-0071). An Administrator's own wording is left alone. |
| `20260924143055_TranslateOnlyAnswersThatNeedIt`       | Added `question_revisions.is_translatable` (true for existing long-text revisions, false otherwise, and checked to be false for anything but short or long text) and `report_answers.translation_mode` (`none`, `choice`, or `machine`; existing long-text, select, and type-ahead answers backfilled `machine`, everything else `none`). The awaiting-translation index now covers only `machine` answers (ADR-0112). |
| `20260924161944_CreatePublicReportsView`              | No table change. Creates the `public_reports` view (`Sql/20260924161944_CreatePublicReportsView.sql`). It is the whole publication invariant in SQL, and its columns are the public DTO's allowlist (#28, CON-DP-011). The public feed and detail read only this view. |
| `20260924170847_AddReportComments`                    | Added `report_comments` (author and hiding reviewer as opaque token subjects, `hidden_at`, `deleted`) and `report_comment_revisions` (text, locale, machine translation and its source, one row per edit, unique per comment and number), and `translate_comment` as an outbox type. Replaces `public_reports` to add `comment_count` and creates `public_report_comments`, each visible comment's current revision on a public report (`Sql/20260924170847_AddReportComments.sql`, ADR-0114). |
| `20260925212255_KeepSystemQuestionsPrivate`           | No schema change. Gives each live system question whose current revision is not private a new, private revision (`Sql/20260925212255_KeepSystemQuestionsPrivate.sql`). The seeded publication consent was not private; the domain now refuses that (#450). |
| `20260925213420_StoreYesOrNoInTheReportersLanguage` | Recreated `ck_report_answers_translation_mode` to allow `fixed`: a yes/no or checkbox answer's counterpart (`yes`↔`oui`, `no`↔`non`), written at submission (ADR-0127). Existing answers are unchanged. |
| `20260925223046_StoreYesOrNoAsABoolean` | Added `report_answers.value_boolean`, and converted every stored yes/no and checkbox answer to it once: `yes`/`oui` → `true`, `no`/`non` → `false`, clearing `value`, `translated_value`, and `translation_source`, with mode `none`. Any other stored value stops the migration (ADR-0130). Dropped `fixed` from `ck_report_answers_translation_mode`, and added `ck_report_answers_text_or_boolean` and `ck_report_answers_boolean_has_no_words`. |
| `20260925230357_NameEachAnswersChoice` | Added `report_answers.choice_id` (a restricted foreign key to `question_choices`) and linked every existing select and type-ahead answer to the choice its stored label names; a label no choice carries any more gets a removed choice holding it, in the answer's language, so every old answer resolves. No answer's text is rewritten. `ck_question_choices_label` now also lets such a removed choice hold one language, and the translation queue (its index and `answers_awaiting_translation`) leaves choice answers out (ADR-0128). The backfill is `Sql/20260925230357_NameEachAnswersChoice.sql`. |
| `20260925233040_TranslateReporterAddedValues` | Added `question_choices.label_en_source` and `label_fr_source` (`human` or `auto`, present exactly when their label is; every existing label backfilled `human`), and allowed the outbox type `translate_choice`: the Worker supplies a reporter-added type-ahead value's missing language on the value itself (ADR-0129). The backfill is `Sql/20260925233040_TranslateReporterAddedValues.sql`. |
| `20260925235946_ReviewTypeAheadValues` | Added `question_choices.needs_review`, `reviewed_at`, `reviewed_by`, and `created_at`, and flagged every live reporter-added value still missing a language (what "awaiting review" meant before). `admin_pending_counts` gains `type_ahead_values_awaiting_review`, counted on live questions; its Down script drops and recreates the view without it (ADR-0129). |
| `20260926001923_ReplacePickerOptionsAndNameConditionsByChoice` | Added `question_choices.replaced_by_choice_id` (a replaced picker option names its replacement) and `question_revisions.depends_on_choice_id`, backfilled from `depends_on_option_code` by the parent's choice with that code; the script stops the migration if any code resolves to no choice, and only then is `depends_on_option_code` dropped — nothing it held is lost. Both new columns are restricted foreign keys to `question_choices` (ADR-0128). The backfill is `Sql/20260926001923_ReplacePickerOptionsAndNameConditionsByChoice.sql`; `Down` puts the codes back first. |
| `20260926004803_MergeTypeAheadValues` | Added `question_choices.merged_into_choice_id`, a restricted self-reference, with `ck_question_choices_merged_is_removed`: a merged value is removed and never names itself. Answers are not touched; they read the value merged into (ADR-0129). |

Past migrations are history and are never edited — including the raw SQL
already inlined in them. New raw SQL goes in its own `.sql` file under
[`../Sql/`](../Sql), not as a C# string literal.
