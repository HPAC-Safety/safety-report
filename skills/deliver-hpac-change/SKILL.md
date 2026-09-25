---
name: deliver-hpac-change
description: Deliver HPAC Safety work through its issue, branch, documentation, pull-request, and CI workflow. Use when creating or editing issues, docs, worktrees, PRs, or checks.
---

# Deliver an HPAC Safety change

## Start

### Claim the issue

- Work from one focused GitHub issue.
- **Never pick up an issue labelled `in progress`** — another agent has claimed
  it, even if it looks stalled or is the obvious next piece. Find work with
  `gh issue list --state open --search '-label:"in progress"'`. Asked for a
  labelled issue by number? Stop and ask the person.
- **Label it `in progress` the moment you pick it up, before anything else** —
  before the worktree, branch, or first edit:
  `gh issue edit <number> --add-label "in progress"`.
  - No exceptions: not for a one-line fix, an issue you just filed, or a
    resumed session. The label is the only thing telling the next agent it is
    taken.
  - Stop without a pull request? Remove it:
    `gh issue edit <number> --remove-label "in progress"`.
  - It stays while a pull request is open; closing the issue takes it off the
    board.

### File a new issue

**Milestone and labels are mandatory — no exceptions.** Pass both on
`gh issue create`, never as a follow-up:

- **Milestone**: exactly one open milestone
  (`gh api repos/{owner}/{repo}/milestones --jq '.[].title'`). An issue carved
  out of a parent takes the parent's milestone.
- **Labels**: every relevant one from `gh label list`:
  - one type — `enhancement`, `bug`, `documentation`, or `tech-debt`;
  - every `area:*` the change touches;
  - the `phase:*` matching a phase milestone.
- **Verify** with `gh issue view <number> --json milestone,labels` before
  linking or picking the issue up. Missing either? Fix it before anything else.

Check its real relationships to open issues first, and wire them with GitHub's
native relations (`gh api graphql`; there is no `blocked` label), not only
prose:

- **Blocked by** — only a hard prerequisite: it cannot be implemented or
  verified until another issue lands (a schema, endpoint, DTO, domain method,
  or screen it builds on). Add `addBlockedBy` (`issueId` = new issue,
  `blockingIssueId` = prerequisite), and say `Blocked by #N — <why>` in the
  body's first paragraph. No relation for a soft dependency ("touches similar
  code", "related area").
- **Sub-issue** — only when the new issue is carved out of a larger one being
  split, so the parent's scope shrinks. Use `addSubIssue`. Never link
  independently scoped prerequisites as parent and child; that is `blocked by`,
  and a false hierarchy skews GitHub's completion rollup.
- **Duplicate** — never file a second issue for the same scope. Extend the
  existing one, or, if both must exist, mark the new one `duplicateOf` it.
- When an issue closes or a design change removes a dependency, remove the
  stale relation (`removeBlockedBy`) in the same pass.
- **Relates to** — for a soft link between issues that are neither a
  prerequisite nor a split. It has no API: set it in the issue sidebar,
  Relationships → "Add relates to".

### Worktree and branch

- **Never work on a branch in the primary checkout.** Create a worktree off
  fresh `origin/main`:
  `git fetch origin main && git worktree add -b issue-<number>/<short-description> .claude/worktrees/issue-<number>/<short-description> origin/main`.
  Several agents share this repository; a worktree per issue means none
  switches a branch out from under another.
- **Check at the first edit, every time.** Before the first `Edit` or `Write`
  for an issue, run `git branch --show-current`. If it says `main`, stop and
  create the worktree. A resumed session, a sequencing detour, or a plain
  "continue" does not look like "starting an issue", which is exactly when this
  gets skipped. Having done it right on an earlier issue proves nothing
  ([lesson 0004](../../docs/lessons/0004-a-rule-read-once-is-not-a-rule-checked-again.md)).
- **Push the branch at once**, from the worktree:
  `git push -u origin issue-<number>/<short-description>`. This also replaces
  the `origin/main` upstream, so a bare `git push` targets the branch.
- **Label the session** in the same step:
  `tools/session-label.sh "#<number> <short-description>"`. The person finds
  the tab that owns an issue by this label. Relabel when the pull request opens
  and when checks go green (see "Verify and publish").
- Open every final report to the person with `[#<number> · PR #<pr>]` (just
  `[#<number>]` before the pull request exists), even a one-line report.
- Do all work in the worktree. It comes down once the pull request is open
  (step 8).

### Commit, rebase, claim identifiers

- **Commit and push each unit of work as soon as it is done** — a scenario, a
  passing test, a step definition, a document. Never hold work for the pull
  request; local-only work is invisible to others and lost with the worktree.
- **Rebase onto fresh `origin/main` before every commit**, not only before a
  push — each unit, the final commit, and each fix while watching checks:
  `git fetch origin main && git rebase origin/main`.
  - Resolve conflicts locally. If the rebase brought in commits, re-run the
    affected checks before pushing.
  - Rewrote commits already on `origin`? `git push --force-with-lease`, never
    plain `--force`.
- **Claim a shared identifier after that rebase**, never from the tree as you
  started — a number, name, slug, or migration timestamp is claimed the moment
  someone else merges it
  ([lesson 0003](../../docs/lessons/0003-a-number-is-claimed-the-moment-someone-else-merges.md)).
  - ADR number: `node tools/adr-numbers.mjs --next` (counts every fetched
    remote branch, so an open pull request's number is skipped).
  - Lost the race? `node tools/adr-numbers.mjs --renumber <old> <new>` moves
    the file and rewrites every reference. Take the new number; renaming is
    cheap.
  - Two records already share the number? Add `--file <name>` to say which
    moves. Bare `ADR-NNNN` mentions it leaves alone are ambiguous; resolve them
    by hand.

### Before editing

- Read the affected `/features` pages. Update them first if the target
  behavior changes.
- Preserve unrelated work in a dirty tree.

## Document

### Scenarios

- Every user-facing requirement has a scenario in a `.feature` file. A change
  that adds or changes behavior adds or updates it in the same pull request —
  mandatory.
- Write the scenario first; when the implementation is wrong, correct the
  scenario, not the conversation
  ([ADR-0083](../../docs/decisions/ADR-0083-specification-driven-development.md)).
- The PR body names what changed upstream — scenario, page, boundary — because
  it becomes the squash commit message.
- `@ignore` and superseded scenarios: see
  [`test-hpac-safety`](../test-hpac-safety/SKILL.md) "Scenarios".
- Each `features/<area>/README.md` records what **not** to build. A change that
  draws a new boundary writes it there, not only in the pull request.
- Component READMEs describe scope and implementation status without
  duplicating the specification.

### The `feature-coverage` exemption

- Rules: `AGENTS.md` "The `feature-coverage` exemption"
  ([ADR-0090](../../docs/decisions/ADR-0090-an-exemption-cites-the-claims-it-preserves.md)).
- Pick the category from the list in `.github/pull_request_template.md`
  ("Specification delta"). Never invent one: the vocabulary is closed, and a
  test keeps the template's list equal to the tool's.
- Run the check locally, not only in CI. The tool reads its inputs from the
  environment, so a bare run checks nothing and always passes:

  ```sh
  CHANGED_BEHAVIOR="$(git diff --name-only origin/main...HEAD -- 'src/**' 'tests/e2e/**/*.ts')" \
  CHANGED_FEATURES="$(git diff --name-only origin/main...HEAD -- 'features/**/*.feature')" \
  PR_BODY="$(cat pr-body.md)" node tools/feature-coverage.mjs
  ```
- Renovate writes its own `dependency` exemption for `src/web` bumps from
  `renovate.json`
  ([ADR-0111](../../docs/decisions/ADR-0111-renovate-cites-the-claims-a-web-dependency-bump-preserves.md)).
  If the check fails on a Renovate pull request, fix the citation in
  `renovate.json`; never hand-edit the pull request body.

### Lessons

- A bug fix that reveals a specification gap writes a lesson under
  [`docs/lessons/`](../../docs/lessons/README.md) in the same pull request:
  symptom, root cause, spec delta, and the claim that now proves it
  ([ADR-0085](../../docs/decisions/ADR-0085-a-lesson-flows-upstream-into-the-specification.md)).
  A fix that reveals nothing writes none.
- **Process lesson** (tooling, CI, hooks, conventions, delivery, how agents
  work): also update the skill that would have prevented it, in the same pull
  request, and name it in the lesson's `## Skill` section. The skill holds the
  general rule; the lesson keeps the incident. Agents read skills, not the
  lessons index.
- **Product lesson**: no skill change. Its remedy is a claim and a scenario;
  restating product behavior in a skill creates a second place to drift from
  `/features`.

### ADRs

- One ADR per durable architectural decision (technology choice, rejected
  alternative, durable trade-off), in the same pull request — mandatory. A
  routine detail with no rejected alternative needs none.
- Number it after rebasing (see "Commit, rebase, claim identifiers"). Keep the
  filename and the `# ADR-NNNN` heading in step; `node tools/adr-numbers.mjs`
  fails a duplicate or a mismatch, in the pre-commit hook and CI
  ([ADR-0091](../../docs/decisions/ADR-0091-an-adr-number-is-verified-not-assumed.md)).
- Keep rationale and requirements apart: never restate a scenario's acceptance
  criteria in an ADR, and never justify a technology or pattern choice in a
  `.feature` file or its README.
- An ADR that changes a technology choice or how the system is broken up (new
  or replaced framework, language, runtime, hosting, topology, service split or
  merge) updates the root [`README.md`](../../README.md) in the same pull
  request — the fact and the ADR link, not the rationale. Skip routine or
  reversed-without-effect decisions.

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
- Never include real report content or personal information.

### Agent instructions

- `AGENTS.md`, skills, and role agents follow
  [`ai-author`](../../agents/ai-author.md).
- Never hand-edit generated `.claude/` content. When a project-owned skill or
  agent changes, update `Skillfile`, regenerate `Skillfile.lock`, and run the
  skill validation (`skillfile validate`, `skillfile install`).

## Keep the issue true while you work

The issue is the first hop of the specification chain: **correct the artifact,
not the chat.** A decision made after pickup — an answered question, an owner's
call, a scope change, something found already done — is not recorded until it
is in the issue. The next agent, the reviewer, and the squash commit read the
issue, not this conversation.

- **Edit the issue as soon as a decision lands, before building on it.** Keep
  the original need readable, and add or update:
  - **Decisions** — each decision, who made it, the date, one line each, with
    the rejected option where there was one;
  - **Acceptance criteria** — rewritten to match what will now be built;
  - **Out of scope** — what the discussion chose not to build.
  Strike or remove text the decisions made false.
- **Open a new issue** when added scope could ship on its own: independently
  deliverable, in a different area or layer, or roughly doubling the pull
  request. Give it its need, decisions, and acceptance criteria; wire it with a
  native relation (see "File a new issue"); link it from the original's
  Decisions. Scope that only makes the original need work stays.
- **Scope shrank?** Say so in the issue, and file what was dropped as its own
  issue if it is still wanted.
- **Re-read the issue before opening the pull request.** Its acceptance
  criteria, the scenarios, and the PR body describe the same change; if not,
  fix the issue or specification first.

## Verify and publish

1. **Test, then pass the coverage gate.** Run focused tests, then repository
   checks in proportion to risk. **A pull request touching `src/`, `tests/`, or
   `tools/` is not opened until `tools/coverage-check.sh` passes.** It measures
   `origin/main` and this branch on one machine with CI's commands and ratchet.
   A green test run is not a passing gate
   ([lesson 0010](../../docs/lessons/0010-a-coverage-gate-found-in-ci-not-before-the-pull-request.md)).
   On failure, test each uncovered branch the change added; delete a branch
   that can never run.
2. **Inspect** `git diff --check`, links, generated artifacts, and
   `git status`.
3. **Commit the last unit** — concise imperative message, no co-author
   trailer — rebase onto fresh `origin/main`, and push. Never open a pull
   request from a branch behind `origin/main`.
4. **Open the pull request** with a squash-ready title, then relabel:
   `tools/session-label.sh "#<number> · PR #<pr> <short-description>"`. Once
   the worktree is gone this label is the only record of which pull request the
   session owns.
5. **PR body**: `Closes #<number>` on its own line, and the scenarios it
   satisfies. Built something the specification does not describe? Either fix
   the specification or the change exceeded its scope.
6. **Screenshots** for any user-visible `src/web` change:
   - captured from the real running app by a browser tool (Playwright, Claude
     in Chrome), not a mockup;
   - a new page or component: an after shot; a changed one: before and after;
   - set the locale to English first — a French shot reads as broken;
   - commit under `docs/screenshots/<short-description>/`, named `before-*` /
     `after-*`, and reference them from the body or a comment
     (`gh pr create` / `gh pr comment --attach`), not only pasted inline;
   - reference each by its raw URL pinned to the adding commit:
     `https://raw.githubusercontent.com/HPAC-Safety/safety-report/<sha>/docs/screenshots/<dir>/<file>.png`.
     A relative path or `github.com/…/blob/…` URL renders broken;
   - before reporting, check each URL answers `image/png`:
     `curl -sI <url> | grep -i content-type`
     ([lesson 0017](../../docs/lessons/0017-a-screenshot-linked-by-a-page-url-renders-broken.md)).
7. **Prove it starts.** After pushing, run `./dev-up.sh` from the worktree. It
   takes the dev ports from any other checkout, starts containers detached,
   waits until the API and dev server answer, prints their URLs, and returns.
8. **Tear down at once**: `./dev-up.sh --down`, then `git worktree remove`.
   Containers belong to the checkout that started them; removing the worktree
   first leaves them holding the ports
   ([lesson 0008](../../docs/lessons/0008-containers-outlive-the-worktree-that-started-them.md)).
   Never leave a worktree behind — open, failing, or merged. Watching checks,
   reading logs, and commenting work from the primary checkout via `gh`.
9. **Watch required checks** from the primary checkout.
   - A check fails: recreate the worktree on the same branch (no `-b`):
     `git fetch origin issue-<number>/<short-description> && git worktree add .claude/worktrees/issue-<number>/<short-description> issue-<number>/<short-description>`.
     Fix, commit, rebase, push each fix, and repeat steps 7 and 8.
   - Green: fetch and confirm `gh pr view <pr> --json mergeStateStatus` is not
     `BEHIND` — `main` moves while checks run. Behind? Rebase, push, watch again
     ([lesson 0011](../../docs/lessons/0011-a-branch-rebased-before-its-push-is-behind-by-the-time-it-is-green.md)).
   - Finish only when checks are green on a current branch and no worktree
     remains, then: `tools/session-label.sh "✓ #<number> · PR #<pr> green"`.

## Path filters

- A CI job's path filter lists every input that job reads, not only the
  directory its code lives in. The web bundle loads `locales/` from the
  repository root, so `web` and `e2e` both list it. A missing input skips the
  job, and GitHub counts a skipped job as passing.
- A change to `locales/` runs the browser suite locally
  (`npm --prefix tests/e2e test`) before the pull request, because a step may
  match the copy you changed
  ([lesson 0020](../../docs/lessons/0020-a-copy-change-that-ran-no-browser-test.md)).

## Workflows that push

- **Onto a pull request's branch**: push through `tools/push-to-pr-branch.mjs`,
  passing the workflow's own `pull_request_target.paths`, never a bare
  `git push`. GitHub filters `paths` per push, so a bot's push often starts no
  run of the other bot; the tool replays the commit on top when the newer push
  touched none of the workflow's trigger files
  ([ADR-0113](../../docs/decisions/ADR-0113-a-bot-pushing-onto-a-pull-request-replays-past-another-bot.md),
  [lesson 0016](../../docs/lessons/0016-a-push-filtered-by-paths-starts-no-run-to-supersede-yours.md)).
- **With a token on the remote URL**: check out with
  `persist-credentials: false`. The persisted `GITHUB_TOKEN` header outranks
  the URL, so the push authenticates as `github-actions[bot]` and its CI waits
  for maintainer approval
  ([lesson 0018](../../docs/lessons/0018-a-persisted-checkout-token-outranks-the-pat-on-the-remote.md)).
