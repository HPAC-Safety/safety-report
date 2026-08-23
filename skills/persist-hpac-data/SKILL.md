---
name: persist-hpac-data
description: Apply HPAC persistence, EF Core, migration, encryption, immutable question-privacy, identifier, time, and seed-data rules. Use when changing entities, tables, mappings, migrations, value converters, TinyId keys, occurrence dates or times, encrypted fields, domain-code storage, or the seeded question bank.
---

# Preserve the database contract

Read `src/HpacSafety.Infrastructure/Persistence/README.md` and the relevant ADRs
before editing persistence. `HpacSafety.Infrastructure/Persistence` owns every
table and migration; `HpacSafety.Core` never references EF Core.

## Model storage explicitly

- Encrypt every report answer value in the application before PostgreSQL sees
  it, through `IFieldCipher` and a value converter. `IsPrivate` controls model
  input partitioning, not at-rest protection. Never create a clear-text helper
  column.
- Store `questions.is_private` as a required boolean that defaults to `true` in
  creation paths and has no update path. Copy it to
  `report_answers.is_private` when recording the answer. A privacy change is a
  new question identity, never an update or bulk reclassification.
- Identify every row with an 11-character `TinyId` over `A-Za-z0-9-_`, stored
  as `char(11)`. Never introduce UUID, sequential, or time-bearing keys, and
  never authorize access by possession of an identifier.
- Store domain values as invariant string codes, never ordinals. Throw on an
  unknown stored code instead of defaulting.
- Represent the occurrence as local `DateOnly` plus local `TimeOnly`. Derive
  `TimeOfDay` only through `TimeOfDay.FromLocalTime`; missing time becomes
  `Unknown`, never midnight. Encrypt the precise time and keep only the bucket
  publishable.

## Seed safely

- Write administrator-editable seed data in migrations, never `HasData`.
- Derive seed identifiers deterministically from keys.
- Never put a real name, address, or allowlist in a migration. Guard the single
  `admin@localhost` development row inside PostgreSQL SQL with
  `hpac.seed_development_admin`.
- Reproduce generated `docs/form-spec.md` exactly in the seeded question bank,
  assign an explicit privacy value to every seeded question, and keep the tests
  that prove both. Regenerate the spec with
  `tools/extract-typeform.py`; do not edit it by hand.

Keep scaffolded-migration analyzer exemptions scoped to `**/Migrations/*.cs`.
Put executable seed logic under `Persistence/Seeding`, where normal analysis and
coverage still apply.
