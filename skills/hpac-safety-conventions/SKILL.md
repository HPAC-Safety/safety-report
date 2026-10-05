---
name: hpac-safety-conventions
description: Repository-wide HPAC Safety conventions — .NET, dates, privacy log list, tests, diagrams, and copy — extending the generic coding-conventions skill. Use for any code, test, documentation, or diagram change in this repository.
---

# HPAC Safety conventions

Extends the `coding-conventions` skill; read that
first. This skill holds only what is specific to this repository, under the
same section names.

## Before implementing

- Code graph: `graphify query` / `graphify explain` (see `AGENTS.md`
  "graphify").
- Specification: the feature files' index, the canonical target design.
- Lessons: the lessons index; read it on a design pass.
- Specify first: `AGENTS.md` "Specification-driven development".
- Ownership, the one home: `backend` owns `src/HpacSafety.*` (Api, Core,
  Infrastructure, Worker) with their unit and integration tests, the `tools/`
  scripts, and the CI workflows except those `infrastructure` owns. `ux` owns
  `src/web` and its component tests. `infrastructure` owns `infra/`
  (Terraform) and the workflows that provision or deploy: `terraform.yml`,
  `terraform-relock.yml`, `deploy-environment.yml`, `release.yml`,
  `promote.yml`; the test-writer owns the acceptance step
  definitions; the database-administrator owns the schema's design.
- Focused skills for the surface:
  [`persist-hpac-data`](../persist-hpac-data/SKILL.md),
  [`manage-hpac-migrations`](../manage-hpac-migrations/SKILL.md),
  [`handle-hpac-media`](../handle-hpac-media/SKILL.md),
  [`hpac-domain-model`](../hpac-domain-model/SKILL.md),
  [`anonymize-hpac-reports`](../anonymize-hpac-reports/SKILL.md).

## Privacy

- The boundaries: DTO, storage, model, logging, review, and publication.
- Privacy-sensitive surfaces, each needing a focused privacy or boundary test:
  reports, questions, model input or output, attachments, authentication,
  authorization, logging, deletion, review, and publication.
- **Never log**: DTO bodies, answers, private context, prompts or responses,
  credentials or tokens, client filenames, or attachment URLs.

## .NET

- .NET 10, nullable reference types, async APIs for I/O, cancellation tokens at
  public async boundaries. `Core` has no runtime package dependency.
- **The .NET major moves as one.** It lives in `global.json`,
  `<TargetFramework>`, the Worker's `Dockerfile` base image, and
  `renovate.json`'s `allowedVersions`. An upgrade changes all four in one pull
  request; `node tools/build/check-dotnet-major.ts` fails when they disagree
  (the .NET-major-moves-in-one-pull-request decision).
- **No `Async` suffix** on a method this repository names — the return type
  says it is asynchronous
  (the return-type-says-asynchronous decision).
  - Exception: a member implementing or overriding a contract we do not own
    keeps its given name — `DisposeAsync`, `InitializeAsync`,
    `BackgroundService.ExecuteAsync`, `SaveChangesAsync`, `Stream.ReadAsync`,
    `HttpMessageHandler.SendAsync`, and every EF Core or BCL call.
  - This overrides the upstream `csharp-async` skill on that one point only.
- **Dates**: `DateOnly` for reported dates, `TimeOnly` for local wall time,
  `DateTimeOffset` for instants. Never `DateTime`.

## Tests, diagrams, copy

- Shouldly for .NET assertions; C# tests named `GivenX_WhenY_ThenZ`
  (the scannable test-names decision).
  Detail: [`test-hpac-safety`](../test-hpac-safety/SKILL.md).
- Mermaid for every diagram (the Mermaid-for-diagrams decision).
- UI copy lives in locale catalogues. Database questions carry
  administrator-authored English and French text in each immutable revision.

## Generated files and guards

- A rule lives where it runs: a guard that lives only in CI is not a guard (the
  lesson of that name; the decision that a UI scenario is skipped by Reqnroll
  itself).
- A generated file has no whole-tree total, because one conflicts with every
  branch (the lesson of that name; the decision that every line of the matrix
  derives from one source item).

## Tools

- A script goes in its domain's folder under `tools/` and is named for what it
  does (`check-`, `generate-`, `guard-`, `build-`, `find-`/`read-`, `report-`);
  its test mirrors it under `tests/js/`. See [`tools/README.md`](../../tools/README.md)
  (a workflow step runs one command, and tools is grouped by domain).

## Before finishing

- The specification to update is the feature files.
- `node tools/spec/generate-traceability.ts` exits 0: every built claim's steps
  are bound.
- The conventions most often broken: `DateTime`, an assertion library other
  than Shouldly, a hand-edited generated file.
