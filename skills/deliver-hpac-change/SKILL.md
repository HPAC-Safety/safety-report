---
name: deliver-hpac-change
description: Deliver HPAC Safety work through its issue, branch, documentation, pull-request, and CI workflow. Use when creating or editing issues, docs, worktrees, PRs, or checks.
---

# Deliver an HPAC Safety change

## Start

- Work from a focused GitHub issue.
- When creating a new issue, check its real relationships to existing open
  issues before filing it, and wire them in with GitHub's native issue
  relations (`gh api graphql`; there is no `blocked` label) rather than only
  describing them in prose:
  - **Blocked by**: if the new issue's scope genuinely cannot be implemented
    or verified until another open issue lands (a schema/endpoint/DTO it
    consumes, a domain method it calls, a screen it extends), add the
    relation with the `addBlockedBy` mutation
    (`issueId` = the new issue, `blockingIssueId` = the prerequisite).
    State it in the body too (`Blocked by #N — <why>`, first paragraph) so it
    reads without opening the GitHub sidebar. Don't add a relation for a
    soft/parallel dependency ("touches similar code," "related area") —
    only a hard prerequisite.
  - **Parent / sub-issue**: only when the new issue is actually a piece
    carved out of a larger issue being split up (the larger issue's scope
    shrinks to what's left once the new issue is filed) — use `addSubIssue`
    to attach it to that parent. Do not create a parent/child link between
    independently-scoped issues that merely happen to be prerequisites of
    each other or of a checklist/capstone issue; that's a `blocked by`
    relation, not a hierarchy — forcing one misrepresents GitHub's rollup
    completion percentage.
  - **Duplicate of**: if filing would duplicate an already-open issue's
    scope instead of narrowing or splitting it, don't file a second issue —
    either extend the existing one or, if both must exist for tracking
    reasons, mark the new one `duplicateOf` the original.
  - When an issue closes or a design change removes a dependency, remove the
    now-stale relation (`removeBlockedBy`) in the same pass rather than
    leaving it pointing at resolved work.
- Never create work directly on a branch in the primary checkout. Fetch fresh
  `origin/main`, then create a git worktree off it at
  `.claude/worktrees/issue-<number>/<short-description>` (already gitignored),
  with a branch named `issue-<number>/<short-description>` inside it:
  `git fetch origin main && git worktree add -b issue-<number>/<short-description> .claude/worktrees/issue-<number>/<short-description> origin/main`.
  Multiple agents may be working in this repository at once; a worktree per
  issue means no agent ever switches a branch out from under another one's
  in-progress checkout.
  - **Check this at the moment of the first edit, not only once at the start
    of a session.** Before the first `Edit` or `Write` call for an issue, run
    `git branch --show-current`. If it reports `main`, stop and create the
    worktree first — do not write the file "just this once" and fix it
    later. A rule read once, hours earlier in a long conversation, is not a
    rule checked again on its own; nothing about resuming a session, a
    sequencing detour ("which of these blockers do we do first?"), or a
    plain "continue"/"yes" looks like "starting an issue," which is exactly
    why this is the moment the check gets skipped
    ([Lesson 0004](../../docs/lessons/0004-a-rule-read-once-is-not-a-rule-checked-again.md)).
    Having followed this rule correctly on an earlier issue in the same
    session is not evidence it will hold on the next one — check every time.
- Push the branch to `origin` the moment it exists, before any work begins:
  `git push -u origin issue-<number>/<short-description>` from inside the
  worktree. This also replaces the `origin/main` upstream that
  `git worktree add` sets, so a later bare `git push` targets the issue branch.
- Label the session with the issue it owns, in the same step:
  `tools/session-label.sh "#<number> <short-description>"`. Several agents run
  at once, one terminal tab each, and the person running them finds the tab
  that owns an issue or pull request by this label — not by reading scrollback.
  Relabel when the pull request opens and when its checks go green (see
  "Verify and publish"), and open every final report to the person with
  `[#<number> · PR #<pr>]` (just `[#<number>]` before the pull request exists),
  even when the report is one line.
- Commit and push each unit of work as soon as it is complete — a scenario
  written, a test passing, a step definition wired up, a document updated —
  rather than holding everything until the change is ready for a pull request.
  The first commit never waits for the whole change to be finished. Work that
  exists only in a local worktree is invisible to other agents and
  contributors and is lost if the worktree or session goes away.
- Rebase onto fresh `origin/main` before every **commit**, not only before
  every push — each unit of work, the final commit before opening a pull
  request, and each fix while watching checks, whether or not a pull request
  exists yet:
  `git fetch origin main && git rebase origin/main`. Other agents merge to
  `main` continuously; a branch that is not rebased before it is pushed is
  out of date, and often conflicted, the moment it lands. Resolve any
  conflicts locally, and if the rebase brought in new commits, re-run the
  checks the change affects before pushing. When the rebase rewrote commits
  already on `origin`, push with `git push --force-with-lease`, never plain
  `--force`, so a push someone else made to the branch is never discarded.
- **Claim a shared identifier from the tree as it is after that rebase, never
  from the tree as it was when you started.** A number, a name, a slug or a
  migration timestamp is claimed the moment you write it down, and somebody
  else may have claimed it while you were working
  ([lesson 0003](../../docs/lessons/0003-a-number-is-claimed-the-moment-someone-else-merges.md)).
  For a decision record, `node tools/adr-numbers.mjs --next` reads every
  fetched remote branch, so a number an open pull request has already taken is
  skipped. If you lose the race anyway,
  `node tools/adr-numbers.mjs --renumber <old> <new>` moves the file and
  rewrites every reference in one pass — renaming is cheap, so take the new
  number rather than arguing for the old one. When two records already share
  the number, add `--file <name>` to say which one moves, and expect a list of
  bare `ADR-NNNN` mentions it deliberately left alone: while the number names
  two records, only a reference by filename says which is meant, and those are
  resolved by hand.
- Do all work for the issue inside that worktree. Remove it once the pull
  request is open — see "Verify and publish" for exactly when it comes down
  and how it comes back if a check fails.
- Read the affected `/features` pages before editing. Update them first if the
  target behavior is changing.
- Preserve unrelated work in a dirty tree.

## Document

- `/features` describes the target. Every user-facing requirement is covered by
  a scenario in a `.feature` file. This is mandatory, not discretionary — if a
  change adds or changes behavior, add or update the scenario in the same PR.
- A behavior change with no scenario fails `feature-coverage`. An exemption is
  a **citation**, never an assertion: a closed category, a reason that says what
  changed and why no behavior did, and the claim IDs the change leaves standing,
  each checked against the matrix
  ([ADR-0090](../../docs/decisions/ADR-0090-an-exemption-cites-the-claims-it-preserves.md)).
  Do not reach for it because writing the scenario is slower — if you cannot
  name the claims your change preserves, the change needs a scenario. Run
  `node tools/feature-coverage.mjs` locally rather than discovering this in CI.
- Write the scenario before the implementation, and when the implementation
  turns out to do the wrong thing, correct the scenario rather than arguing it
  out in conversation
  ([ADR-0083](../../docs/decisions/ADR-0083-specification-driven-development.md)).
  The PR body states what changed upstream — which scenario, which page, which
  boundary — because the body becomes the squash commit message, so the
  specification delta is what lands in history.
- A scenario carries `@ignore` until its behavior is implemented (ADR-0049).
  When a decision supersedes what a scenario asserts, **delete the scenario**
  rather than parking it behind `@ignore` — `@ignore` means "not built yet,"
  never "no longer true." Git history keeps the removed text.
  Implementing it means writing its Reqnroll step definitions in
  `tests/HpacSafety.Acceptance.Tests` and removing the `@ignore` tag, in the
  same PR that implements the behavior.
- Component READMEs describe their scope and current implementation status
  without duplicating the specification.
- Each `features/<area>/README.md` records what **not** to build in that area,
  and a change that draws a new boundary writes it there rather than only in
  the pull request that argued about it.
- A bug fix that reveals a specification gap writes a lesson under
  [`docs/lessons/`](../../docs/lessons/README.md) in the same pull request —
  symptom, root cause, spec delta, and the claim that now proves it
  ([ADR-0085](../../docs/decisions/ADR-0085-a-lesson-flows-upstream-into-the-specification.md)).
  A fix that reveals nothing does not.
- When that lesson is about the **development process** — tooling, CI, hooks,
  conventions, the delivery workflow, how agents are expected to work — it also
  updates the skill that would have prevented it, in the same pull request, and
  names that skill in its `## Skill` section. The skill states the general
  rule; the lesson keeps the incident. An agent reads the skills before it
  starts and does not read the lessons index looking for a mistake it has not
  made yet.
- A lesson about **product requirements** does not change a skill. Its remedy
  is a claim and a scenario, and restating product behavior in a skill creates
  a second place for it to drift from `/features`.
- Every markdown file you add opens with frontmatter naming its `title`,
  `description`, and `type`
  ([ADR-0087](../../docs/decisions/ADR-0087-every-markdown-file-declares-itself.md)).
  A `SKILL.md` or an `agents/*.md` carries the `name`/`description` pair its
  loader expects instead. `node tools/check-frontmatter.mjs` is the authority.
- ADRs are historical rationale, one per durable architectural decision
  (technology choice, rejected alternative, durable trade-off). This is
  mandatory, not discretionary — if a change makes such a decision, add the ADR
  in the same PR. A routine implementation detail with no rejected alternative
  does not need one. Number it with `node tools/adr-numbers.mjs --next` after
  rebasing, and keep the filename and the document's own `# ADR-NNNN` heading
  in step — `node tools/adr-numbers.mjs` fails a duplicate or a disagreement,
  in the pre-commit hook and in CI (ADR-0091).
- Never restate a `.feature` scenario's acceptance criteria inside an ADR, and
  never justify a technology/pattern choice inside a `.feature` file or its
  README — keep decision rationale and behavior requirements in their own
  document.
- An ADR that changes a technology choice or how the system is broken up
  (a new or replaced framework/language/runtime, a hosting/topology change, a
  service split or merge) updates the root [`README.md`](../../README.md) in
  the same PR — the fact and a link to the ADR, not the rationale. Do not add
  an entry for a routine or reversed-without-effect decision; keep the README
  short and let the linked ADR carry the "why."
- Update issue acceptance criteria when the design changes; do not leave a
  conflicting backlog item open.
- Never include real report content or personal information.

## Verify and publish

1. Run focused tests, then the repository checks proportional to risk.
   **Before opening a pull request that touches `src/`, `tests/`, or
   `tools/`, run `tools/coverage-check.sh` and do not open it until the gate
   passes.** It measures `origin/main` and this branch on the same machine
   with CI's own commands and runs the same ratchet CI does, so a coverage
   drop is found here, not by the reviewer. A green test run is not a passing
   coverage gate: deleting well-covered code while adding code with untested
   branches passes every test and still fails the ratchet
   ([lesson 0010](../../docs/lessons/0010-a-coverage-gate-found-in-ci-not-before-the-pull-request.md)).
   When it fails, add the test that pins down the behaviour of each uncovered
   branch the change added; a branch that can never run is deleted, not
   tested.
2. Inspect `git diff --check`, links, generated artifacts, and `git status`.
3. Commit any remaining work with a concise imperative message and no
   co-author trailer, rebase onto fresh `origin/main`, and push. Earlier units
   of work are already committed and pushed (see "Start"); this is the last of
   them, not the first. A pull request is never opened from a branch that is
   behind `origin/main`.
4. Open a pull request with a squash-ready title, then relabel the session
   with it: `tools/session-label.sh "#<number> · PR #<pr> <short-description>"`.
   After the worktree is removed the session runs from the primary checkout
   on `main`, so this label is the only thing still saying which pull request
   the session owns.
5. Put `Closes #<number>` on its own line in the PR body, and name the
   scenarios the change satisfies. If it built anything the specification does
   not describe, either the specification was incomplete — fix it — or the
   change exceeded its scope.
6. A PR that changes anything user-visible in `src/web` attaches screenshots
   demonstrating it, in the PR body or a comment — a browser tool
   (Playwright, Claude in Chrome) capturing the real running app, not a
   mockup. A new page/component (a CREATE) needs an after screenshot; a
   change to an existing one (an UPDATE) needs both before and after. Commit
   the image files under `docs/screenshots/<short-description>/`, named
   `before-*`/`after-*`, and reference them from the PR body or a comment
   (`gh pr create`/`gh pr comment --attach`) rather than only pasting them
   inline. Set the locale to English before capturing — the default locale
   the running app starts in is whatever the browser or a prior session left
   it at, and a screenshot in French reads to a reviewer as broken or
   untranslated rather than as the other official language working correctly.
7. After pushing, bring the local Docker environment up on the pushed code:
   `./dev-up.sh` from the worktree. It takes the dev ports over from any other
   checkout of this repository that still holds them, starts the containers
   detached, waits until the API and the dev server actually answer, prints
   their URLs, and returns — it does not tail logs. This proves the pushed
   code starts, not only that it builds.
8. Tear the environment down and remove the worktree immediately after:
   `./dev-up.sh --down`, then `git worktree remove`. Containers belong to the
   checkout that started them — compose names the project after the worktree
   directory, and the web container bind-mounts it — so removing the worktree
   without `--down` leaves containers running from a directory that no longer
   exists, holding the ports every other checkout needs
   ([lesson 0008](../../docs/lessons/0008-containers-outlive-the-worktree-that-started-them.md)).
   Never leave a worktree sitting around, whether the PR is still open, still
   failing checks, or already merged. Watching checks, reading logs, and
   commenting all work from the primary checkout via `gh`; none of it needs
   the worktree present.
9. Watch required checks from the primary checkout. If one fails, recreate the
   worktree on the *same* branch (no `-b`, it already exists —
   `git fetch origin issue-<number>/<short-description> && git worktree add .claude/worktrees/issue-<number>/<short-description> issue-<number>/<short-description>`),
   fix, committing, rebasing onto fresh `origin/main`, and pushing each fix as
   it lands, repeat steps 7 and 8. Finish only when
   checks are green and no worktree remains, and mark the session done:
   `tools/session-label.sh "✓ #<number> · PR #<pr> green"`.

Never hand-edit generated `.claude/` content. When project-owned skills change,
update `Skillfile`, regenerate `Skillfile.lock`, and run the repository's skill
validation.
