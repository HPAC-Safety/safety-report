---
name: hpac-safety-conventions
description: Repository-wide HPAC Safety conventions. Use for any code, test, documentation, or diagram change in this repository.
---

# HPAC Safety conventions

## Before implementing

- **Orient**, even when the task looks small or familiar — drift comes from
  skipping this, not from the change itself:
  - `graphify query` / `graphify explain` (see `AGENTS.md` "graphify");
  - [`features/README.md`](../../features/README.md), the affected canonical
    pages, and the area's **out of scope** section;
  - every ADR that bears on the change;
  - [`docs/lessons/`](../../docs/lessons/README.md).
- When they conflict, source and tests are current state; ADRs and issues are
  history.
- **Stop on a gap.** If the reading leaves a decision unsettled, or two sources
  in tension, ask before proceeding — even under a "spike" or "just get it
  working" framing. Name the gap, the options, and your recommendation. Never
  resolve it by assumption or by picking the easiest option to build.
- **Specify first** (`AGENTS.md` "Specification-driven development"). Say what
  is out of scope for the area you touched, not only what you built.

## Design

- Keep the implementation direct. Use plain code until a real external
  boundary or a second implementation makes an abstraction useful.
- Add an interface only at a real external boundary or when two
  implementations already need a shared contract.
- When a seam is justified, use SOLID and named Gang-of-Four patterns as the
  vocabulary. The seam earns the pattern; naming a pattern never earns the
  seam.

## Privacy

- Protect privacy at DTO, storage, model, logging, review, and publication
  boundaries.
- Use synthetic data only, in tests and docs.
- **Never log**: DTO bodies, answers, private context, prompts or responses,
  credentials or tokens, client filenames, or attachment URLs.

## .NET

- .NET 10, nullable reference types, async APIs for I/O, cancellation tokens at
  public async boundaries. `Core` has no runtime package dependency.
- **The .NET major moves as one.** It lives in `global.json`,
  `<TargetFramework>`, the Worker's `Dockerfile` base image, and
  `renovate.json`'s `allowedVersions`. An upgrade changes all four in one pull
  request; `node tools/dotnet-major.mjs` fails when they disagree
  ([ADR-0120](../../docs/decisions/ADR-0120-the-dotnet-major-moves-in-one-pull-request.md)).
- **No `Async` suffix** on a method this repository names — the return type
  says it is asynchronous
  ([ADR-0093](../../docs/decisions/ADR-0093-the-return-type-says-a-method-is-asynchronous.md)).
  - Exception: a member implementing or overriding a contract we do not own
    keeps its given name — `DisposeAsync`, `InitializeAsync`,
    `BackgroundService.ExecuteAsync`, `SaveChangesAsync`, `Stream.ReadAsync`,
    `HttpMessageHandler.SendAsync`, and every EF Core or BCL call.
  - This overrides the upstream `csharp-async` skill on that one point only.
- **Dates**: `DateOnly` for reported dates, `TimeOnly` for local wall time,
  `DateTimeOffset` for instants. Never `DateTime`.

## Tests, diagrams, copy

- Shouldly for .NET assertions; C# tests named `GivenX_WhenY_ThenZ`
  ([ADR-0069](../../docs/decisions/ADR-0069-scannable-given-when-then-test-names.md)).
  Detail: [`test-hpac-safety`](../test-hpac-safety/SKILL.md).
- Mermaid for every diagram
  ([ADR-0046](../../docs/decisions/ADR-0046-mermaid-for-diagrams.md)).
- UI copy lives in locale catalogues. Database questions carry
  administrator-authored English and French text in each immutable revision.

## Generated files and guards

- Never hand-edit a generated file.
- **Put a rule where it runs, not only where it is checked.** A convention
  enforced by a CI command-line flag holds only in CI; a local `dotnet test`,
  an IDE run, or another agent's session escapes it. Enforce it in code, a
  hook, or the tool that owns the artifact; CI is the backstop
  ([lesson 0001](../../docs/lessons/0001-a-guard-that-lives-only-in-ci-is-not-a-guard.md),
  [ADR-0073](../../docs/decisions/ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md)).
- **A committed generated file merges the way its sources do.**
  - Every line derives from one source item; no whole-tree totals or counts.
  - Independent items are sorted by a stable key and separated by unchanged
    lines, so git's merge of two correct copies is correct.
  - A count belongs in the generator's output or a CI job summary
    ([lesson 0013](../../docs/lessons/0013-a-generated-file-with-a-whole-tree-total-conflicts-with-every-branch.md),
    [ADR-0106](../../docs/decisions/ADR-0106-every-line-of-the-matrix-derives-from-one-source-item.md)).

## Before finishing

- Run the narrowest relevant checks.
- Inspect the diff for unrelated changes.
- Update `/features` whenever the target design changes.
