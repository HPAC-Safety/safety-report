---
name: manage-hpac-migrations
description: HPAC Safety's migration conventions, paths, and commands — extends the generic manage-ef-core-migrations skill. Use when adding a table, column, index, constraint, or seed, or when changing how a migration is applied in this repository.
---

# Manage an HPAC Safety migration

Extends the `manage-ef-core-migrations` skill;
read that first. Its schema conventions (keys, types, enums, deletion) also
extend the `postgres-dba` skill and win over it. This skill
holds only what is specific to this repository, under the same section names.

- Read
  [`Persistence/Migrations/README.md`](../../src/HpacSafety.Infrastructure/Persistence/Migrations/README.md)
  first: the database, its four conventions, and the schema diagram.
- The target schema: the data-and-persistence constraint page. A
  disposable database: the `postgres` service in `docker-compose.yml`, or the
  PostgreSQL container the API and Infrastructure test suites start.
- Tables hold personal and medical information, so an audit query returns
  counts and shapes, never row content.
- This pair is the one home for schema-change procedure and rules. The wider
  persistence contract is [`persist-hpac-data`](../persist-hpac-data/SKILL.md).

## Rules

The generic rules hold (the migrations, SQL files, and stored procedures
decision), plus:

1. **Past migrations keep their inline SQL.** The raw SQL inlined in
   `20260823001528_InitialSchema.cs` and
   `20260827013637_MigrateCanonicalDomainAndPersistence.cs` stays.
2. **Nothing is physically deleted.** No `DELETE`, no `DROP TABLE` on a table
   holding application data, no `ALTER` that loses a value (the canonical-domain migration decision).
   - Retirement is a `deleted timestamptz` stamp. Every new table gets that
     column and the default live-row filter — except `question_choices` and
     `question_choice_parents`, which skip the filter because their aggregate
     reads removed rows (the question-owns-its-choices and shared-dependent-choice decisions).
   - A widening cast that loses nothing (`char(11)` → `varchar(256)`) is not
     destructive.
   - The carved exceptions (`AGENTS.md` invariant 8), each argued in its own
     decision:
     - the legacy per-language question tables and `report_aircraft`, dropped
       by `MigrateCanonicalDomainAndPersistence` after folding their data
       forward
       (the canonical-domain migration decision);
     - `admin_users`, which never held data in a deployed environment
       (the no-user-records decision);
     - the shared-choice-list and per-revision option tables, dropped only
       after the same migration copies every choice onto its question
       (the question-owns-its-choices decision);
     - `pending_import_logic` rows, transient Typeform import notes with no
       `deleted` column, hard-deleted when an administrator resolves them
       (the Typeform import decision).
     None generalizes; any other physical delete needs its own decision record.
3. **Every key is a tiny id**, `char(11)`
   (the tiny-ids decision). Never `uuid` or
   `bigint identity`. Type the key as `TinyId`; `ConfigureConventions` handles
   the conversion.
4. **Every enum column is a `varchar` of invariant codes with a `CHECK`
   constraint.** Adding a member drops and recreates the constraint; EF
   generates it once the constraint string in the entity configuration is
   updated.
5. **No `DateTime`** — `DateOnly`, `TimeOnly`, `DateTimeOffset` (the banned-`DateTime` decision).
   `tests/BannedSymbols.txt` enforces it in the build.
6. **Names are `snake_case`**, applied automatically. Hand-name a column only
   when it must differ.
7. **Seed identifiers come from `SeedIds`**
   (the seeding-by-migration decision). Never
   real report content.
8. **New raw SQL is a `.sql` file** under
   `src/HpacSafety.Infrastructure/Persistence/Sql/`, loaded by the migration,
   never a C# string literal. Views and stored procedures live there too.
   - A read query that applies a rule (a filter, a derived flag, a count)
     becomes a view, mapped read-only under `Persistence/Views/` with
     `ToView` (the decision that a read rule lives in a view). A pure projection stays LINQ.
   - Existing inline SQL in past migrations is not rewritten.
9. **Four tables refuse changes to locked columns, and every `DELETE`.**
   `reports`, `report_answers`, `report_files`, and `summary_revisions` carry
   `BEFORE UPDATE OR DELETE` triggers (the database-refuses-changes decision). A migration that must change a
   locked column:
   - disables the table's trigger inside its own transaction
     (`ALTER TABLE … DISABLE TRIGGER <table>_immutable`), makes the one change,
     and enables it again before the transaction ends;
   - carries its own decision record arguing for why a reporter's account or a saved
     revision may change. There is no session setting, role, or runtime flag
     that bypasses a trigger, and none may be added;
   - a new column on one of these tables is unguarded until it is added to that
     table's function and trigger column list, so decide locked or writable in
     the migration that adds it.

## Generate

1. The model: the entity in `HpacSafety.Core`, its configuration in
   `Persistence/Configurations/`, and its `DbSet` and `ApplyConfiguration`
   call in `HpacSafetyDbContext`.
2. Command:

   ```sh
   dotnet ef migrations add <DescriptiveName> \
     --project src/HpacSafety.Infrastructure \
     --startup-project src/HpacSafety.Infrastructure \
     --output-dir Persistence/Migrations
   ```

   - `HpacSafety.Api` cannot be the startup project; it lacks
     `Microsoft.EntityFrameworkCore.Design`. `HpacSafetyDbContextFactory` in
     Infrastructure covers this.
   - Missing assets file? `dotnet restore HpacSafety.slnx` first.

## Review what it generated

- The data never dropped is application data.
- The snapshot is `HpacSafetyDbContextModelSnapshot.cs`.
- No `DateTime`, no `uuid`, and no raw SQL that belongs in a `.sql` file.

## How it is applied

- `HpacSafety.Api` and `HpacSafety.Worker` both call `EnsureMigrated` (the
  `MigrationRunner` extension on `HpacSafetyDbContext`) at startup: take a
  PostgreSQL advisory lock, re-check pending migrations after acquiring it,
  apply any that remain. Whichever starts first does the work, so "Worker
  before API" after a deploy is safe.
- There is no `migrate` deploy job. Adding one reintroduces the ordering
  dependency this replaced (the migrations, SQL files, and stored procedures
  decision).

## Squash to one baseline

Not yet sanctioned here. Before squashing:

- A decision record must supersede the generic rule 2 for this repository and
  say what happens to rule 1 above and to the migrations that `AGENTS.md`
  invariant 8 and the canonical-domain, no-user-records, and
  question-owns-its-choices decisions cite as carved exceptions — the
  baseline never creates those tables, so it drops nothing.
- Every deployed environment's database is recreated, or proven identical and
  given the baseline's history row.

Carry forward, in their final form:

- the `.sql` files under `Persistence/Sql/` that define views, functions, and
  the immutability triggers of rule 9 (not the ones that transformed rows),
  merged into one set loaded by the baseline;
- the seed from `Persistence/Seeding/` (`QuestionBankSeed`, `SeedIds`). Not
  `DevelopmentAdminSeed`: it seeds `admin_users`, which the baseline never
  creates;
- the `CHECK` constraints and the default soft-delete filters, which the model
  regenerates — verify them in the schema diff.

Then replace the migration table in `Persistence/Migrations/README.md` with
the one baseline.

## Before the pull request

- Run `dotnet test HpacSafety.slnx --filter "Category!=ui"`. API and
  Infrastructure suites boot a real PostgreSQL container.
- The diagram is a Mermaid `erDiagram` with `snake_case` names (the Mermaid-for-diagrams decision).
- Update the migration table at the bottom of `Persistence/Migrations/README.md`,
  and its schema diagram when the shape changed.
- Update the data-and-persistence constraint page when the logical record changed.
