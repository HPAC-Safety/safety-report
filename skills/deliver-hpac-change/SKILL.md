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
- Push the branch to `origin` the moment it exists, before any work begins:
  `git push -u origin issue-<number>/<short-description>` from inside the
  worktree. This also replaces the `origin/main` upstream that
  `git worktree add` sets, so a later bare `git push` targets the issue branch.
- Commit and push each unit of work as soon as it is complete — a scenario
  written, a test passing, a step definition wired up, a document updated —
  rather than holding everything until the change is ready for a pull request.
  The first commit never waits for the whole change to be finished. Work that
  exists only in a local worktree is invisible to other agents and
  contributors and is lost if the worktree or session goes away.
- Rebase onto fresh `origin/main` before every push — each unit of work, the
  final push before opening a pull request, and each fix while watching
  checks, whether or not a pull request exists yet:
  `git fetch origin main && git rebase origin/main`. Other agents merge to
  `main` continuously; a branch that is not rebased before it is pushed is
  out of date, and often conflicted, the moment it lands. Resolve any
  conflicts locally, and if the rebase brought in new commits, re-run the
  checks the change affects before pushing. When the rebase rewrote commits
  already on `origin`, push with `git push --force-with-lease`, never plain
  `--force`, so a push someone else made to the branch is never discarded.
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
- ADRs are historical rationale, one per durable architectural decision
  (technology choice, rejected alternative, durable trade-off). This is
  mandatory, not discretionary — if a change makes such a decision, add the ADR
  in the same PR. A routine implementation detail with no rejected alternative
  does not need one.
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
2. Inspect `git diff --check`, links, generated artifacts, and `git status`.
3. Commit any remaining work with a concise imperative message and no
   co-author trailer, rebase onto fresh `origin/main`, and push. Earlier units
   of work are already committed and pushed (see "Start"); this is the last of
   them, not the first. A pull request is never opened from a branch that is
   behind `origin/main`.
4. Open a pull request with a squash-ready title.
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
   `./dev-up.sh` from the worktree (`./dev-up.sh --down` first if containers
   from another branch are running). It starts the containers detached, waits
   until the API and the dev server actually answer, prints their URLs, and
   returns — it does not tail logs. The running environment should be the
   change under review, not whatever branch was built last.
8. Remove the worktree (`git worktree remove`) immediately after — never leave
   one sitting around, whether the PR is still open, still failing checks, or
   already merged. Watching checks, reading logs, and commenting all work from
   the primary checkout via `gh`; none of it needs the worktree present.
9. Watch required checks from the primary checkout. If one fails, recreate the
   worktree on the *same* branch (no `-b`, it already exists —
   `git fetch origin issue-<number>/<short-description> && git worktree add .claude/worktrees/issue-<number>/<short-description> issue-<number>/<short-description>`),
   fix, committing, rebasing onto fresh `origin/main`, and pushing each fix as
   it lands, repeat step 7, then
   remove the worktree again. Finish only when
   checks are green and no worktree remains.

Never hand-edit generated `.claude/` content. When project-owned skills change,
update `Skillfile`, regenerate `Skillfile.lock`, and run the repository's skill
validation.
