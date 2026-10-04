---
name: hpac-role-agents
description: HPAC Safety's paths, tags, commands, ADRs, and privacy boundaries for the ten role agents — spec-author, test-writer, spec-reviewer, ai-author, database-administrator, critic, adversary, backend, ux, infrastructure — which are generic. Use whenever acting as one of those roles in this repository.
---

# HPAC Safety role agents

Extends the generic role agents under [`agents/`](../../agents/). Each section
holds only what is specific to this repository for that role, and is named
with the agent's persona, as in "adversary (Kyle)". An agent file says who the
agent is and what it does; how it works is in the generic skills it preloads
([CONV-008](../../.spec/conventions/CONV-008-an-agent-file-is-a-persona-and-a-role-its-how-lives-in-skills.md)).
The four chain roles and why each trusts only the artifact before it:
[ADR-0197](../../.spec/decisions/ADR-0197-backend-and-ux-replace-the-implementer-and-the-adversary-takes-privacy-review.md),
which CONV-008 keeps.

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
- Persona, model, and effort, declared in each agent's frontmatter and
  description
  ([CONV-008](../../.spec/conventions/CONV-008-an-agent-file-is-a-persona-and-a-role-its-how-lives-in-skills.md)):

  | Role | Persona | model | effort | Why |
  |---|---|---|---|---|
  | spec-author | Jennifer | opus | high | Judgement: reads widely, decides what to build |
  | spec-reviewer | Jessica | opus | high | Judgement: weighs a diff against claims and ADRs |
  | database-administrator | Jane | opus | high | Judgement: schema mistakes outlive the code |
  | test-writer | Kevin | sonnet | medium | Build: binds a written scenario |
  | ai-author | Emily | sonnet | medium | Build: rewrites wording, never rules |
  | critic | Karen | opus | medium | Judgement: weighs a plan, one bounded pass; medium because the loop is capped and the findings are cited, not open-ended |
  | adversary | Kyle | opus | high | Judgement: hunts the hardest-to-see bugs and holes, read-only |
  | backend | Brad | sonnet | medium | Build: designs and builds the server side within settled claims |
  | ux | Tiffany | sonnet | medium | Build: designs and builds the web UI within settled claims |
  | infrastructure | Dave | opus | high | Judgement: cloud and network mistakes outlive the code and reach production |

- No role uses the clone's shared stash, above all the builders and the
  test-writer, who edit files. The rule is written only; nothing enforces it
  (`deliver-hpac-change` "Worktree and branch", #796).
- `skillfile install` copies each agent verbatim into `.claude/agents/`. An
  orchestrator spawns a role by its `name` and gets these settings, unless it
  overrides `model` for one call.

## spec-author (Jennifer)

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

## test-writer (Kevin)

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

## spec-reviewer (Jessica)

- Never put report content, answers, or credentials in a finding.
- Contradiction between a feature file and an ADR:
  [ADR-0047](../../.spec/decisions/ADR-0047-feature-files-must-not-contradict-adrs.md).
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

## database-administrator (Jane)

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

## ai-author (Emily)

The role and its scope:
[CONV-008](../../.spec/conventions/CONV-008-an-agent-file-is-a-persona-and-a-role-its-how-lives-in-skills.md),
which lists what still holds of ADR-0121 and
[ADR-0124](../../.spec/decisions/ADR-0124-the-ai-author-role-may-register-its-files-and-mend-links.md).
Generic and project files, and the naming rule:
[CONV-009](../../.spec/conventions/CONV-009-a-skills-name-says-hpac-exactly-when-it-is-project-specific.md).
How the files are written: the generic
[`write-agent-instructions`](../write-agent-instructions/SKILL.md).

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
  "Markdown". Its body is the three sections CONV-008 names.
- Checks:
  - `node tools/docs/check-frontmatter.ts` (rules:
    [`deliver-hpac-change`](../deliver-hpac-change/SKILL.md) "Markdown");
  - `node tools/docs/check-generic-instructions.ts` — every agent and every
    skill whose directory has no `hpac` names nothing specific to this
    repository. It selects the files by that rule, so there is no list to
    update. The pre-commit hook and the `docs` CI job run it;
  - `node tools/docs/check-links.ts` — every relative link resolves.
- Stable handles here include `deliver-change` "Verify and publish" step
  numbers.

## critic (Karen)

The rule and its bound:
[CONV-007](../../.spec/conventions/CONV-007-a-plan-meets-the-critic-and-a-change-meets-the-adversary.md).
A `PreToolUse` hook (`tools/github/remind-critic.ts`, wired in
`.claude/settings.json`) reminds on `ExitPlanMode` and on `gh issue create`;
it never blocks.

- Read: [`.spec/features/README.md`](../../.spec/features/README.md) and the
  affected area's README (its out-of-scope section), the `AGENTS.md` product
  invariants and "Not built", the ADRs the plan touches
  ([`.spec/README.md`](../../.spec/README.md)), and the lessons
  ([`.spec/lessons/`](../../.spec/lessons/README.md)).
- A plan that changes behavior without a scenario first conflicts with
  [ADR-0083](../../.spec/decisions/ADR-0083-specification-driven-development.md).
  A plan that adds a user table, allowlist, credential proxy, or outbound
  email conflicts with "Not built".
- Cite the claim ID, ADR number, or file for each finding.
- Never put report content, answers, or credentials in a finding.

## adversary (Kyle)

Runs by convention only
([CONV-007](../../.spec/conventions/CONV-007-a-plan-meets-the-critic-and-a-change-meets-the-adversary.md)).

- Contract boundaries here: the API's request and response DTOs, the public
  views, the Worker's model input and output, the migrations and SQL views,
  and the pre-signed URL and quarantine flow.
- Privacy and security are this role's, not the spec-reviewer's. Boundaries to attack: report content or credentials in logs; a
  public DTO grown a field; a private-only fact reaching a summary; a document
  reaching the model; an attachment published without media consent; a
  receipt or token stored in the clear; a physical deletion.
- Missing tests: a privacy-sensitive surface (see backend) with no focused
  boundary test.
- Findings are synthetic-only: never copy real report content into one.
- Test commands it may run: a filtered `dotnet test`, `CI=1 npm test` for e2e;
  never against production data.

## backend (Brad)

The back-end engineer: designs and builds the server side
([CONV-008](../../.spec/conventions/CONV-008-an-agent-file-is-a-persona-and-a-role-its-how-lives-in-skills.md)).

- Owns `src/HpacSafety.*` (Api, Core, Infrastructure, Worker) and their unit
  and integration tests, `tools/` scripts, and the CI workflows. The web
  UI under `src/web` is `ux`'s; `infra/` is `infrastructure`'s; the acceptance
  step definitions are the test-writer's; the schema's design is the
  database-administrator's.
- Code graph: `graphify query "<question>"`.
- Conventions: [`hpac-safety-conventions`](../hpac-safety-conventions/SKILL.md),
  plus the focused skill for the surface:
  [`persist-hpac-data`](../persist-hpac-data/SKILL.md),
  [`manage-hpac-migrations`](../manage-hpac-migrations/SKILL.md),
  [`handle-hpac-media`](../handle-hpac-media/SKILL.md),
  [`hpac-domain-model`](../hpac-domain-model/SKILL.md),
  [`anonymize-hpac-reports`](../anonymize-hpac-reports/SKILL.md).
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
- Work only in its own worktree off fresh `origin/main`
  ([`deliver-change`](../deliver-change/SKILL.md) "Worktree and branch").

## ux (Tiffany)

The UX designer and front-end engineer: designs and builds the web UI.

- Owns `src/web` and its component tests; server code is `backend`'s, and the
  e2e step definitions the test-writer's.
- Follows the backend (Brad) section's rules for
  claims, the graph, the exemption, and the never-log list, and
  [`build-hpac-web-ui`](../build-hpac-web-ui/SKILL.md) and
  [`localize-hpac-app`](../localize-hpac-app/SKILL.md).
- Every UI change carries a Playwright test, and a server test for any
  API-facing behavior
  ([ADR-0045](../../.spec/decisions/ADR-0045-ui-changes-require-playwright-and-server-tests.md)).
- Privacy boundaries: nothing about a member in the browser beyond the
  receipt the browser keeps; no report content in analytics or logs; fixtures
  stay synthetic.
- Work only in its own worktree off fresh `origin/main`.

## infrastructure (Dave)

The cloud and DevOps engineer: designs and builds what the system runs on
([CONV-008](../../.spec/conventions/CONV-008-an-agent-file-is-a-persona-and-a-role-its-how-lives-in-skills.md)).

- Owns `infra/` (Terraform) and the workflows that provision or deploy:
  `terraform.yml`, `terraform-relock.yml`, `deploy-environment.yml`,
  `release.yml`, `promote.yml`. `backend` keeps the other workflows.
- Skill: [`manage-hpac-infrastructure`](../manage-hpac-infrastructure/SKILL.md);
  constraints:
  [`.spec/infrastructure-and-operations.md`](../../.spec/infrastructure-and-operations.md)
  (`CON-INF-*`), and AGENTS.md invariant 8 (managed encryption, no
  deletion).
- The owner promotes to production.
- Never put report content, a secret, or a state file in a report.
- Work only in its own worktree off fresh `origin/main`.
