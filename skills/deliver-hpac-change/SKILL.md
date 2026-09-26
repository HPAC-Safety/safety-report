---
name: deliver-hpac-change
description: HPAC Safety's tools, commands, labels, and paths for delivering a change — extends the generic deliver-change skill. Use when creating or editing issues, docs, worktrees, PRs, or checks in this repository.
---

# Deliver an HPAC Safety change

Extends [`deliver-change`](../deliver-change/SKILL.md); read that first. This
skill holds only what is specific to this repository, under the same section
names and step numbers.

## Start

### File a new issue

- **Labels**:
  - one type — `enhancement`, `bug`, `documentation`, or `tech-debt`;
  - every `area:*` the change touches;
  - the `phase:*` matching a phase milestone.
- Relationship query owner and name: `owner:"HPAC-Safety",name:"safety-report"`.

### Worktree and branch

- Session label: `tools/session-label.sh "#<number> <short-description>"`
  (and the later relabels in "Verify and publish").
- Why the first-edit check exists:
  [lesson 0004](../../docs/lessons/0004-a-rule-read-once-is-not-a-rule-checked-again.md).

### Commit, rebase, claim identifiers

- Why identifiers are claimed after the rebase:
  [lesson 0003](../../docs/lessons/0003-a-number-is-claimed-the-moment-someone-else-merges.md).
- ADR number: `node tools/adr-numbers.mjs --next`.
- Lost the race? `node tools/adr-numbers.mjs --renumber <old> <new>` moves the
  file and rewrites every reference.
- Two records already share the number? Add `--file <name>` to say which
  moves. Bare `ADR-NNNN` mentions it leaves alone are ambiguous; resolve them
  by hand.

### Before editing

- The specification is `/features`.

## Document

### Scenarios

- Specification-driven development:
  [ADR-0083](../../docs/decisions/ADR-0083-specification-driven-development.md).
- `@ignore` and superseded scenarios: also
  [`test-hpac-safety`](../test-hpac-safety/SKILL.md) "Scenarios".
- Each `features/<area>/README.md` records what **not** to build.

### The `feature-coverage` exemption

- Rules: `AGENTS.md` "The `feature-coverage` exemption"
  ([ADR-0090](../../docs/decisions/ADR-0090-an-exemption-cites-the-claims-it-preserves.md)).
- The closed category list is in `.github/pull_request_template.md`
  ("Specification delta"); a test keeps the template's list equal to the
  tool's ([lesson 0022](../../docs/lessons/0022-a-closed-list-kept-where-the-author-never-looks.md)).
- Run the check locally with the body: "Verify and publish" step 1 runs it;
  alone, `tools/ci-local.sh --body pr-body.md --job feature-coverage`.
- Renovate writes its own `dependency` exemption for `src/web` bumps from
  `renovate.json`
  ([ADR-0111](../../docs/decisions/ADR-0111-renovate-cites-the-claims-a-web-dependency-bump-preserves.md)).

### Inventories

- A new directory under `src/` gets a row in
  [`docs/source-inventory.md`](../../docs/source-inventory.md), and a removed
  one loses its row. `node tools/check-inventories.mjs` fails the pre-commit
  hook and the `docs` job otherwise.
- A pull request that closes an issue removes its row from
  [`docs/issue-traceability.md`](../../docs/issue-traceability.md). Filing an
  issue needs no pull request. Drift never fails a pull request; it keeps an
  "Issue traceability drift" issue open, and whoever resolves that issue adds
  the missing rows
  ([ADR-0143](../../docs/decisions/ADR-0143-issue-traceability-drift-opens-an-issue-and-gates-nothing.md)).

### Lessons

- Lessons live under [`docs/lessons/`](../../docs/lessons/README.md)
  ([ADR-0085](../../docs/decisions/ADR-0085-a-lesson-flows-upstream-into-the-specification.md)).
- A process lesson updates the generic skill when its rule transfers to any
  project, and this project's companion skill when the rule names this
  repository's tools or paths. The lesson's `## Skill` section names the skill
  it changed.
- A product lesson's remedy is a claim and a scenario in `/features`.

### ADRs

- `node tools/adr-numbers.mjs` fails a duplicate number or a filename and
  heading that disagree, in the pre-commit hook and CI
  ([ADR-0091](../../docs/decisions/ADR-0091-an-adr-number-is-verified-not-assumed.md)).
- The root README is [`README.md`](../../README.md).

### Markdown

- Every tracked markdown file opens with frontmatter: `title`, `description`,
  and `type` — one of `adr`, `spec`, `guide`, `readme`, `lesson`,
  `instructions`, `template` — plus the keys that type adds
  ([ADR-0087](../../docs/decisions/ADR-0087-every-markdown-file-declares-itself.md)).
- A `skills/*/SKILL.md` or `agents/*.md` carries exactly `name` and
  `description` instead; its type comes from its path.
- The Worker's runtime prompts are exempt; their bytes are the model payload.
- `node tools/check-frontmatter.mjs` is the authority; the pre-commit hook runs
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

1. The gate is `tools/ci-local.sh --body pr-body.md`, for every pull request
   ([ADR-0145](../../docs/decisions/ADR-0145-a-pull-requests-checks-run-locally-under-act.md),
   [lesson 0025](../../docs/lessons/0025-a-local-gate-that-re-implemented-ci-disagreed-with-it.md)).
   - It runs the workflow files themselves under act (version in
     `.act-version`): `linked-issue.yml`, `feature-coverage.yml`, terraform
     `infra`, and every `ci.yml` job, coverage against main's last green
     artifact included. It stops at the first failure.
   - It runs committed `HEAD`: commit first. It refuses a dirty tree or a
     `HEAD` without a fresh `origin/main`, and waits while another run holds
     the machine's lock.
   - Token: `HPAC_ACT_TOKEN`, a fine-grained read-only token for this
     repository (Actions, Contents, Metadata: read). Without it the script
     uses `gh auth token` and warns.
   - One job: `--job <id>`, repeatable (a body edit: `--job linked-issue
     --job feature-coverage`).
   - Exit 0 passed, 1 a job failed, 2 a precondition failed, 3 the lock timed
     out. Full logs: `artifacts/ci-local/`.
   - Local green is necessary, not sufficient; step 9 still applies.
4. Relabel: `tools/session-label.sh "#<number> · PR #<pr> <short-description>"`.
   The repository squash-merges and deletes the branch once required checks
   pass.
6. Screenshots, for a user-visible `src/web` change:
   - browser tools: Playwright or Claude in Chrome;
   - set the locale to English first — a French shot reads as broken;
   - commit under `docs/screenshots/<short-description>/`; attach with
     `gh pr create` / `gh pr comment --attach`;
   - URL:
     `https://raw.githubusercontent.com/HPAC-Safety/safety-report/<sha>/docs/screenshots/<dir>/<file>.png`
     ([lesson 0017](../../docs/lessons/0017-a-screenshot-linked-by-a-page-url-renders-broken.md)).
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
     `tools/pr-screenshots.mjs`: it fails a change to a `.tsx` or `.css` under
     `src/web/src/` (not a test) whose body has neither a pinned
     `raw.githubusercontent.com/…/docs/screenshots/…` image nor that line. It
     does not judge whether the pair is complete; review does
     ([ADR-0142](../../docs/decisions/ADR-0142-a-web-ui-pull-request-shows-its-screenshots.md),
     [lesson 0023](../../docs/lessons/0023-a-rule-the-template-never-asks-for.md)).
     Step 1 runs it; alone,
     `tools/ci-local.sh --body pr-body.md --job screenshots`.
7. `./dev-up.sh` from the worktree. It takes the dev ports from any other
   checkout, starts containers detached, waits until the API and dev server
   answer, prints their URLs, and returns.
8. `./dev-up.sh --down`, then `git worktree remove`
   ([lesson 0008](../../docs/lessons/0008-containers-outlive-the-worktree-that-started-them.md)).
9. Why green is not enough:
   [lesson 0011](../../docs/lessons/0011-a-branch-rebased-before-its-push-is-behind-by-the-time-it-is-green.md).
   Finish with `tools/session-label.sh "✓ #<number> · PR #<pr> green"`.

## Path filters

- The web bundle loads `locales/` from the repository root, so the `web` and
  `e2e` jobs in `ci.yml` both list it.
- A change to `locales/` runs the browser suite before the pull request,
  because a step may match the copy you changed: step 1 runs `e2e`, whose
  filter lists `locales/`
  ([lesson 0020](../../docs/lessons/0020-a-copy-change-that-ran-no-browser-test.md)).

## Workflows that push

- **Onto a pull request's branch**: push through `tools/push-to-pr-branch.mjs`,
  passing the workflow's own `pull_request_target.paths`
  ([ADR-0113](../../docs/decisions/ADR-0113-a-bot-pushing-onto-a-pull-request-replays-past-another-bot.md),
  [lesson 0016](../../docs/lessons/0016-a-push-filtered-by-paths-starts-no-run-to-supersede-yours.md)).
- **With a token on the remote URL**:
  [lesson 0018](../../docs/lessons/0018-a-persisted-checkout-token-outranks-the-pat-on-the-remote.md).
