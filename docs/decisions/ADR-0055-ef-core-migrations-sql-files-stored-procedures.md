---
status: accepted
date: 2026-09-19
decision-makers: Chase Florell
keywords: EF Core, migrations, stored procedures, views, raw SQL, idempotent migration
---

# ADR-0055 — EF Core is the only path to schema change; SQL lives in files; the app applies its own migrations

## Context

`HpacSafetyDbContext` (`src/HpacSafety.Infrastructure/Persistence/HpacSafetyDbContext.cs`)
is already the one database context, and every migration to date
(`src/HpacSafety.Infrastructure/Persistence/Migrations/*.cs`) is generated
through EF Core. That much is established practice, not new. Two things are
not yet decided, and both need deciding before the Worker starts touching the
database:

**Where does non-trivial SQL live?** The existing migrations already contain
raw SQL — `migrationBuilder.Sql(...)` calls for data transforms, and
`QuestionBankSeedWriter.cs` builds INSERT statements as C# strings. That SQL
is currently inline in C#, which means a reviewer reads PostgreSQL syntax
inside a `MigrationBuilder` call with no syntax highlighting, and a change to
one clause is a diff against a C# string rather than a diff against SQL.

**Who applies a migration, and when?** `deploy-api.yml` currently defines a
dedicated `migrate` job that runs as its own ECS Fargate task, before the new
API task set takes traffic, with an explicit comment: "Never at application
startup — that races the moment more than one task boots."
`deploy-worker.yml` correspondingly declares "the Worker deliberately has no
migrate job. Migrations are owned by the API deployment so that exactly one
workflow can change the schema." That design assumed the API is always the
first, and only, process to need the database.

The Worker no longer fits that assumption. It is a separate deployable
(`src/HpacSafety.Worker`) that will consume `outbox_messages` once the Phase 1
outbox loop lands (ADR-0002), and nothing orders "the API's deploy finishes
and its migrate job succeeds" ahead of "the Worker's deploy finishes and its
task starts." A rolling deploy, a rollback of only one service, or the two
services simply being deployed by different pipeline runs all make it
possible for the Worker to be the first process to open a connection against
a database that has pending migrations. The current design has no answer for
that case beyond "deploy the API first" — a documented ordering constraint
between two independently deployable services is exactly the kind of
constraint that is silently violated the first time someone reasonably
assumes the two are independent.

## Decision

### Schema changes are EF Core migrations, generated in `HpacSafety.Infrastructure`

No hand-written DDL, and no schema change applied by any means other than a
migration reviewed in `src/HpacSafety.Infrastructure/Persistence/Migrations/`.
`HpacSafetyDbContext` remains the one context; there is no second one for a
"Worker schema" or a "reporting schema."

### Simple queries stay in LINQ; complex ones move to a view or stored procedure, in their own `.sql` file

A query that reads naturally as a `Where`/`Select`/`Include` chain stays
exactly that — LINQ in C#, no rule requires "complex-looking" ceremony around
a simple filter. A query that is hard to express or hard to keep correct as
LINQ — multi-table aggregation, a report that joins across the question bank
and its revisions, anything a reviewer would rather read as SQL than as a
chain of C# generic methods — is defined as a PostgreSQL **view** or **stored
procedure**, called from EF Core (`FromSqlRaw` against a keyless entity for a
view; a thin repository method wrapping a function call for a procedure). The
judgment call of "is this complex enough" is left to the author and reviewer;
this ADR does not draw a line in LOC or join count.

Any raw SQL — a view definition, a stored procedure body, or a migration's
data-transform statement — is authored in its own `.sql` file under
`src/HpacSafety.Infrastructure/Persistence/Sql/`, loaded from the migration or
from the DbContext rather than typed as a C# string literal. This applies
going forward; the existing inline SQL in past migrations
(`20260823001528_InitialSchema.cs`, `20260827013637_MigrateCanonicalDomainAndPersistence.cs`,
`QuestionBankSeedWriter.cs`) is not rewritten retroactively — migrations are
immutable history, not a place to backfill a new convention.

### Migrations are applied by the application itself, idempotently, guarded by an advisory lock — supersedes the deploy-job model

There is no dedicated `migrate` deploy job. Both `HpacSafety.Api` and
`HpacSafety.Worker` call one shared routine —
`HpacSafetyDbContext.EnsureMigratedAsync` in `HpacSafety.Infrastructure` —
once at startup, before serving traffic or starting the outbox loop. That
routine:

1. Acquires a PostgreSQL session-level advisory lock
   (`pg_advisory_lock(bigint)`) on a fixed, well-known key before touching the
   migration history.
2. Re-checks `Database.GetPendingMigrationsAsync()` after acquiring the lock —
   not before — because the other process may have applied every pending
   migration while this one was waiting.
3. Calls `Database.MigrateAsync()` only if migrations are still pending, then
   releases the lock.

Whichever of API or Worker starts first after a deploy applies the schema;
the other blocks briefly on the advisory lock and then finds nothing left to
do. This makes "the Worker started before the API" — or the reverse, or both
starting within the same second — safe by construction rather than by
deployment ordering. `deploy-api.yml` no longer needs `ECS_TASK_DEFINITION_MIGRATE`,
`ECS_SUBNETS`, or `ECS_SECURITY_GROUPS`, and neither `deploy-api.yml` nor
`deploy-worker.yml` need a `migrate` job.

This does not weaken migration safety for genuinely dangerous migrations
(a long-running rewrite, a breaking column change): those are still designed
to run online, support both a fresh database and the current-main upgrade
path, and never physically delete data, exactly as `skills/persist-hpac-data/SKILL.md`
already requires. What changes is *who* triggers `MigrateAsync`, not what a
migration is allowed to do.

## Consequences

- `deploy-api.yml`: removes the `migrate` job and its variables
  (`ECS_TASK_DEFINITION_MIGRATE`, `ECS_SUBNETS`, `ECS_SECURITY_GROUPS`); `deploy`
  now depends directly on `image`.
- `deploy-worker.yml`: removes the "migrations are owned by the API
  deployment" comment; the Worker applies migrations exactly the same way the
  API does.
- `HpacSafety.Infrastructure` gains `EnsureMigratedAsync`, and
  `HpacSafety.Worker.csproj` gains its own EF Core package references (it
  already carried an unused `ProjectReference` to `HpacSafety.Infrastructure`).
- Both `Program.cs` entry points call `EnsureMigratedAsync` before their
  respective run loops start.
- A slower first request/first outbox poll immediately after a deploy that
  shipped a migration — the process applying it waits for `MigrateAsync` to
  finish before doing anything else. This is the same cost the old `migrate`
  job paid, just moved earlier into the process that happens to run it,
  and it removes the deploy pipeline's most complex remaining job
  (`aws ecs run-task` plus polling for a `STOPPED` exit code).
- New raw SQL is reviewed as `.sql` files; existing inline SQL in past
  migrations is left as-is.

## Alternatives rejected

**Keep the dedicated deploy-time `migrate` job, and add ordering between the
two deploy workflows (Worker deploy waits on API deploy).** Rejected because
it reintroduces exactly the coupling this ADR removes: two services that are
supposed to be independently deployable now have a hidden dependency that
only becomes visible when someone deploys the Worker on its own and gets a
schema mismatch. Enforcing this in CI (a `needs:` across two separate
workflow files, or a manual runbook step) is more moving parts than one
advisory lock.

**A distributed lock outside PostgreSQL (Redis, DynamoDB) for migration
coordination.** Rejected — there is no such component in this stack, and
introducing one purely to coordinate a migration that runs, at most, once per
deploy is a new piece of infrastructure to justify a problem PostgreSQL's own
advisory locks already solve for free.

**Let both processes race `MigrateAsync()` unguarded, relying on EF Core's
migration-history table and transactional DDL to make the loser's call a
no-op.** EF Core does not guarantee this: two concurrent `MigrateAsync()`
calls against the same pending migration can both see it as pending, both
begin applying it, and the second to commit can fail with a already-exists
error or, worse, partially apply a non-transactional statement. The advisory
lock removes the race outright rather than relying on the second caller
failing gracefully.

## Related

- [ADR-0002](ADR-0002-transactional-outbox.md) — why the Worker needs the
  database at all (it consumes `outbox_messages`)
- [ADR-0020](ADR-0020-seeding-by-migration.md) — seeding is also a migration
  concern; the new apply-on-connect model applies to it the same way
- [ADR-0040](ADR-0040-migrate-canonical-domain-and-persistence.md) — the
  current schema/persistence baseline this ADR builds on
- `skills/persist-hpac-data/SKILL.md` — updated alongside this ADR
