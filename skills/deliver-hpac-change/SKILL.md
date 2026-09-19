---
name: deliver-hpac-change
description: Deliver HPAC Safety work through its issue, branch, documentation, pull-request, and CI workflow. Use when creating or editing issues, docs, branches, commits, PRs, or checks.
---

# Deliver an HPAC Safety change

## Start

- Work from current `main` and a focused GitHub issue.
- Name the branch `issue-<number>/<short-description>`.
- Read the affected `/features` pages before editing. Update them first if the
  target behavior is changing.
- Preserve unrelated work in a dirty tree.

## Document

- `/features` describes the target. Every user-facing requirement is covered by
  a scenario in a `.feature` file. This is mandatory, not discretionary — if a
  change adds or changes behavior, add or update the scenario in the same PR.
- A scenario carries `@ignore` until its behavior is implemented (ADR-0049).
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
   change to an existing one (an UPDATE) needs both before and after.
7. Watch required checks, fix failures on the branch, and finish only when they
   are green.

Never hand-edit generated `.claude/` content. When project-owned skills change,
update `Skillfile`, regenerate `Skillfile.lock`, and run the repository's skill
validation.
