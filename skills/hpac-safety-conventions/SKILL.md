---
name: hpac-safety-conventions
description: Repository-wide HPAC Safety conventions — .NET, dates, privacy log list, tests, diagrams, and copy — extending the generic coding-conventions skill. Use for any code, test, documentation, or diagram change in this repository.
---

# HPAC Safety conventions

Extends [`coding-conventions`](../coding-conventions/SKILL.md); read that
first. This skill holds only what is specific to this repository, under the
same section names.

## Before implementing

- Code graph: `graphify query` / `graphify explain` (see `AGENTS.md`
  "graphify").
- Specification: [`.spec/features/README.md`](../../.spec/features/README.md).
- Lessons: [`.spec/lessons/`](../../.spec/lessons/README.md).
- Specify first: `AGENTS.md` "Specification-driven development".

## Privacy

- The boundaries: DTO, storage, model, logging, review, and publication.
- **Never log**: DTO bodies, answers, private context, prompts or responses,
  credentials or tokens, client filenames, or attachment URLs.

## .NET

- .NET 10, nullable reference types, async APIs for I/O, cancellation tokens at
  public async boundaries. `Core` has no runtime package dependency.
- **The .NET major moves as one.** It lives in `global.json`,
  `<TargetFramework>`, the Worker's `Dockerfile` base image, and
  `renovate.json`'s `allowedVersions`. An upgrade changes all four in one pull
  request; `node tools/build/check-dotnet-major.ts` fails when they disagree
  ([ADR-0120](../../.spec/decisions/ADR-0120-the-dotnet-major-moves-in-one-pull-request.md)).
- **No `Async` suffix** on a method this repository names — the return type
  says it is asynchronous
  ([ADR-0093](../../.spec/decisions/ADR-0093-the-return-type-says-a-method-is-asynchronous.md)).
  - Exception: a member implementing or overriding a contract we do not own
    keeps its given name — `DisposeAsync`, `InitializeAsync`,
    `BackgroundService.ExecuteAsync`, `SaveChangesAsync`, `Stream.ReadAsync`,
    `HttpMessageHandler.SendAsync`, and every EF Core or BCL call.
  - This overrides the upstream `csharp-async` skill on that one point only.
- **Dates**: `DateOnly` for reported dates, `TimeOnly` for local wall time,
  `DateTimeOffset` for instants. Never `DateTime`.

## Tests, diagrams, copy

- Shouldly for .NET assertions; C# tests named `GivenX_WhenY_ThenZ`
  ([ADR-0069](../../.spec/decisions/ADR-0069-scannable-given-when-then-test-names.md)).
  Detail: [`test-hpac-safety`](../test-hpac-safety/SKILL.md).
- Mermaid for every diagram
  ([ADR-0046](../../.spec/decisions/ADR-0046-mermaid-for-diagrams.md)).
- UI copy lives in locale catalogues. Database questions carry
  administrator-authored English and French text in each immutable revision.

## Generated files and guards

- Why a rule lives where it runs:
  [lesson 0001](../../.spec/lessons/0001-a-guard-that-lives-only-in-ci-is-not-a-guard.md),
  [ADR-0073](../../.spec/decisions/ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md).
- Why a generated file has no whole-tree total:
  [lesson 0013](../../.spec/lessons/0013-a-generated-file-with-a-whole-tree-total-conflicts-with-every-branch.md),
  [ADR-0106](../../.spec/decisions/ADR-0106-every-line-of-the-matrix-derives-from-one-source-item.md).

## Tools

- A script goes in its domain's folder under `tools/` and is named for what it
  does (`check-`, `generate-`, `guard-`, `build-`, `find-`/`read-`, `report-`);
  its test mirrors it under `tests/js/`. See [`tools/README.md`](../../tools/README.md)
  ([ADR-0189](../../.spec/decisions/ADR-0189-a-workflow-step-runs-one-command-and-tools-is-grouped-by-domain.md)).

## Before finishing

- The specification to update is `.spec/features`.
