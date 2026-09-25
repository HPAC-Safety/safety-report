---
name: manage-hpac-migrations
description: Write, review, and apply an HPAC Safety EF Core migration. Use when adding a table, column, index, constraint, or seed, or when changing how a migration is applied.
---

# Manage an HPAC Safety migration

- Read
  [`Persistence/Migrations/README.md`](../../src/HpacSafety.Infrastructure/Persistence/Migrations/README.md)
  first: the database, its four conventions, and the schema diagram.
- This skill is the one home for schema-change procedure and rules. The wider
  persistence contract is [`persist-hpac-data`](../persist-hpac-data/SKILL.md).

## Rules

1. **A migration is the only way the schema changes.** No hand-written DDL, no
   hand-applied `psql` fix, no second `DbContext`
   ([ADR-0055](../../docs/decisions/ADR-0055-ef-core-migrations-sql-files-stored-procedures.md)).
2. **A past migration is never edited.** Fix a mistake with a new one. Do not
   retrofit new conventions onto old files — the raw SQL inlined in
   `20260823001528_InitialSchema.cs` and
   `20260827013637_MigrateCanonicalDomainAndPersistence.cs` stays.
3. **Nothing is physically deleted.** No `DELETE`, no `DROP TABLE` on a table
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
4. **Both paths work**: an empty database and one at current `main`. A new
   column is nullable, defaulted, or backfilled by the same migration — never
   `NOT NULL` with no value for existing rows.
5. **Every key is a tiny id**, `char(11)`
   ([ADR-0034](../../docs/decisions/ADR-0034-tiny-ids.md)). Never `uuid` or
   `bigint identity`. Type the key as `TinyId`; `ConfigureConventions` handles
   the conversion.
6. **Every enum column is a `varchar` of invariant codes with a `CHECK`
   constraint.** Adding a member drops and recreates the constraint; EF
   generates it once the constraint string in the entity configuration is
   updated.
7. **No `DateTime`** — `DateOnly`, `TimeOnly`, `DateTimeOffset`
   ([ADR-0035](../../docs/decisions/ADR-0035-dateonly-datetimeoffset-timeonly-datetime-is-banned.md)).
   `tests/BannedSymbols.txt` enforces it in the build.
8. **Names are `snake_case`**, applied automatically. Hand-name a column only
   when it must differ.
9. **Seed data is a migration**: idempotent, identifiers from `SeedIds` so a
   re-run cannot duplicate it
   ([ADR-0020](../../docs/decisions/ADR-0020-seeding-by-migration.md)).
   Synthetic only; never real report content or personal data.
10. **New raw SQL is a `.sql` file** under
    `src/HpacSafety.Infrastructure/Persistence/Sql/`, loaded by the migration,
    never a C# string literal. Views and stored procedures live there too.
    - A read query that applies a rule (a filter, a derived flag, a count)
      becomes a view, mapped read-only under `Persistence/Views/` with
      `ToView` (ADR-0116). A pure projection stays LINQ.
    - Existing inline SQL in past migrations is not rewritten.

## Generate

1. Change the model first: the entity in `HpacSafety.Core`, its
   `IEntityTypeConfiguration` in `Persistence/Configurations/`, and its
   `DbSet` and `ApplyConfiguration` call in `HpacSafetyDbContext`. Never write
   the migration by hand.
2. Generate:

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
   - Undo an uncommitted migration: `dotnet ef migrations remove` with the
     same two project arguments.

## Review what it generated

Never commit a migration unread. Check it:

- adds and never drops application data;
- makes new columns nullable or defaulted;
- gives every foreign key a chosen delete behavior — `Restrict` when a child
  outlives its parent's retirement, `SetNull` for a provenance pointer,
  `Cascade` only for rows genuinely part of the parent;
- leaves `HpacSafetyDbContextModelSnapshot.cs` consistent, committed in the
  same change;
- has no `DateTime`, no `uuid`, and no raw SQL that belongs in a `.sql` file.

## How it is applied

- Nobody applies it by hand. `HpacSafety.Api` and `HpacSafety.Worker` both call
  `EnsureMigrated` (the `MigrationRunner` extension on `HpacSafetyDbContext`)
  at startup: take a PostgreSQL
  advisory lock, re-check pending migrations after acquiring it, apply any
  that remain. Whichever starts first does the work, so "Worker before API"
  after a deploy is safe.
- There is no `migrate` deploy job. Adding one reintroduces the ordering
  dependency this replaced
  ([ADR-0055](../../docs/decisions/ADR-0055-ef-core-migrations-sql-files-stored-procedures.md)).

## Before the pull request

- Run `dotnet test HpacSafety.slnx --filter "Category!=ui"`. API and
  Infrastructure suites boot a real PostgreSQL container, so a migration that
  cannot apply to an empty database fails there.
- **Mermaid `erDiagram` in the PR body** for any change that adds, removes, or
  restructures a table, column, relationship, or constraint
  ([ADR-0046](../../docs/decisions/ADR-0046-mermaid-for-diagrams.md)):
  - the resulting shape of the **affected** tables only, not the whole
    database;
  - `snake_case` names, primary and foreign keys, cardinality;
  - omit columns that do not help a reviewer see the change;
  - put it in the ADR too when the change warrants one.
- Update the migration table at the bottom of `Persistence/Migrations/README.md`,
  and its schema diagram when the shape changed.
- Update [`docs/data-and-persistence.md`](../../docs/data-and-persistence.md)
  when the logical record changed. Add an ADR for a rejected alternative or a
  durable trade-off.
