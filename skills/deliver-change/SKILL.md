---
name: deliver-change
description: Deliver a change through a GitHub issue, worktree, specification, documentation, pull request, and CI workflow shared by several agents. Use when creating or editing issues, docs, worktrees, PRs, or checks.
---

# Deliver a change

**Project rules.** A project may extend this skill with a companion skill that
names this one; its agent instructions (`AGENTS.md`) list it. Read both. The
companion holds the project's tools, commands, and paths for each step here,
and wins where they differ.

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
- **Labels**: every relevant one from `gh label list` — one type, and every
  area and phase label the change matches. The project skill names the
  vocabulary.
- **Verify** with `gh issue view <number> --json milestone,labels` before
  linking or picking the issue up. Missing either? Fix it before anything else.

#### Relationships

**Wire relationships when the issue is created, not later.** Every new issue is
checked against open issues (`gh issue list --state open`) and against every
other issue filed in the same pass. Set each relationship that genuinely
exists, with GitHub's native relations (`gh api graphql`; there is no `blocked`
label), not only prose. A link known at filing and left unset is lost.

- **Filing several issues at once** — splitting a feature, or recording
  follow-ups found mid-change: plan the graph before creating any.
  1. Which issue is the parent, and which are carved out of it (sub-issue)?
  2. Which must land before which (blocked by)?
  3. Which only relate (relates to)?

  Create the parent first, then the children, then wire every relation in the
  same pass.
- **Verify** alongside milestone and labels:
  `gh api graphql -f query='{repository(owner:"<owner>",name:"<repo>"){issue(number:<n>){parent{number} subIssues(first:50){nodes{number}} blockedBy(first:20){nodes{number}}}}}'`.

The relations, strongest first. Use the strongest that is true, never a
stronger one:

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
  prerequisite nor a split: a follow-up, a sibling in the same area, the issue
  whose work surfaced this one. Set it rather than leaving the link only in
  prose. It has no API: set it in the issue sidebar, Relationships → "Add
  relates to".

### Worktree and branch

- **Never work on a branch in the primary checkout.** Create a worktree off
  fresh `origin/main`:
  `git fetch origin main && git worktree add -b issue-<number>/<short-description> .claude/worktrees/issue-<number>/<short-description> origin/main`.
  Several agents share the repository; a worktree per issue means none
  switches a branch out from under another.
- **Check at the first edit, every time.** Before the first `Edit` or `Write`
  for an issue, run `git branch --show-current`. If it says `main`, stop and
  create the worktree. A resumed session, a sequencing detour, or a plain
  "continue" does not look like "starting an issue", which is exactly when this
  gets skipped. Having done it right on an earlier issue proves nothing.
- **Push the branch at once**, from the worktree:
  `git push -u origin issue-<number>/<short-description>`. This also replaces
  the `origin/main` upstream, so a bare `git push` targets the branch.
- **Label the session** in the same step, with the project's session-label
  command, as `#<number> <short-description>`. The person finds the tab that
  owns an issue by this label. Relabel when the pull request opens and when
  checks go green (see "Verify and publish").
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
  someone else merges it.
  - Take the next ADR number from a tool that counts every fetched remote
    branch, so an open pull request's number is skipped.
  - Lost the race? Take the new number and rewrite every reference; renaming
    is cheap.

### Before editing

- Read the affected specification pages. Update them first if the target
  behavior changes.
- Preserve unrelated work in a dirty tree.

## Document

### Scenarios

- Every user-facing requirement has a scenario in a `.feature` file. A change
  that adds or changes behavior adds or updates it in the same pull request —
  mandatory.
- Write the scenario first; when the implementation is wrong, correct the
  scenario, not the conversation.
- The PR body names what changed upstream — scenario, page, boundary — because
  it becomes the squash commit message.
- `@ignore` and superseded scenarios: see
  [`test-from-scenarios`](../test-from-scenarios/SKILL.md) "Scenarios".
- Each area's specification page records what **not** to build. A change that
  draws a new boundary writes it there, not only in the pull request.
- Component READMEs describe scope and implementation status without
  duplicating the specification.

### Exemptions from scenario coverage

- A behavior-changing pull request that touches no scenario fails the
  project's coverage check. An exemption is only for a change that alters no
  behavior, and cites the claims the change leaves standing.
- Pick the exemption category from the project's closed list, where the pull
  request template shows it. Never invent one.
- Run the check locally, not only in CI. When the tool reads its inputs from
  the environment, a bare run checks nothing and always passes.
- A bot that opens pull requests (a dependency updater) writes its own
  exemption from its configuration. If the check fails on one, fix the
  citation in that configuration; never hand-edit the pull request body.

### Lessons

- A bug fix that reveals a specification gap writes a lesson in the same pull
  request: symptom, root cause, spec delta, and the claim that now proves it.
  A fix that reveals nothing writes none.
- **Process lesson** (tooling, CI, hooks, conventions, delivery, how agents
  work): also update the skill that would have prevented it, in the same pull
  request, and name it in the lesson's `## Skill` section. The skill holds the
  general rule; the lesson keeps the incident. Agents read skills, not the
  lessons index.
- **Product lesson**: no skill change. Its remedy is a claim and a scenario;
  restating product behavior in a skill creates a second place to drift from
  the specification.

### ADRs

- One ADR per durable architectural decision (technology choice, rejected
  alternative, durable trade-off), in the same pull request — mandatory. A
  routine detail with no rejected alternative needs none.
- Number it after rebasing (see "Commit, rebase, claim identifiers"). Keep the
  filename and the `# ADR-NNNN` heading in step.
- Keep rationale and requirements apart: never restate a scenario's acceptance
  criteria in an ADR, and never justify a technology or pattern choice in a
  `.feature` file or its README.
- An ADR that changes a technology choice or how the system is broken up (new
  or replaced framework, language, runtime, hosting, topology, service split or
  merge) updates the root `README.md` in the same pull request — the fact and
  the ADR link, not the rationale. Skip routine or reversed-without-effect
  decisions.

### Markdown

- Every tracked markdown file declares frontmatter that says what it is; the
  project skill names the keys and the checking tool.
- Never include real user content or personal information.

### Agent instructions

- The agent instructions, skills, and role agents follow the `ai-author` role.
- Never hand-edit generated skill or agent copies. When a project-owned skill
  or agent changes, update its install manifest and lock file, and re-run the
  install.

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

The step numbers are stable; the project skill adds its commands under the
same numbers.

1. **Test, then pass the coverage gate.** Run focused tests, then repository
   checks in proportion to risk. A pull request touching code, tests, or tools
   is not opened until the project's local coverage gate passes. A green test
   run is not a passing gate. On failure, test each uncovered branch the change
   added; delete a branch that can never run.
2. **Inspect** `git diff --check`, links, generated artifacts, and
   `git status`.
3. **Commit the last unit** — concise imperative message, no co-author
   trailer — rebase onto fresh `origin/main`, and push. Never open a pull
   request from a branch behind `origin/main`.
4. **Open the pull request** with a squash-ready title, then relabel the
   session `#<number> · PR #<pr> <short-description>`. Once the worktree is
   gone this label is the only record of which pull request the session owns.
   - **Enable auto-merge at once**: `gh pr merge <pr> --auto --squash`, then
     confirm `gh pr view <pr> --json autoMergeRequest` is not `null`.
   - Skip it only for a draft, or a pull request the user asked to hold.
   - Auto-merge does not replace step 9: a branch that falls `BEHIND` still
     needs a rebase and push before it can merge.
5. **PR body**: `Closes #<number>` on its own line, and the scenarios it
   satisfies. Built something the specification does not describe? Either fix
   the specification or the change exceeded its scope.
6. **Screenshots** for any user-visible web change:
   - captured from the real running app by a browser tool, not a mockup;
   - a new page or component: an after shot; a changed one: before and after;
   - in the project's primary language;
   - committed in the repository, named `before-*` / `after-*`, and referenced
     from the body or a comment, not only pasted inline;
   - referenced by a `raw.githubusercontent.com` URL pinned to the adding
     commit. A relative path or `github.com/…/blob/…` URL renders broken;
   - before reporting, check each URL answers `image/png`:
     `curl -sI <url> | grep -i content-type`.
7. **Prove it starts.** After pushing, start the app from the worktree and wait
   until it answers.
8. **Tear down at once**: stop what step 7 started, then `git worktree remove`.
   Containers belong to the checkout that started them; removing the worktree
   first leaves them holding the ports. Never leave a worktree behind — open,
   failing, or merged. Watching checks, reading logs, and commenting work from
   the primary checkout via `gh`.
9. **Watch required checks** from the primary checkout.
   - A check fails: recreate the worktree on the same branch (no `-b`):
     `git fetch origin issue-<number>/<short-description> && git worktree add .claude/worktrees/issue-<number>/<short-description> issue-<number>/<short-description>`.
     Fix, commit, rebase, push each fix, and repeat steps 7 and 8.
   - Green: fetch and confirm `gh pr view <pr> --json mergeStateStatus` is not
     `BEHIND` — `main` moves while checks run. Behind? Rebase, push, watch
     again.
   - Finish only when checks are green on a current branch and no worktree
     remains, then relabel the session `✓ #<number> · PR #<pr> green`.

## Path filters

- A CI job's path filter lists every input that job reads, not only the
  directory its code lives in. A missing input skips the job, and GitHub counts
  a skipped job as passing.
- A change to an input a skipped job would have read runs that job's suite
  locally before the pull request.

## Workflows that push

- **Onto a pull request's branch**: GitHub filters `paths` per push, so one
  bot's push often starts no run of another bot. A workflow pushing onto a pull
  request's branch replays its commit on top when the newer push touched none
  of its trigger files, never a bare `git push` that loses the race.
- **With a token on the remote URL**: check out with
  `persist-credentials: false`. The persisted `GITHUB_TOKEN` header outranks
  the URL, so the push authenticates as `github-actions[bot]` and its CI waits
  for maintainer approval.
