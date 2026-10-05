---
name: deliver-hpac-change
description: HPAC Safety's tools, commands, labels, and paths for delivering and reviewing a change — extends the generic deliver-change skill, and is the project companion of spec-driven-development ("Specification-driven development"), review-work ("Review"), and write-agent-instructions ("Agent instructions"). Use when creating or editing issues, docs, worktrees, PRs, or checks in this repository.
---

# Deliver an HPAC Safety change

Extends the `deliver-change` skill; read that first. This
skill holds only what is specific to this repository, under the same section
names and step numbers. It is also the project companion of
the `spec-driven-development` skill, in "Specification-driven development", of
the `review-work` skill, in "Review", and of
the `write-agent-instructions` skill, in "Agent
instructions".

## Start

### Settle the requirement before building

- Asking: put the question to the owner directly; for genuinely ambiguous
  product behavior also read
  the `clarify-requirements` skill. Record the
  answer in the issue, and in the specification where it changes behavior,
  before any code (the specification-driven-development decision).
- Sequencing: an SQL view under `Persistence/Sql/` is a view, and an EF
  migration a migration, for the sequencing rule.

### File a new issue

- **Labels**:
  - one type — `enhancement`, `bug`, `documentation`, or `tech-debt`;
  - every `area:*` the change touches;
  - the `phase:*` matching a phase milestone.
- Relationship query owner and name: `owner:"HPAC-Safety",name:"safety-report"`.

### Worktree and branch

- Session label: `tools/dev/session-label.sh "#<number> <short-description>"`
  (and the later relabels in "Verify and publish").
- Why the first-edit check exists: the lesson that a rule read once is not a
  rule checked again.
- The shared-stash rule (#796) is written, not enforced: git has no pre-stash
  hook, and the owner chose not to add an agent tool hook for it.
- Git hooks: `./init-dev.sh` installs `tools/dev/git-hook-shim.sh` under each
  name, and it runs the tracked `.githooks/<name>` of the current worktree. Edit
  `.githooks/<name>`, never the installed file; a moved tool needs no re-install
  (an amendment to the one-command-workflow-step decision). Never set
  `core.hooksPath` or add a hook manager such as Husky (the git-hooks-are-tracked, run-through-a-shim, one-check-per-file convention).
- A new pre-commit check is a new file, `.githooks/pre-commit.d/NN-<check>.sh`:
  it gates itself on `$STAGED`, says what it guards and its CI backstop, and
  exits non-zero to fail. `.githooks/pre-commit` only runs them all, in order,
  and names each that failed.
- `post-merge` and `post-rewrite` regenerate and stage the generated claims
  file, traceability matrix, and specification index, then merge the specification
  into the local graphify graph, through the shared
  `.githooks/lib/regenerate-spec.sh`. On `main` they only merge the graph, which is
  untracked: the primary checkout only fast-forwards to `origin/main`, which
  already carries the generated files, so a pull leaves `main` clean (#802).

### Commit, rebase, claim identifiers

- Why identifiers are claimed after the rebase: the lesson that a number is
  claimed the moment someone else merges.
- ADR number: `node tools/spec/adr-numbers.ts --next`.
- Lost the race? `node tools/spec/adr-numbers.ts --renumber <old> <new>` moves the
  file and rewrites every reference.
- Two records already share the number? Add `--file <name>` to say which
  moves. Bare ADR-number mentions it leaves alone are ambiguous; resolve them
  by hand.

### Before editing

- The specification is the feature files.

## Document

### Specification

Everything the specification chain reads, and its checks, are under
"Specification-driven development" below.

### Inventories

- A new directory under `src/` gets a row in
  [`docs/source-inventory.md`](../../docs/source-inventory.md), and a removed
  one loses its row. `node tools/docs/check-inventories.ts` fails the pre-commit
  hook and the `docs` job otherwise.
- The issue-traceability page under `docs/` is
  generated from GitHub; never edit it by hand, and a pull request that
  closes an issue leaves it alone. Drift never fails a pull request; it keeps
  an "Issue traceability drift" issue open, and whoever resolves that issue
  runs `node tools/spec/generate-issue-traceability.ts` in a pull request that
  closes it (the issue-traceability-drift and no-hand-written-status-page
  decisions).

### Markdown

- Every tracked markdown file opens with frontmatter: `title`, `description`,
  and `type` — one of `adr`, `spec`, `guide`, `readme`, `lesson`,
  `convention`, `instructions`, `template` — plus the keys that type adds
  (the every-markdown-file-declares-itself decision):
  - `adr`: `status`, `date`, `decision-makers`, `keywords`;
  - `lesson`: `date`, `issue`, `status`, `kind` (the immutable-ADR decision);
  - `convention`: `status` (`accepted` or `superseded`) and `date`;
  - `spec`: `area`.
- A `skills/*/SKILL.md` carries exactly `name` and `description` instead; its
  type comes from its path.
- An agent file (in agent-team) carries `name`, `description`, `model`, and `effort`, and
  may carry the other keys Claude Code reads on an agent: `tools`,
  `disallowedTools`, `permissionMode`, `maxTurns`, `skills`, `memory`,
  `isolation`, `background`. Nothing else (the agent-file persona-and-role
  convention).
- The Worker's runtime prompts are exempt; their bytes are the model payload.
- `node tools/docs/check-frontmatter.ts` is the authority; the pre-commit hook runs
  it over staged markdown.
- Never include real report content.

### Agent instructions

- The `ai-author` role is the `ai-author` agent, imported from agent-team.
  The role and its scope, with what still holds of the ai-author role
  decisions: the agent-file persona-and-role convention. Generic and project
  files, and the naming rule: the skill-name-says-hpac convention.
  How the files are written:
  the `write-agent-instructions` skill.
- **Edits**: `AGENTS.md`, `skills/*/SKILL.md` (and a skill's `agents/*.yaml`),
  and their `Skillfile` entries. The role agents and generic skills are edited
  in agent-team, then their pin is bumped here.
- **Never edits**:
  - generated copies under `.claude/` — run `skillfile install` instead;
  - the symlinks `CLAUDE.md`, `.github/copilot-instructions.md`,
    `.cursor/rules/agents.mdc`;
  - the Worker's runtime prompts under `src/HpacSafety.Worker/Prompts/`;
  - product code, tests, anything in the specification directory, or `docs/**`
    pages, except
    to fix a link a move broke.
- What `AGENTS.md` owns and what belongs in a skill: the always-loaded-contract
  rule (`AGENTS.md` owns invariants and routing, skills own procedure). Product behavior lives in the
  feature files (the lesson-flows-upstream decision);
  where a lesson's general rule lands: "Specification-driven development" "Lessons".
- An agent's frontmatter keys: "Markdown" above. Its body is the three sections
  the agent-file convention names.
- A role has no per-role project skill: project rules reach it through the
  companions of the generic skills it preloads, which `AGENTS.md`'s skill table
  lists (the skill-name-says-hpac convention).
- Checks:
  - `node tools/docs/check-frontmatter.ts` (rules: "Markdown" above);
  - `node tools/docs/check-generic-instructions.ts` — every agent and every
    skill whose directory has no `hpac` names nothing specific to this
    repository. It selects the files by that rule, so there is no list to
    update. It also fails a decision, lesson, convention, claim, or
    specification reference in every agent and skill, `hpac` ones included
    (paths to code and tools are fine). The pre-commit hook and the `docs` CI
    job run it;
  - `node tools/docs/check-links.ts` — every relative link resolves.
- Stable handles here include `deliver-change` "Verify and publish" step
  numbers.
- Manifest and lock: update `Skillfile`, regenerate `Skillfile.lock`, and run
  `skillfile validate` and `skillfile install`. Generated copies live under
  `.claude/`.

## Specification-driven development

Extends the `spec-driven-development` skill; read that first. This section
holds what differs here: the repository's own tools under `tools/spec/`,
`tools/docs/`, and `tools/gherkin/` own the specification directory (the
tools-own-the-specification-directory convention). It applies to every role
that reads the generic skill, `spec-author` and `spec-reviewer` included.

### The generator

The generic skill's `$SDD` (`scripts/sdd.ts`) is not used here.

- Never run `init`, `new`, or `generate` without `--check`: it would overwrite
  the generated files in a shape that lacks the step-definition bindings.
- `check`, `generate --check`, and `coverage` are not gates: they fail
  hundreds of times on this tree, and the repository's own checks are the gate.
- Where the generic skill says a command, run the tool:

| Generic | Here |
|---|---|
| `next-claim <area>` | `node tools/spec/claim-prefixes.ts --next <area>`; it reads this checkout only, not remote branches, so rebase onto fresh `origin/main` first and expect a race with another open branch |
| `next-adr` | `node tools/spec/adr-numbers.ts --next` |
| `new adr` | copy the decisions directory's template, then the number above |
| `new lesson`, `new convention`, `new feature`, `new page` | written by hand from the README or template in its directory; this lifts "never hand-write a record the generator can produce". A lesson or convention takes the next unused number in its directory, chosen after rebasing onto fresh `origin/main`; no tool reserves it, so it can race |
| `generate` | `node tools/spec/generate-traceability.ts` and `node tools/spec/generate-spec-index.ts` |
| `generate --check` | the same two, each with `--check` |
| `check` | under `tools/spec/`: `check-records`, `check-glossary`, `check-area-paths`, `adr-numbers`, and `check-adr-immutability` (with `BASE_SHA=<base>` or `--staged`; bare, it compares nothing and passes); under `tools/gherkin/`: `lint-scenarios`; under `tools/docs/`: `check-frontmatter`, `check-links` |
| `coverage` | `tools/dev/ci-local.sh --body pr-body.md --job feature-coverage`, which runs `tools/spec/check-feature-coverage-diff.ts` and `tools/spec/check-ignored-claims.ts` as CI does |

- The generic "regenerate before every commit that touches the specification"
  is replaced by "Generated files" below.

### Layout

Everything the specification chain reads lives in the specification directory
(the specification-directory decision): per area, its `.feature` file and its
`README.md`; the five constraint pages listed in `tools/spec/spec-paths.ts`;
the decisions, lessons, and conventions; and the three generated files.

- A new path the tools read is added to `tools/spec/spec-paths.ts`, not written
  into a tool; `tests/js/spec/spec-paths.test.ts` ties the hooks and workflows to
  it.
- A file added under the old decisions, lessons, or features locations in
  `docs/` or at the root fails `check-frontmatter.ts`: rebase, then move it
  into the specification directory.
- After pulling this layout into an older clone: `rm .gitattributes && git
  checkout -- .gitattributes`, then `./init-dev.sh`, which installs the hooks
  and registers the `merge=ours` driver.

### Generated files

Never edited by hand:

- the generated claims file, the canonical data — every claim with its steps
  and the step-definition files that bind them, every constraint, and what
  each decision and lesson cites — conforming to its schema, and the
  traceability matrix, one row per claim: both
  `node tools/spec/generate-traceability.ts` (the claim-to-step-definition
  map and generated-claims decisions);
- the specification index, of every area, constraint page, decision, and
  lesson: `node tools/spec/generate-spec-index.ts`.

All three regenerate in post-merge and post-rewrite, in `traceability.yml` on
a same-repo pull request (also when only a step file changed), and in
`tools/dev/ci-local.sh`. The `docs` job fails any one stale. A stale
specification index in pre-commit: run `node tools/spec/generate-spec-index.ts`
and stage it. Run both generators before finishing a specification change.

- `node tools/spec/generate-traceability.ts` fails a built claim (not `@ignore`)
  with a step no step definition in its engine matches. The specification
  wins: fix the step definition, or the scenario only when it said the wrong
  thing. Stale `@ignore` claims, ambiguous steps, and unused step definitions
  are recorded in the generated claims file, not failed.
- The tool reads only the step-definition forms in use: `[Given(@"…")]` on one
  line in a `[Binding]` class scoped, if at all, by `[Scope(Feature = "…")]`;
  `Given("…")` or `Given(/…/)` from `createBdd()`; Cucumber parameters
  `{string}`, `{word}`, `{int}`, `{}`. Anything else fails it by name — teach
  the tool first.

### Identifiers

- A new claim's ID: `node tools/spec/claim-prefixes.ts --next <area>`, under
  the `prefix:` in the area's README; a split-off scenario takes one the same
  way.
- Retired prefixes live in `RETIRED_PREFIXES` in `tools/spec/claim-prefixes.ts`;
  an area README carries no `keeps:` frontmatter. The `REQ-QB` and `REQ-MOD`
  prefixes are retired: their claims keep their IDs in whichever area holds
  them, and nothing new takes them (the split-area-keeps-its-claim-IDs
  decision).

### Feature areas

- A scenario is never edited to match the code. Every example and fixture is
  synthetic: never real report content.
- Quote interface copy and page titles; a renamed step renames its step
  definition's text in the same commit (the glossary-words convention).
- `@ignore` and superseded scenarios: also
  [`test-hpac-safety`](../test-hpac-safety/SKILL.md) "Scenarios". A leading
  scenario is `@ignore @issue-<N>`, and `feature-coverage` fails one whose
  issue is closed — on every pull request, until it is fixed — or a pull
  request that closes it while it is still `@ignore`. Specification first is
  two issues: the spec pull request adds the `@ignore @issue-<N>` scenario and
  closes its own specification issue; the code pull request later builds it
  and closes N (the scenario-counts-only-in-its-own-area convention).
- A built claim fails the `coverage` job unless its scenario passed in its
  engine's run; the job summary lists every claim's result (the
  built-claim-counts-only-when-passed decision).
- `node tools/spec/check-glossary.ts` fails a banned synonym in the `docs`
  job, which `tools/dev/ci-local.sh` runs. No git hook runs it.
- `node tools/gherkin/lint-scenarios.ts` fails a status code, a storage
  identifier, a transport term ("the API" included), a reason, or a locale
  code in a step, and a scenario that breaks the one-behavior limits, in the
  `cucumber` job, which `tools/dev/ci-local.sh` runs. No git hook runs it (the
  behavior-not-implementation and one-behavior-per-scenario conventions).
- Splitting an area past about 800 lines: the procedure, and the next ID with
  `node tools/spec/claim-prefixes.ts --next <area>`, are in the area-split
  convention.
- `node tools/spec/check-area-paths.ts` fails the `docs` job on a new file
  under `src/` the area-paths map lacks; see "Coverage and exemptions".

### Decisions

- The rules: the accepted-ADR-is-immutable decision. Lifecycle, template,
  checks, and the missing numbers: the decisions README. A record has exactly
  the template's sections, in its order.
- Conventions: a new process, tooling, or agent-workflow rule is a numbered
  convention file, named with the next unused number and a kebab slug (see the
  conventions README). The process ADRs before the accepted-ADR-is-immutable
  decision stay among the decisions.
- The upstream `documentation-and-adrs` skill's ADR template and lifecycle do
  not apply here; this section and the accepted-ADR-is-immutable decision do.
- Three checks run in pre-commit and the `docs` job; what each fails is in the
  decisions README's "Checks":
  - `node tools/spec/adr-numbers.ts` — numbering and status agreement (the
    ADR-number-is-verified and specification-directory decisions);
  - `node tools/spec/check-records.ts` — the template and status line;
  - `node tools/spec/check-adr-immutability.ts` — a pull request changes an
    ADR on its base only in its status. CI passes `BASE_SHA`; pre-commit runs
    it with `--staged`.
- Superseding an older ADR: change its `status:` and its status line in the
  same pull request, and nothing else in it.
- The root README is [`README.md`](../../README.md).

### Lessons

- Lessons live in the lessons directory of the specification, with its README
  (the lesson-flows-upstream and immutable-ADR decisions).
- Frontmatter `kind:` is `product`, `process`, or `incident`;
  `node tools/spec/check-records.ts` checks what each kind owes, in pre-commit
  and `docs`. The rules: the table in the lessons README's "The shape".
- A product lesson's remedy is a claim and a scenario in the feature files.
- A process lesson updates the generic skill when its rule transfers to any
  project (upstream, in agent-team), and the project skill when the rule names
  this repository's tools or paths. `## Skill` names the one skill it changed,
  once.
- No index to update: the specification index lists the lesson from its frontmatter
  `title`, `description` (shown as "What it cost us"), `issue`, `date`,
  `status` (`accepted` or `superseded`), and `kind`, and its remedy from the
  claim IDs under `## Scenario` and the backticked skill names under
  `## Skill`.

### Coverage and exemptions

- Rules: `AGENTS.md` "The `feature-coverage` exemption" (the
  exemption-cites-the-claims-it-preserves decision).
- Relevance (the scenario-counts-only-in-its-own-area convention): the
  area-paths map maps every behavior-bearing path to its feature areas; a
  changed step-definition file takes the areas of the claims it binds. A
  changed scenario counts only in one of the changed files' areas, and so does
  each cited claim. A new file under `src/` joins the map in the same pull
  request; `node tools/spec/check-area-paths.ts` fails the `docs` job
  otherwise.
- A manifest-only diff passes with no citation: `DEPENDENCY_MANIFESTS` in
  `tools/spec/check-feature-coverage.ts` lists them.
- The closed category list is in `.github/pull_request_template.md`
  ("Specification delta"); a test keeps the template's list equal to the
  tool's (the lesson on a closed list kept where the author never looks).
- Run the check locally with the body: "Verify and publish" step 1 runs it;
  alone, `tools/dev/ci-local.sh --body pr-body.md --job feature-coverage`.
- Renovate writes its own `dependency` exemption for `src/web` bumps from
  `renovate.json` (the Renovate-cites-preserved-claims decision).

### Checks

- `node tools/docs/check-links.ts` fails a relative link or `#anchor` that does not
  resolve: pre-commit checks staged markdown, and the whole tree when a file is
  deleted or renamed; `docs` checks everything. Fix the link — never move a
  file without its references.
- Before finishing a specification change run both generators, then
  `check-glossary`, `lint-scenarios`, and `check-records`.

## Review

Extends the `review-work` skill. Never put report content,
answers, or credentials in a finding; cite a location, a count, or a shape.

### spec-reviewer

- A contradiction between a feature file and an ADR (the
  feature-files-must-not-contradict-ADRs decision).
- The exemption, `No .feature scenario needed:`, names the claims it preserves
  (the exemption-cites-the-claims-it-preserves decision).
- The generic skill's check, generated-file comparison, and coverage
  judgement are the tools in "Specification-driven development" "The
  generator"; run those first.
- The generated claims file, for the cited claims: each is
  bound by files the diff touches; the diff adds no entry to
  `unusedStepDefinitions` or `ambiguousSteps` and no new `"staleIgnore": true` (the claim-to-step-definition map decision).
- Words: `node tools/spec/check-glossary.ts` passes; a banned synonym is a
  specification delta, never a matter of taste (the glossary-words
  convention).
- Declarative steps: `node tools/gherkin/lint-scenarios.ts` passes, and a
  reworded step's definition still asserts what it asserted before (the
  behavior-not-implementation convention).
- One behavior: every assertion of a split scenario survives in exactly one
  scenario, the original ID stays with the main behavior, and no Then step
  definition performs an action the scenario claims (the
  one-behavior-per-scenario convention).
- Records: `node tools/spec/check-records.ts` and `node tools/spec/check-adr-immutability.ts`
  with the pull request's base as `BASE_SHA` both pass; see "Specification-driven development" "Decisions" (the
  immutable-ADR decision).

### critic

The rule and its bound: the plan-meets-the-critic convention.
A `PreToolUse` hook (`tools/github/remind-critic.ts`, wired in
`.claude/settings.json`) reminds on `ExitPlanMode` and on `gh issue create`; it
never blocks.

- A plan that changes behavior without a scenario first conflicts with the specification-driven-development decision.
  A plan that adds a user table, allowlist, credential proxy, or outbound email
  conflicts with `AGENTS.md` "Not built".
- Read the area README's out-of-scope section, the `AGENTS.md` product
  invariants, the ADRs (listed in the specification index), and the
  lessons (in the lessons directory of the specification).

### adversary

Runs by convention only (the plan-meets-the-critic convention).

- Contract boundaries here: the API's request and response DTOs, the public
  views, the Worker's model input and output, the migrations and SQL views, and
  the pre-signed URL and quarantine flow.
- Privacy and security are this role's, not the spec-reviewer's. Boundaries to
  attack: report content or credentials in logs; a public DTO grown a field; a
  private-only fact reaching a summary; a document reaching the model; an
  attachment published without media consent; a receipt or token stored in the
  clear; a physical deletion.
- Missing tests: a privacy-sensitive surface
  ([`hpac-safety-conventions`](../hpac-safety-conventions/SKILL.md) "Privacy")
  with no focused boundary test.
- Test commands it may run: a filtered `dotnet test`, `CI=1 npm test` for e2e;
  never against production data.

### auditor

Runs on demand, never per change (the plan-meets-the-critic-and-change-meets-the-adversary convention). Read-only checks to cite, from
`tools/spec/`, `tools/docs/`, and `tools/gherkin/`: `check-records`,
`check-glossary`, `check-ignored-claims`, `check-area-paths`,
`lint-scenarios`, `check-links`,
`check-frontmatter`, `check-generic-instructions`, `check-inventories`, and
the generators with `--check` (`generate-traceability.ts --check`,
`generate-spec-index.ts --check`), which fail on a stale committed file and
write nothing. Hold the code to the `AGENTS.md` product invariants and "Not
built", and the area READMEs' out-of-scope sections.

## Verify and publish

1. Run the tests for the code you changed, natively, then the gate:
   `tools/dev/ci-local.sh --body pr-body.md`, for every pull request (the
   checks-run-locally-under-act decision, and the lesson that a local gate
   re-implementing CI disagreed with it).
   - Native tests first: a filtered `dotnet test` (for example
     `dotnet test <project> --filter <name>`) for the changed .NET code, and
     `CI=1 npm test` in `tests/e2e` for each touched e2e spec. Not the whole
     suite.
   - The claim gate runs in `coverage`, so the fast default skips
     it: `--job coverage` runs it with `test` and `e2e`, or judge your native
     runs as [`test-hpac-safety`](../test-hpac-safety/SKILL.md) "`@ignore`"
     shows.
   - The gate runs the workflow files themselves under act (version in
     `.act-version`). By default only the fast checks: `linked-issue.yml`,
     `feature-coverage.yml`, and the `ci.yml` jobs `build`, `lint`, `web`, `i18n`,
     `docs`, `cucumber`, and `agent-config`. It skips `test`, `coverage`,
     `e2e`, and terraform `infra`, and needs no `gh` login. It stops at the
     first failure.
   - GitHub CI is the full gate, the coverage ratchet included. `--full` runs
     every job locally as the gate once did, coverage against main's last
     green artifact included; do it only when the full result is worth the
     wait, such as chasing a coverage failure.
   - It runs committed `HEAD`: commit first. It refuses a dirty tree or a
     `HEAD` without a fresh `origin/main`.
   - No lock: runs from different worktrees proceed in parallel, even at the
     same time. Each gets its own throwaway clone, its own free port for the
     browser suite, and its own act job container names, so one run can't
     wait behind, or clobber, another.
   - Each run is its own Docker group, `hpac-ci-<issue>-<run>`, with its own
     network. Teardown, "try: work; finally: tear down": its containers,
     volumes, and network are deleted when it ends, pass or fail, `INT` and
     `TERM` included; a `kill -9` is swept by the next run. Never touch a dev
     stack.
   - No token: act gets none. For `--full` or `--job coverage` the script
     downloads main's coverage baseline on the host with the `gh` login;
     without one, such a run exits 2 ("run gh auth login").
   - The bots' commits: it regenerates and commits the traceability matrix in
     its own clone, and under act `i18n` accepts French still pending as a `#`
     stub. A scenario change or a new English key passes locally as it will
     after the bots run; never hand-write French to pass it.
   - One job: `--job <id>`, repeatable (a body edit: `--job linked-issue
     --job feature-coverage`). It is exclusive with `--full`.
   - Exit 0 passed, 1 a job failed (or coverage lost a per-project report),
     2 a precondition or setup step failed. Full logs: `artifacts/ci-local/`.
   - Local green is necessary, not sufficient; step 9 still applies.
4. Relabel: `tools/dev/session-label.sh "#<number> · PR #<pr> <short-description>"`.
   **Enable auto-merge: `gh pr merge <pr> --auto`**, with no `--squash` (the
   queue sets the method and refuses the flag) and never `--admin`. Never
   merge directly or run `enqueuePullRequest` or `mergePullRequest` — the
   owner does that by hand (the merge-queue decision, second amendment; a
   repository-tracked `PreToolUse` hook,
   `tools/github/guard-pr-merge.ts`, refuses it too). Get the pull request's own
   required checks green. `main` has a merge queue: with auto-merge on, the
   pull request queues once its required checks pass, and the queue
   squash-merges it and deletes the branch. The
   squash message is the pull request body, so the body is final once queued.
6. Screenshots, for a user-visible `src/web` change (an `after` shot alone
   satisfies it; `before` is optional):
   - browser tools: Playwright or Claude in Chrome;
   - set the locale to English first — a French shot reads as broken;
   - commit under `docs/screenshots/<short-description>/`; attach with
     `gh pr create` / `gh pr comment --attach`;
   - URL:
     `https://raw.githubusercontent.com/HPAC-Safety/safety-report/<sha>/docs/screenshots/<dir>/<file>.png`
     (the lesson that a screenshot linked by a page URL renders broken).
   - OS-level capture on macOS, against a headed browser, for a native
     `<datalist>`, `<select>` popup, or date input: `screencapture -iw <file>`
     and click the window, or `screencapture -l <windowid> <file>` with an id
     this lists beside each window's app:

     ```sh
     swift -e 'import CoreGraphics; for w in CGWindowListCopyWindowInfo(.optionOnScreenOnly, kCGNullWindowID) as! [[String: Any]] where w["kCGWindowLayer"] as? Int == 0 { print(w["kCGWindowNumber"]!, w["kCGWindowOwnerName"]!) }'
     ```
   - The body's `## Screenshots` section in
     `.github/pull_request_template.md` holds the links, or the line
     `No screenshot needed: <reason>` for a `src/web` change with nothing
     visible.
   - The `screenshots` job in `linked-issue.yml` runs
     `tools/web/check-pr-screenshots.ts`: it fails a change to a `.tsx` or `.css` under
     `src/web/src/` (not a test) whose body has neither a pinned
     `raw.githubusercontent.com/…/docs/screenshots/…` image nor that line. It
     does not judge the shots; review does (the web-UI-screenshots decision,
     and the lesson on a rule the template never asks for).
     Step 1 runs it; alone,
     `tools/dev/ci-local.sh --body pr-body.md --job screenshots`.
7. `./dev-up.sh` from the worktree. It takes the dev ports from any other
   checkout, starts containers detached, waits until the API and dev server
   answer, prints their URLs, and returns.
8. `./dev-up.sh --down`, then `git worktree remove` (the lesson that containers outlive the worktree that
   started them).
9. Why green is not enough: the lesson that a branch rebased before its push is
   behind by the time it is green.
   With the merge queue, `BEHIND` alone needs no rebase.
   - Queue state: `gh pr view <pr> --json state,mergeStateStatus,autoMergeRequest`,
     or `https://github.com/HPAC-Safety/safety-report/queue/main`.
   - Removed from the queue: the merge group's failing check is on the
     `gh-readonly-queue/main/*` run, linked from the pull request's timeline.
     The usual cause is a collision with a pull request that merged first:
     `docs` (a duplicate ADR, lesson, or `REQ` number, or a stale matrix),
     `feature-coverage` (an exemption citing a claim that is gone), or
     `coverage`.
   - Two queued pull requests that both change the specification: the second
     is usually ejected, because its traceability matrix is stale on the
     merged tree. That is expected. Rebase onto `main` and push;
     `traceability.yml` regenerates the matrix, and auto-merge queues it again.
   Finish with `tools/dev/session-label.sh "✓ #<number> · PR #<pr> green"`.

## Path filters

- The web bundle loads `locales/` from the repository root, so the `web` and
  `e2e` jobs in `ci.yml` both list it.
- A change to `locales/` runs the browser suite before the pull request,
  because a step may match the copy you changed: step 1 runs `e2e`, whose
  filter lists `locales/` (the lesson on a copy change that ran no browser
  test).

## Workflow steps

- The script goes under `tools/<group>/` with its test under
  `tests/js/<group>/`. `tools/github/check-workflow-steps.ts` fails a `run:`
  that is more than one command in pre-commit and `docs`. Add the script to the
  workflow's path filter (the one-command-workflow-step decision).

## Workflows that push

- **Onto a pull request's branch**: push through `tools/github/push-to-pr-branch.ts`,
  passing the workflow's own `pull_request_target.paths`
  (the bot-push-replays decision, and the lesson that a push filtered by paths
  starts no run to supersede yours).
- **With a token on the remote URL**: the lesson that a persisted checkout
  token outranks the PAT on the remote.

## Required checks

- Why a check nobody requires holds nothing back: the lesson that a check
  nobody required let a broken bump merge.

## Concurrency and quotas

- `terraform.yml` serialises `apply` alone, in the job-level group
  `terraform-apply` (the terraform-apply-concurrency-group decision, and the
  lesson that a run waiting on reviewers held every later run).
- `skillfile` reads `GITHUB_TOKEN`, then `GH_TOKEN`. `agent-config` passes it
  `github.token`.
