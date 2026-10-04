---
name: hpac-role-agents
description: HPAC Safety's paths, tags, commands, ADRs, and privacy boundaries for the six role agents — spec-author, test-writer, implementer, spec-reviewer, ai-author, database-administrator — which are generic. Use whenever acting as one of those roles in this repository.
---

# HPAC Safety role agents

Extends the generic role agents under [`agents/`](../../agents/). Each section
holds only what is specific to this repository for that role. The four chain
roles and why each trusts only the artifact before it:
[ADR-0086](../../.spec/decisions/ADR-0086-four-role-agents-defined-in-the-repository.md).

## Every role

- Specification index: [`.spec/README.md`](../../.spec/README.md), generated
  by `node tools/spec/generate-spec-index.ts` — every area, constraint page, ADR, and
  lesson. Authority rules:
  [`.spec/features/README.md`](../../.spec/features/README.md). Layout:
  [`deliver-hpac-change`](../deliver-hpac-change/SKILL.md) "Specification
  directory".
- Area files: `.spec/features/<area>/<area>.feature`, with supporting detail in the
  sibling `.spec/features/<area>/README.md`, which also holds the out-of-scope
  section.
- Claims: [`.spec/claims.json`](../../.spec/claims.json), every claim with its
  steps, bindings, and citing ADRs and lessons, and the one-row-per-claim
  matrix [`.spec/traceability.md`](../../.spec/traceability.md); both generated
  by `node tools/spec/generate-traceability.ts`
  ([ADR-0193](../../.spec/decisions/ADR-0193-the-claims-are-generated-as-json-a-graph-fragment-and-one-slim-matrix.md)).
  In the graph: `graphify query "<claim ID>"`.
- Specification-driven development, a superseded scenario deleted, and never
  editing a scenario to match the code:
  [ADR-0083](../../.spec/decisions/ADR-0083-specification-driven-development.md).
- Browser tag: `@ui`. Not built yet: `@ignore @issue-<N>`, naming the open
  issue that will build it
  ([CONV-001](../../.spec/conventions/CONV-001-a-scenario-counts-only-in-its-own-area-and-an-ignored-one-names-its-issue.md)).
- Every example and fixture is synthetic: never real report content.
- Model and effort, declared in each agent's frontmatter
  ([ADR-0182](../../.spec/decisions/ADR-0182-a-role-agent-declares-its-model-and-effort.md)):

  | Role | model | effort | Why |
  |---|---|---|---|
  | spec-author | opus | high | Judgement: reads widely, decides what to build |
  | spec-reviewer | opus | high | Judgement: weighs a diff against claims and ADRs |
  | database-administrator | opus | high | Judgement: schema mistakes outlive the code |
  | implementer | sonnet | medium | Build: executes claims already settled |
  | test-writer | sonnet | medium | Build: binds a written scenario |
  | ai-author | sonnet | medium | Build: rewrites wording, never rules |

- No role uses the clone's shared stash, above all the implementer and the
  test-writer, who edit files. The rule is written only; nothing enforces it
  (`deliver-hpac-change` "Worktree and branch", #796).
- `skillfile install` copies each agent verbatim into `.claude/agents/`. An
  orchestrator spawns a role by its `name` and gets these settings, unless it
  overrides `model` for one call.

## spec-author

- Clarify with [`clarify-requirements`](../clarify-requirements/SKILL.md).
- Claim IDs are `@REQ-<AREA>-<NNN>`, never reused or renumbered
  ([ADR-0084](../../.spec/decisions/ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md)).
- A new claim's ID: `node tools/spec/claim-prefixes.ts --next <area>`, under
  the `prefix:` in the area's README. `REQ-QB` and `REQ-MOD` are retired: their
  claims keep their IDs in whichever area holds them, and nothing new takes
  them ([ADR-0194](../../.spec/decisions/ADR-0194-a-split-area-keeps-every-claim-id-and-a-new-claim-takes-the-new-areas-prefix.md)).
- Words: each concept as [`.spec/glossary.md`](../../.spec/glossary.md) names
  it; quote interface copy and page titles. A renamed step renames its step
  definition's text in the same commit
  ([CONV-003](../../.spec/conventions/CONV-003-scenarios-and-area-readmes-use-the-glossary.md)).
- Steps say what a reader observes: an outcome phrase from the glossary, never
  a status code, a table or column, a route, "the API", or a reason; the step
  definition keeps the detail
  ([CONV-004](../../.spec/conventions/CONV-004-scenarios-describe-behavior-not-implementation.md)).
- One behavior per scenario: at most one When, never after a Then, and at
  most 8 steps; a later action is a new scenario with a new ID from
  `node tools/spec/claim-prefixes.ts --next <area>`. No click, press, typing,
  role, selector, or pixel in a step; a key is a noun phrase in an Examples
  cell ("the Escape key")
  ([CONV-006](../../.spec/conventions/CONV-006-a-scenario-holds-one-behavior.md)).
- Run `node tools/spec/generate-traceability.ts`,
  `node tools/spec/generate-spec-index.ts`,
  `node tools/spec/check-glossary.ts`, and
  `node tools/gherkin/lint-scenarios.ts` before finishing.
- Decision records: [`.spec/decisions/TEMPLATE.md`](../../.spec/decisions/TEMPLATE.md),
  immutable once accepted; conventions: `.spec/conventions/` ([ADR-0192](../../.spec/decisions/ADR-0192-an-accepted-adr-is-immutable-and-process-rules-are-conventions.md)).
  `node tools/spec/check-records.ts` passes before finishing.

## test-writer

- Runners
  ([ADR-0053](../../.spec/decisions/ADR-0053-ui-scenarios-execute-via-playwright-bdd.md)):
  - untagged scenario: Reqnroll, in `tests/HpacSafety.Acceptance.Tests`;
  - `@ui` scenario: playwright-bdd, in `tests/e2e/steps`.
- Conventions and fixtures:
  [`test-hpac-safety`](../test-hpac-safety/SKILL.md).
- Start from the claim's entry in
  [`.spec/claims.json`](../../.spec/claims.json): its steps with no `files` are
  the definitions to write. Remove `@ignore` and its `@issue-<N>` once the
  entry says `"staleIgnore": true` and the scenario passes; from then on CI
  fails the claim unless it passes in every run
  ([ADR-0195](../../.spec/decisions/ADR-0195-a-built-claim-counts-only-when-its-scenario-passed-in-the-run.md)).
- A Then's definition asserts and never acts; a key comes from its Examples
  cell through `tests/e2e/steps/keys.ts`
  ([CONV-006](../../.spec/conventions/CONV-006-a-scenario-holds-one-behavior.md)).
- Test code is C# or TypeScript.
- Synthetic fixtures: people, locations, reports, attachments.
- The required phrases in model output are the role phrases.

## implementer

- Code graph: `graphify query "<question>"`.
- Conventions: [`hpac-safety-conventions`](../hpac-safety-conventions/SKILL.md),
  plus the focused skill for the surface — persistence, media, localization,
  domain model, web UI.
- Privacy-sensitive surfaces that need a focused privacy or boundary test:
  reports, questions, model input or output, attachments, authentication,
  authorization, logging, deletion, review, and publication.
- The exemption is `No .feature scenario needed:` and names the claims it
  preserves
  ([ADR-0090](../../.spec/decisions/ADR-0090-an-exemption-cites-the-claims-it-preserves.md)).
- The never-log list is `hpac-safety-conventions` "Privacy": DTO bodies,
  answers, private context, prompts or responses, credentials or tokens, client
  filenames, attachment URLs.
- Conventions most often broken: `DateTime`, an assertion library other than
  Shouldly, a hand-edited generated file.
- Before finishing, `node tools/spec/generate-traceability.ts` exits 0: every
  built claim's steps are bound.

## spec-reviewer

- Contradiction between a feature file and an ADR:
  [ADR-0047](../../.spec/decisions/ADR-0047-feature-files-must-not-contradict-adrs.md).
- Privacy boundaries: report content or credentials in logs, a document
  reaching the model, a public DTO grown a field, a private-only fact in a
  summary.
- The exemption: `No .feature scenario needed:`
  ([ADR-0090](../../.spec/decisions/ADR-0090-an-exemption-cites-the-claims-it-preserves.md)).
- `.spec/claims.json` for the cited claims: each bound by the files the diff
  touches; no new entry in `unusedStepDefinitions` or `ambiguousSteps`, and no
  new `"staleIgnore": true`, the diff caused
  ([ADR-0184](../../.spec/decisions/ADR-0184-a-generated-map-binds-every-claim-to-its-step-definitions.md)).
- Words: `node tools/spec/check-glossary.ts` passes; a banned synonym is a
  specification delta, never a matter of taste
  ([CONV-003](../../.spec/conventions/CONV-003-scenarios-and-area-readmes-use-the-glossary.md)).
- Declarative steps: `node tools/gherkin/lint-scenarios.ts` passes, and a
  reworded step's definition still asserts what it asserted before
  ([CONV-004](../../.spec/conventions/CONV-004-scenarios-describe-behavior-not-implementation.md)).
- One behavior: every assertion of a split scenario survives in exactly one
  scenario, the original ID stays with the main behavior, and no Then step
  definition performs an action the scenario claims
  ([CONV-006](../../.spec/conventions/CONV-006-a-scenario-holds-one-behavior.md)).
- Records: `node tools/spec/check-records.ts` and, with the pull request's
  base as `BASE_SHA`, `node tools/spec/check-adr-immutability.ts` both pass
  ([ADR-0192](../../.spec/decisions/ADR-0192-an-accepted-adr-is-immutable-and-process-rules-are-conventions.md)).

## database-administrator

- Conventions and commands:
  [`manage-hpac-migrations`](../manage-hpac-migrations/SKILL.md), then
  [`persist-hpac-data`](../persist-hpac-data/SKILL.md) for records, queries,
  and soft deletion.
- They override the generic database skills: tiny `char(11)` keys, never
  `uuid` or identity
  ([ADR-0034](../../.spec/decisions/ADR-0034-tiny-ids.md)); no `DateTime`; no
  physical deletion (`AGENTS.md` invariant 8); enums as `varchar` codes with a
  `CHECK`.
- The schema as built:
  [`Persistence/Migrations/README.md`](../../src/HpacSafety.Infrastructure/Persistence/Migrations/README.md);
  the target: [`.spec/data-and-persistence.md`](../../.spec/data-and-persistence.md).
- A disposable database: the `postgres` service in `docker-compose.yml`, or
  the PostgreSQL container the API and Infrastructure test suites start.
- Tables hold personal and medical information. Audit queries return counts
  and shapes, never row content.

## ai-author

The role: [ADR-0121](../../.spec/decisions/ADR-0121-a-fifth-role-maintains-the-agent-instructions.md),
amended by
[ADR-0124](../../.spec/decisions/ADR-0124-the-ai-author-role-may-register-its-files-and-mend-links.md).
Generic and project files:
[ADR-0131](../../.spec/decisions/ADR-0131-a-generic-skill-names-no-project-and-a-project-skill-extends-it.md).

- **Edits**: `AGENTS.md`, `skills/*/SKILL.md` (and a skill's `agents/*.yaml`),
  `agents/*.md`, and their `Skillfile` entries.
- **Never edits**:
  - generated copies under `.claude/` — run `skillfile install` instead;
  - the symlinks `CLAUDE.md`, `.github/copilot-instructions.md`,
    `.cursor/rules/agents.mdc`;
  - the Worker's runtime prompts under `src/HpacSafety.Worker/Prompts/`;
  - product code, tests, anything under `.spec/`, or `docs/**` pages, except
    to fix a link a move broke.
- What `AGENTS.md` owns and what belongs in a skill:
  [ADR-0037](../../.spec/decisions/ADR-0037-progressive-agent-instructions.md).
- Where a lesson's general rule lands:
  [`deliver-hpac-change`](../deliver-hpac-change/SKILL.md) "Lessons".
- Product behavior lives in `.spec/features`
  ([ADR-0085](../../.spec/decisions/ADR-0085-a-lesson-flows-upstream-into-the-specification.md)).
- An agent's frontmatter keys: [`deliver-hpac-change`](../deliver-hpac-change/SKILL.md)
  "Markdown".
- Checks:
  - `node tools/docs/check-frontmatter.ts` (rules:
    [`deliver-hpac-change`](../deliver-hpac-change/SKILL.md) "Markdown");
  - `node tools/docs/check-generic-instructions.ts` — every generic skill and agent
    it lists names nothing specific to this repository. A new generic file is
    added to its list; a new split's project skill is not. The pre-commit hook
    and the `docs` CI job run it;
  - `node tools/docs/check-links.ts` — every relative link resolves.
- Stable handles here include `deliver-change` "Verify and publish" step
  numbers.
