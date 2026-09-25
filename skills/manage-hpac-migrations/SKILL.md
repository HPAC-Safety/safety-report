---
name: manage-hpac-migrations
description: HPAC Safety's migration conventions, paths, and commands — extends the generic manage-ef-core-migrations skill. Use when adding a table, column, index, constraint, or seed, or when changing how a migration is applied in this repository.
---

# Manage an HPAC Safety migration

Extends [`manage-ef-core-migrations`](../manage-ef-core-migrations/SKILL.md);
read that first. Its schema conventions (keys, types, enums, deletion) also
extend [`postgres-dba`](../postgres-dba/SKILL.md) and win over it. This skill
holds only what is specific to this repository, under the same section names.

- Read
  [`Persistence/Migrations/README.md`](../../src/HpacSafety.Infrastructure/Persistence/Migrations/README.md)
  first: the database, its four conventions, and the schema diagram.
- This pair is the one home for schema-change procedure and rules. The wider
  persistence contract is [`persist-hpac-data`](../persist-hpac-data/SKILL.md).

## Rules

The generic rules hold
([ADR-0055](../../docs/decisions/ADR-0055-ef-core-migrations-sql-files-stored-procedures.md)),
plus:

1. **Past migrations keep their inline SQL.** The raw SQL inlined in
   `20260823001528_InitialSchema.cs` and
   `20260827013637_MigrateCanonicalDomainAndPersistence.cs` stays.
2. **Nothing is physically deleted.** No `DELETE`, no `DROP TABLE` on a table
   holding application data, no `ALTER` that loses a value
   ([ADR-0040](../../docs/decisions/ADR-0040-migrate-canonical-domain-and-persistence.md)).
   - Retirement is a `deleted timestamptz` stamp. Every new table gets that
     column and the default live-row filter — except `question_choices`, which
     skips the filter because its aggregate reads removed rows (ADR-0095).
   - A widening cast that loses nothing (`char(11)` → `varchar(256)`) is not
     destructive.
   - The carved exceptions (`AGENTS.md` invariant 8), each argued in its own
     ADR:
     - the legacy per-language question tables and `report_aircraft`, dropped
       by `MigrateCanonicalDomainAndPersistence` after folding their data
       forward
       ([ADR-0040](../../docs/decisions/ADR-0040-migrate-canonical-domain-and-persistence.md));
     - `admin_users`, which never held data in a deployed environment
       ([ADR-0065](../../docs/decisions/ADR-0065-no-user-records-identity-is-the-token-subject.md));
     - the shared-choice-list and per-revision option tables, dropped only
       after the same migration copies every choice onto its question
       ([ADR-0095](../../docs/decisions/ADR-0095-a-question-owns-its-choices-outside-its-revisions.md));
     - `pending_import_logic` rows, transient Typeform import notes with no
       `deleted` column, hard-deleted when an administrator resolves them
       ([ADR-0077](../../docs/decisions/ADR-0077-typeform-json-import-and-export.md)).
     None generalizes; any other physical delete needs its own ADR.
3. **Every key is a tiny id**, `char(11)`
   ([ADR-0034](../../docs/decisions/ADR-0034-tiny-ids.md)). Never `uuid` or
   `bigint identity`. Type the key as `TinyId`; `ConfigureConventions` handles
   the conversion.
4. **Every enum column is a `varchar` of invariant codes with a `CHECK`
   constraint.** Adding a member drops and recreates the constraint; EF
   generates it once the constraint string in the entity configuration is
   updated.
5. **No `DateTime`** — `DateOnly`, `TimeOnly`, `DateTimeOffset`
   ([ADR-0035](../../docs/decisions/ADR-0035-dateonly-datetimeoffset-timeonly-datetime-is-banned.md)).
   `tests/BannedSymbols.txt` enforces it in the build.
6. **Names are `snake_case`**, applied automatically. Hand-name a column only
   when it must differ.
7. **Seed identifiers come from `SeedIds`**
   ([ADR-0020](../../docs/decisions/ADR-0020-seeding-by-migration.md)). Never
   real report content.
8. **New raw SQL is a `.sql` file** under
   `src/HpacSafety.Infrastructure/Persistence/Sql/`, loaded by the migration,
   never a C# string literal. Views and stored procedures live there too.
   - A read query that applies a rule (a filter, a derived flag, a count)
     becomes a view, mapped read-only under `Persistence/Views/` with
     `ToView` (ADR-0116). A pure projection stays LINQ.
   - Existing inline SQL in past migrations is not rewritten.

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
  dependency this replaced
  ([ADR-0055](../../docs/decisions/ADR-0055-ef-core-migrations-sql-files-stored-procedures.md)).

## Squash to one baseline

Not yet sanctioned here. Before squashing:

- An ADR must supersede the generic rule 2 for this repository and say what
  happens to rule 1 above and to the migrations that `AGENTS.md` invariant 8
  and ADR-0040, ADR-0065, and ADR-0095 cite as carved exceptions — the
  baseline never creates those tables, so it drops nothing.
- Every deployed environment's database is recreated, or proven identical and
  given the baseline's history row.

Carry forward, in their final form:

- the `.sql` files under `Persistence/Sql/` that define views and functions
  (not the ones that transformed rows), merged into one set loaded by the
  baseline;
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
- The diagram is a Mermaid `erDiagram` with `snake_case` names
  ([ADR-0046](../../docs/decisions/ADR-0046-mermaid-for-diagrams.md)).
- Update the migration table at the bottom of `Persistence/Migrations/README.md`,
  and its schema diagram when the shape changed.
- Update [`docs/data-and-persistence.md`](../../docs/data-and-persistence.md)
  when the logical record changed.
