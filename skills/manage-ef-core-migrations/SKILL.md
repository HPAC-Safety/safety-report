---
name: manage-ef-core-migrations
description: Write, review, and apply an EF Core migration safely — model first, generated not hand-written, never editing a past migration, working on empty and current databases. Use when adding a table, column, index, constraint, or seed, or when changing how a migration is applied.
---

# Manage an EF Core migration

**Project rules.** A project may extend this skill with a companion skill that
names this one; its agent instructions (`AGENTS.md`) list it. Read both. The
companion holds the project's schema conventions, paths, and commands, and wins
where they differ.

## Rules

1. **A migration is the only way the schema changes.** No hand-written DDL, no
   hand-applied `psql` fix, no second `DbContext`.
2. **A past migration is never edited.** Fix a mistake with a new one. Do not
   retrofit new conventions onto old files.
3. **Both paths work**: an empty database and one at current `main`. A new
   column is nullable, defaulted, or backfilled by the same migration — never
   `NOT NULL` with no value for existing rows.
4. **Seed data is a migration**: idempotent, with fixed identifiers so a re-run
   cannot duplicate it. Synthetic only; never real user content or personal
   data.

## Generate

1. Change the model first: the entity, its `IEntityTypeConfiguration`, and its
   `DbSet` and `ApplyConfiguration` call in the `DbContext`. Never write the
   migration by hand.
2. Generate with `dotnet ef migrations add <DescriptiveName>`, passing the
   project and startup project that hold the `DbContext` and a design-time
   factory.
   - Undo an uncommitted migration: `dotnet ef migrations remove` with the
     same project arguments.

## Review what it generated

Never commit a migration unread. Check it:

- adds and never drops data the project keeps;
- makes new columns nullable or defaulted;
- gives every foreign key a chosen delete behavior — `Restrict` when a child
  outlives its parent's retirement, `SetNull` for a provenance pointer,
  `Cascade` only for rows genuinely part of the parent;
- leaves the model snapshot consistent, committed in the same change;
- follows the project's type and raw-SQL conventions.

## How it is applied

- Nobody applies it by hand. The project documents the one mechanism that
  applies migrations, and a change never adds a second.

## Before the pull request

- Run the tests that boot a real database, so a migration that cannot apply to
  an empty database fails there.
- **An entity-relationship diagram in the PR body** for any change that adds,
  removes, or restructures a table, column, relationship, or constraint:
  - the resulting shape of the **affected** tables only, not the whole
    database;
  - database names, primary and foreign keys, cardinality;
  - omit columns that do not help a reviewer see the change;
  - put it in the ADR too when the change warrants one.
- Update the project's migration list and schema documentation when the shape
  or the logical record changed. Add an ADR for a rejected alternative or a
  durable trade-off.
