---
name: manage-hpac-migrations
description: Write, review, and apply an HPAC Safety EF Core migration. Use when adding a table, column, index, constraint, or seed, or when changing how a migration is applied.
---

# Manage an HPAC Safety migration

Read
[`src/HpacSafety.Infrastructure/Persistence/Migrations/README.md`](../../src/HpacSafety.Infrastructure/Persistence/Migrations/README.md)
first — it describes the database, its four conventions, and the schema
diagram. This skill is the procedure and the rules a migration has to satisfy.
[`persist-hpac-data`](../persist-hpac-data/SKILL.md) covers the wider
persistence contract.

## The rules, before you generate anything

1. **A migration is the only way the schema changes.** No hand-written DDL, no
   `psql` fix applied by hand, no second `DbContext`
   ([ADR-0055](../../docs/decisions/ADR-0055-ef-core-migrations-sql-files-stored-procedures.md)).
2. **A past migration is history and is never edited.** Correct a mistake with
   a new migration. This includes not retrofitting a new convention onto old
   files — the raw SQL already inlined in `20260823001528_InitialSchema.cs`
   and `20260827013637_MigrateCanonicalDomainAndPersistence.cs` stays where it
   is.
3. **Nothing is physically deleted.** No `DELETE`, no `DROP TABLE` on a table
   holding application data, no destructive `ALTER` that loses a value.
   Retirement is a `deleted timestamptz` stamp, and every new table gets that
   column plus the default live-row filter (`question_choices` alone skips the
   filter, because its aggregate reads its removed rows — ADR-0095)
   ([ADR-0040](../../docs/decisions/ADR-0040-migrate-canonical-domain-and-persistence.md)).
   **Two carved exceptions exist**: `admin_users` is dropped by
   [ADR-0065](../../docs/decisions/ADR-0065-no-user-records-identity-is-the-token-subject.md),
   on the specific ground that it never held application data in any deployed
   environment; and the shared-choice-list and per-revision option tables are
   dropped by
   [ADR-0095](../../docs/decisions/ADR-0095-a-question-owns-its-choices-outside-its-revisions.md)
   only after the same migration copies every choice onto its question.
   Neither generalizes. A widening cast that loses no value —
   `char(11)` to `varchar(256)` — is not destructive and needs no exception.
   Any other physical delete needs its own ADR arguing its own facts.
4. **Both paths have to work**: a fresh, empty database and a database sitting
   at current `main`. A new column is nullable, or has a default, or is
   backfilled by the same migration — never `NOT NULL` with no answer for the
   rows already there.
5. **Every key is a tiny id** — `char(11)`, fixed length
   ([ADR-0034](../../docs/decisions/ADR-0034-tiny-ids.md)). Never `uuid`,
   never `bigint identity`. The conversion is already configured centrally in
   `ConfigureConventions`; a new entity gets it for free by typing its key as
   `TinyId`.
6. **Every enum column is a `varchar` of invariant codes with a `CHECK`
   constraint** listing them. Adding an enum member means dropping and
   recreating that constraint in the migration — EF generates this once the
   constraint string in the entity configuration is updated.
7. **`DateOnly` / `TimeOnly` / `DateTimeOffset`, never `DateTime`**
   ([ADR-0035](../../docs/decisions/ADR-0035-dateonly-datetimeoffset-timeonly-datetime-is-banned.md)).
   The build enforces this through `tests/BannedSymbols.txt`.
8. **Names are `snake_case`**, applied automatically. Do not hand-name a
   column unless it needs to differ.
9. **Seed data is a migration too**, idempotent, with identifiers derived
   deterministically by `SeedIds` so a re-run cannot duplicate it
   ([ADR-0020](../../docs/decisions/ADR-0020-seeding-by-migration.md)). Never
   seed real report content or personal data — synthetic only.
10. **New raw SQL lives in its own `.sql` file** under
    `src/HpacSafety.Infrastructure/Persistence/Sql/`, loaded by the migration,
    not typed as a C# string literal. A view or stored procedure belongs there
    too; a query that reads naturally as LINQ stays LINQ.

## Generating it

```sh
dotnet ef migrations add <DescriptiveName> \
  --project src/HpacSafety.Infrastructure \
  --startup-project src/HpacSafety.Infrastructure \
  --output-dir Persistence/Migrations
```

`HpacSafety.Api` cannot be the startup project — it does not reference
`Microsoft.EntityFrameworkCore.Design`. `HpacSafety.Infrastructure` provides
`HpacSafetyDbContextFactory` for this. If the command reports a missing assets
file, run `dotnet restore HpacSafety.slnx` first.

Model changes come first: the entity in `HpacSafety.Core`, its
`IEntityTypeConfiguration` in `Persistence/Configurations/`, and its `DbSet`
and `ApplyConfiguration` call in `HpacSafetyDbContext`. The migration is
generated from those, never written by hand.

To undo a generated migration that has not been committed:
`dotnet ef migrations remove` with the same two project arguments.

## Then read what it generated

Never commit a generated migration unread. Check that it:

- adds and never drops application data;
- makes new columns nullable or defaulted;
- names foreign keys with a delete behaviour that was chosen, not defaulted —
  `Restrict` where a child must outlive its parent's retirement, `SetNull` for
  a provenance pointer, `Cascade` only for rows that are genuinely part of the
  parent;
- leaves `HpacSafetyDbContextModelSnapshot.cs` consistent (it is regenerated
  with the migration; commit it in the same change);
- contains no `DateTime`, no `uuid`, and no raw SQL that should have been a
  `.sql` file.

## How it gets applied

Nobody applies it. Both `HpacSafety.Api` and `HpacSafety.Worker` call
`HpacSafetyDbContext.EnsureMigratedAsync` at startup: it takes a PostgreSQL
advisory lock, re-checks for pending migrations *after* acquiring it, and
applies them only if any remain. Whichever process starts first after a deploy
does the work and the other finds nothing to do. There is no `migrate` deploy
job, and adding one would reintroduce the ordering dependency this replaced
([ADR-0055](../../docs/decisions/ADR-0055-ef-core-migrations-sql-files-stored-procedures.md)).

## Before opening the pull request

- Run `dotnet test HpacSafety.slnx --filter "Category!=ui"`. The API and
  Infrastructure suites boot against a real PostgreSQL container through
  Testcontainers, so a migration that cannot apply to an empty database fails
  there.
- Add a Mermaid `erDiagram` of the **affected** tables to the pull-request
  body — not the whole database. Show `snake_case` names, keys, and
  cardinality, and leave out columns that do not help a reviewer see what
  changed ([ADR-0046](../../docs/decisions/ADR-0046-mermaid-for-diagrams.md)).
- Update the migration table at the bottom of
  `Persistence/Migrations/README.md`, and its schema diagram when the shape
  changed.
- Update [`docs/data-and-persistence.md`](../../docs/data-and-persistence.md)
  when the logical record changed, and add an ADR when the change involved a
  rejected alternative or a durable trade-off.
