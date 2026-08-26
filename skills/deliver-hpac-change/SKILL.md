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

- `/features` describes the target.
- Component READMEs describe their scope and current implementation status
  without duplicating the specification.
- ADRs are historical rationale. Add one only for a durable decision whose
  trade-off will not be clear from `/features` and code.
- Update issue acceptance criteria when the design changes; do not leave a
  conflicting backlog item open.
- Never include real report content or personal information.

## Verify and publish

1. Run focused tests, then the repository checks proportional to risk.
2. Inspect `git diff --check`, links, generated artifacts, and `git status`.
3. Commit with a concise imperative message and no co-author trailer.
4. Push and open a pull request with a squash-ready title.
5. Put `Closes #<number>` on its own line in the PR body.
6. Watch required checks, fix failures on the branch, and finish only when they
   are green.

Never hand-edit generated `.claude/` content. When project-owned skills change,
update `Skillfile`, regenerate `Skillfile.lock`, and run the repository's skill
validation.
