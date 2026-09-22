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
- Do all work for the issue inside that worktree. Never leave it in place
  after a push — see "Verify and publish" for exactly when it comes down and
  how it comes back if a check fails.
- Read the affected `/features` pages before editing. Update them first if the
  target behavior is changing.
- Preserve unrelated work in a dirty tree.

## Document

- `/features` describes the target. Every user-facing requirement is covered by
  a scenario in a `.feature` file. This is mandatory, not discretionary — if a
  change adds or changes behavior, add or update the scenario in the same PR.
- A scenario carries `@ignore` until its behavior is implemented (ADR-0049).
  When a decision supersedes what a scenario asserts, **delete the scenario**
  rather than parking it behind `@ignore` — `@ignore` means "not built yet,"
  never "no longer true." Git history keeps the removed text.
  Implementing it means writing its Reqnroll step definitions in
  `tests/HpacSafety.Acceptance.Tests` and removing the `@ignore` tag, in the
  same PR that implements the behavior.
- Component READMEs describe their scope and current implementation status
  without duplicating the specification.
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
3. Commit with a concise imperative message and no co-author trailer.
4. Push and open a pull request with a squash-ready title.
5. Put `Closes #<number>` on its own line in the PR body.
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
   fix, push, repeat step 7, then remove the worktree again. Finish only when
   checks are green and no worktree remains.

Never hand-edit generated `.claude/` content. When project-owned skills change,
update `Skillfile`, regenerate `Skillfile.lock`, and run the repository's skill
validation.
