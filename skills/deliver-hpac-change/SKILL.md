---
name: deliver-hpac-change
description: HPAC Safety's tools, commands, labels, and paths for delivering a change — extends the generic deliver-change skill. Use when creating or editing issues, docs, worktrees, PRs, or checks in this repository.
---

# Deliver an HPAC Safety change

Extends [`deliver-change`](../deliver-change/SKILL.md); read that first. This
skill holds only what is specific to this repository, under the same section
names and step numbers.

## Start

### Settle the requirement before building

- Asking: put the question to the owner directly; for genuinely ambiguous
  product behavior also read
  [`clarify-requirements`](../clarify-requirements/SKILL.md). Record the
  answer in the issue, and in the specification where it changes behavior,
  before any code
  ([ADR-0083](../../.spec/decisions/ADR-0083-specification-driven-development.md)).
- Sub-agent briefs: end with "if anything is ambiguous, stop and report the
  question; do not guess".
- Sequencing: two issues that edit the same EF migration, SQL view under
  `Persistence/Sql/`, or table run in filing order: the second is filed as
  blocked by the first, and is not picked up until the first has merged. Migration timestamps
  and view definitions do not survive being rebased across each other.

### File a new issue

- **Labels**:
  - one type — `enhancement`, `bug`, `documentation`, or `tech-debt`;
  - every `area:*` the change touches;
  - the `phase:*` matching a phase milestone.
- Relationship query owner and name: `owner:"HPAC-Safety",name:"safety-report"`.

### Worktree and branch

- Session label: `tools/dev/session-label.sh "#<number> <short-description>"`
  (and the later relabels in "Verify and publish").
- Why the first-edit check exists:
  [lesson 0004](../../.spec/lessons/0004-a-rule-read-once-is-not-a-rule-checked-again.md).
- The shared-stash rule (#796) is written, not enforced: git has no pre-stash
  hook, and the owner chose not to add an agent tool hook for it.
- Git hooks: `./init-dev.sh` installs `tools/dev/git-hook-shim.sh` under each
  name, and it runs the tracked `.githooks/<name>` of the current worktree. Edit
  `.githooks/<name>`, never the installed file; a moved tool needs no re-install
  ([ADR-0189](../../.spec/decisions/ADR-0189-a-workflow-step-runs-one-command-and-tools-is-grouped-by-domain.md)
  amendment).
- `post-merge` and `post-rewrite` regenerate and stage `.spec/claims.json`,
  `.spec/traceability.md`, and `.spec/README.md`, then merge the specification
  into the local graphify graph. On `main` they only merge the graph, which is
  untracked: the primary checkout only fast-forwards to `origin/main`, which
  already carries the generated files, so a pull leaves `main` clean (#802).

### Commit, rebase, claim identifiers

- Why identifiers are claimed after the rebase:
  [lesson 0003](../../.spec/lessons/0003-a-number-is-claimed-the-moment-someone-else-merges.md).
- ADR number: `node tools/spec/adr-numbers.ts --next`.
- Lost the race? `node tools/spec/adr-numbers.ts --renumber <old> <new>` moves the
  file and rewrites every reference.
- Two records already share the number? Add `--file <name>` to say which
  moves. Bare `ADR-NNNN` mentions it leaves alone are ambiguous; resolve them
  by hand.

### Before editing

- The specification is `.spec/features`.

## Document

### Specification directory

Everything the specification chain reads lives in `.spec/`
([ADR-0183](../../.spec/decisions/ADR-0183-the-specification-lives-in-a-spec-directory.md)):

- `.spec/features/<area>/` — the area's `.feature` file and its `README.md`;
- the five constraint pages, `.spec/*.md`, listed in `tools/spec/spec-paths.ts`;
- `.spec/decisions/`, `.spec/lessons/`, and `.spec/conventions/`;
- three generated files, never edited by hand:
  - `.spec/claims.json`, the canonical data — every claim with its steps and
    the step-definition files that bind them, every constraint, and what each
    ADR and lesson cites — conforming to `.spec/claims.schema.json`, and
    `.spec/traceability.md`, one row per claim — both
    `node tools/spec/generate-traceability.ts`
    ([ADR-0184](../../.spec/decisions/ADR-0184-a-generated-map-binds-every-claim-to-its-step-definitions.md),
    [ADR-0193](../../.spec/decisions/ADR-0193-the-claims-are-generated-as-json-a-graph-fragment-and-one-slim-matrix.md));
  - `.spec/README.md`, the index of every area, constraint page, decision, and
    lesson — `node tools/spec/generate-spec-index.ts`.

Rules:

- A page goes in `.spec/` when the chain reads it — scenarios, `CON-*` IDs, a
  decision, a lesson, a convention. A page that explains how goes in `docs/`.
- A new path the tools read is added to `tools/spec/spec-paths.ts`, not written
  into a tool; `tests/js/spec/spec-paths.test.ts` ties the hooks and workflows to
  it.
- All three regenerate in post-merge and post-rewrite, in `traceability.yml`
  on a same-repo pull request (also when only a step file changed), and in
  `tools/dev/ci-local.sh`. The `docs` job fails any one stale. A stale
  `.spec/README.md` in pre-commit: run `node tools/spec/generate-spec-index.ts` and stage it.
- `node tools/spec/generate-traceability.ts` fails a built claim (not `@ignore`) with a step no
  step definition in its engine matches. The specification wins: fix the step
  definition, or the scenario only when it said the wrong thing. Stale
  `@ignore` claims, ambiguous steps, and unused step definitions are recorded
  in `.spec/claims.json`, not failed.
- The tool reads only the step-definition forms in use: `[Given(@"…")]` on one
  line in a `[Binding]` class scoped, if at all, by `[Scope(Feature = "…")]`;
  `Given("…")` or `Given(/…/)` from `createBdd()`; Cucumber parameters
  `{string}`, `{word}`, `{int}`, `{}`. Anything else fails it by name — teach
  the tool first.
- `node tools/docs/check-links.ts` fails a relative link or `#anchor` that does not
  resolve: pre-commit checks staged markdown, and the whole tree when a file is
  deleted or renamed; `docs` checks everything. Fix the link — never move a
  file without its references.
- A file added under the old `docs/decisions/`, `docs/lessons/`, or `features/`
  fails `check-frontmatter.ts`: rebase, then move it under `.spec/`.
- After pulling this layout into an older clone: `rm .gitattributes && git
  checkout -- .gitattributes`, then `./init-dev.sh`, which installs the hooks
  and registers the `merge=ours` driver.

### Scenarios

- Specification-driven development:
  [ADR-0083](../../.spec/decisions/ADR-0083-specification-driven-development.md).
- `@ignore` and superseded scenarios: also
  [`test-hpac-safety`](../test-hpac-safety/SKILL.md) "Scenarios". A leading
  scenario is `@ignore @issue-<N>`, and `feature-coverage` fails one whose
  issue is closed, or a pull request that closes it while it is still
  `@ignore`
  ([CONV-001](../../.spec/conventions/CONV-001-a-scenario-counts-only-in-its-own-area-and-an-ignored-one-names-its-issue.md)).
- A built claim fails the `coverage` job unless its scenario passed in its
  engine's run; the job summary lists every claim's result
  ([ADR-0195](../../.spec/decisions/ADR-0195-a-built-claim-counts-only-when-its-scenario-passed-in-the-run.md)).
- Each `.spec/features/<area>/README.md` records what **not** to build.
- An area past about 800 lines is split, not grouped with `Rule:` blocks, and
  its scenarios keep their IDs: the procedure, and the next ID with
  `node tools/spec/claim-prefixes.ts --next <area>`, are
  [CONV-002](../../.spec/conventions/CONV-002-an-area-past-800-lines-is-split-and-its-scenarios-keep-their-ids.md).

### The `feature-coverage` exemption

- Rules: `AGENTS.md` "The `feature-coverage` exemption"
  ([ADR-0090](../../.spec/decisions/ADR-0090-an-exemption-cites-the-claims-it-preserves.md)).
- Relevance
  ([CONV-001](../../.spec/conventions/CONV-001-a-scenario-counts-only-in-its-own-area-and-an-ignored-one-names-its-issue.md)):
  [`.spec/area-paths.json`](../../.spec/area-paths.json) maps every
  behavior-bearing path to its feature areas; a step definition takes the
  areas of the claims it binds. A changed scenario counts only in one of the
  changed files' areas, and so does each cited claim. A new file under `src/`
  joins the map in the same pull request; `node tools/spec/check-area-paths.ts`
  fails the `docs` job otherwise.
- The closed category list is in `.github/pull_request_template.md`
  ("Specification delta"); a test keeps the template's list equal to the
  tool's ([lesson 0022](../../.spec/lessons/0022-a-closed-list-kept-where-the-author-never-looks.md)).
- Run the check locally with the body: "Verify and publish" step 1 runs it;
  alone, `tools/dev/ci-local.sh --body pr-body.md --job feature-coverage`.
- Renovate writes its own `dependency` exemption for `src/web` bumps from
  `renovate.json`
  ([ADR-0111](../../.spec/decisions/ADR-0111-renovate-cites-the-claims-a-web-dependency-bump-preserves.md)).

### Inventories

- A new directory under `src/` gets a row in
  [`docs/source-inventory.md`](../../docs/source-inventory.md), and a removed
  one loses its row. `node tools/docs/check-inventories.ts` fails the pre-commit
  hook and the `docs` job otherwise.
- [`docs/issue-traceability.md`](../../docs/issue-traceability.md) is
  generated from GitHub; never edit it by hand, and a pull request that
  closes an issue leaves it alone. Drift never fails a pull request; it keeps
  an "Issue traceability drift" issue open, and whoever resolves that issue
  runs `node tools/spec/generate-issue-traceability.ts` in a pull request that
  closes it
  ([ADR-0143](../../.spec/decisions/ADR-0143-issue-traceability-drift-opens-an-issue-and-gates-nothing.md),
  [ADR-0191](../../.spec/decisions/ADR-0191-each-rule-is-stated-once-and-no-status-page-is-written-by-hand.md)).

### Lessons

- Lessons live under [`.spec/lessons/`](../../.spec/lessons/README.md)
  ([ADR-0085](../../.spec/decisions/ADR-0085-a-lesson-flows-upstream-into-the-specification.md),
  [ADR-0192](../../.spec/decisions/ADR-0192-an-accepted-adr-is-immutable-and-process-rules-are-conventions.md)).
- Frontmatter `kind:` is `product`, `process`, or `incident`;
  `node tools/spec/check-records.ts` checks what each kind owes, in pre-commit
  and `docs`. The rules: the table in
  [`.spec/lessons/README.md`](../../.spec/lessons/README.md#the-shape).
- A process lesson updates the generic skill when its rule transfers to any
  project, and this project's companion skill when the rule names this
  repository's tools or paths. The lesson's `## Skill` section names the skill
  it changed — once; no footer naming the generic skill too.
- A product lesson's remedy is a claim and a scenario in `.spec/features`.
- No index to update: `.spec/README.md` lists the lesson from its frontmatter
  `title`, `description` (shown as "What it cost us"), `issue`, `date`,
  `status` (`accepted` or `superseded`), and `kind`, and its remedy from the
  claim IDs under `## Scenario` and the backticked skill names under
  `## Skill`.

### ADRs

- The rules: [ADR-0192](../../.spec/decisions/ADR-0192-an-accepted-adr-is-immutable-and-process-rules-are-conventions.md). Lifecycle, template, checks, and the
  missing numbers: [`.spec/decisions/README.md`](../../.spec/decisions/README.md).
- Template: copy [`.spec/decisions/TEMPLATE.md`](../../.spec/decisions/TEMPLATE.md).
  From ADR-0192 on, a record has exactly its sections, in its order.
- Conventions: a new process, tooling, or agent-workflow rule is
  `.spec/conventions/CONV-NNN-kebab-slug.md`
  ([`.spec/conventions/README.md`](../../.spec/conventions/README.md)). Take
  the next unused number; it is never reused. The process ADRs before ADR-0192
  stay in `.spec/decisions/`.
- The upstream `documentation-and-adrs` skill's ADR template and lifecycle do
  not apply here; this section and ADR-0192 do.
- Three checks run in pre-commit and the `docs` job; what each fails is in
  [`.spec/decisions/README.md`](../../.spec/decisions/README.md#checks):
  - `node tools/spec/adr-numbers.ts` — numbering and status agreement
    ([ADR-0091](../../.spec/decisions/ADR-0091-an-adr-number-is-verified-not-assumed.md),
    [ADR-0183](../../.spec/decisions/ADR-0183-the-specification-lives-in-a-spec-directory.md));
  - `node tools/spec/check-records.ts` — the template and status line;
  - `node tools/spec/check-adr-immutability.ts` — a pull request changes an
    ADR on its base only in its status. CI passes `BASE_SHA`; pre-commit runs
    it with `--staged`.
- Superseding an older ADR: change its `status:` and its status line in the
  same pull request, and nothing else in it.
- The root README is [`README.md`](../../README.md).

### Markdown

- Every tracked markdown file opens with frontmatter: `title`, `description`,
  and `type` — one of `adr`, `spec`, `guide`, `readme`, `lesson`,
  `convention`, `instructions`, `template` — plus the keys that type adds
  ([ADR-0087](../../.spec/decisions/ADR-0087-every-markdown-file-declares-itself.md)):
  - `adr`: `status`, `date`, `decision-makers`, `keywords`;
  - `lesson`: `date`, `issue`, `status`, `kind` ([ADR-0192](../../.spec/decisions/ADR-0192-an-accepted-adr-is-immutable-and-process-rules-are-conventions.md));
  - `convention`: `status` (`accepted` or `superseded`) and `date`;
  - `spec`: `area`.
- A `skills/*/SKILL.md` carries exactly `name` and `description` instead; its
  type comes from its path.
- An `agents/*.md` carries `name`, `description`, `model`, and `effort`, and
  may carry the other keys Claude Code reads on an agent: `tools`,
  `disallowedTools`, `permissionMode`, `maxTurns`, `skills`, `memory`,
  `isolation`, `background`. Nothing else
  ([ADR-0182](../../.spec/decisions/ADR-0182-a-role-agent-declares-its-model-and-effort.md)).
- The Worker's runtime prompts are exempt; their bytes are the model payload.
- `node tools/docs/check-frontmatter.ts` is the authority; the pre-commit hook runs
  it over staged markdown.
- Never include real report content.

### Agent instructions

- The `ai-author` role is [`agents/ai-author.md`](../../agents/ai-author.md);
  this repository's rules for it are in
  [`hpac-role-agents`](../hpac-role-agents/SKILL.md).
- Manifest and lock: update `Skillfile`, regenerate `Skillfile.lock`, and run
  `skillfile validate` and `skillfile install`. Generated copies live under
  `.claude/`.

## Verify and publish

1. Run the tests for the code you changed, natively, then the gate:
   `tools/dev/ci-local.sh --body pr-body.md`, for every pull request
   ([ADR-0145](../../.spec/decisions/ADR-0145-a-pull-requests-checks-run-locally-under-act.md),
   [lesson 0025](../../.spec/lessons/0025-a-local-gate-that-re-implemented-ci-disagreed-with-it.md)).
   - Native tests first: a filtered `dotnet test` (for example
     `dotnet test <project> --filter <name>`) for the changed .NET code, and
     `CI=1 npm test` in `tests/e2e` for each touched e2e spec. Not the whole
     suite.
   - The claim gate (ADR-0195) runs in `coverage`, so the fast default skips
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
   owner does that by hand
   ([ADR-0147](../../.spec/decisions/ADR-0147-pull-requests-merge-through-a-merge-queue.md)
   second amendment; a repository-tracked `PreToolUse` hook,
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
     ([lesson 0017](../../.spec/lessons/0017-a-screenshot-linked-by-a-page-url-renders-broken.md)).
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
     does not judge the shots; review does
     ([ADR-0142](../../.spec/decisions/ADR-0142-a-web-ui-pull-request-shows-its-screenshots.md),
     [lesson 0023](../../.spec/lessons/0023-a-rule-the-template-never-asks-for.md)).
     Step 1 runs it; alone,
     `tools/dev/ci-local.sh --body pr-body.md --job screenshots`.
7. `./dev-up.sh` from the worktree. It takes the dev ports from any other
   checkout, starts containers detached, waits until the API and dev server
   answer, prints their URLs, and returns.
8. `./dev-up.sh --down`, then `git worktree remove`
   ([lesson 0008](../../.spec/lessons/0008-containers-outlive-the-worktree-that-started-them.md)).
9. Why green is not enough:
   [lesson 0011](../../.spec/lessons/0011-a-branch-rebased-before-its-push-is-behind-by-the-time-it-is-green.md).
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
     is usually ejected, because its `.spec/traceability.md` is stale on the
     merged tree. That is expected. Rebase onto `main` and push;
     `traceability.yml` regenerates the matrix, and auto-merge queues it again.
   Finish with `tools/dev/session-label.sh "✓ #<number> · PR #<pr> green"`.

## Path filters

- The web bundle loads `locales/` from the repository root, so the `web` and
  `e2e` jobs in `ci.yml` both list it.
- A change to `locales/` runs the browser suite before the pull request,
  because a step may match the copy you changed: step 1 runs `e2e`, whose
  filter lists `locales/`
  ([lesson 0020](../../.spec/lessons/0020-a-copy-change-that-ran-no-browser-test.md)).

## Workflow steps

- A `run:` is one command; logic goes in a script under `tools/<group>/` with
  its test under `tests/js/<group>/`, and plain sequences become separate steps.
  `tools/github/check-workflow-steps.ts` fails anything else in pre-commit and
  `docs`. Add the script to the workflow's path filter
  ([ADR-0189](../../.spec/decisions/ADR-0189-a-workflow-step-runs-one-command-and-tools-is-grouped-by-domain.md)).

## Workflows that push

- **Onto a pull request's branch**: push through `tools/github/push-to-pr-branch.ts`,
  passing the workflow's own `pull_request_target.paths`
  ([ADR-0113](../../.spec/decisions/ADR-0113-a-bot-pushing-onto-a-pull-request-replays-past-another-bot.md),
  [lesson 0016](../../.spec/lessons/0016-a-push-filtered-by-paths-starts-no-run-to-supersede-yours.md)).
- **With a token on the remote URL**:
  [lesson 0018](../../.spec/lessons/0018-a-persisted-checkout-token-outranks-the-pat-on-the-remote.md).

## Required checks

- A new CI job that can fail `main` joins the main ruleset's required status
  checks in the pull request that adds it, and its workflow triggers on
  `merge_group`. A check nobody requires holds nothing back
  ([lesson 0042](../../.spec/lessons/0042-a-check-nobody-required-let-a-broken-bump-merge.md)).

## Concurrency and quotas

- `terraform.yml` serialises `apply` alone, in the job-level group
  `terraform-apply`
  ([ADR-0148](../../.spec/decisions/ADR-0148-a-terraform-apply-waits-in-its-own-concurrency-group.md),
  [lesson 0026](../../.spec/lessons/0026-a-run-waiting-on-reviewers-held-every-later-run.md)).
- `skillfile` reads `GITHUB_TOKEN`, then `GH_TOKEN`. `agent-config` passes it
  `github.token`.
