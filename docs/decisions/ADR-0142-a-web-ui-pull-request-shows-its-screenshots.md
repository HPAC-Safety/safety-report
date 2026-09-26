---
title: A web UI pull request shows its screenshots
description: A pull request that changes a rendered web file links before and after screenshots from its body, or says why it needs none, and a screenshots job in the Linked issue workflow refuses one that does neither.
type: adr
status: accepted
date: 2026-09-26
decision-makers: Chase Florell
keywords: screenshots, pull request template, pull request body, web UI, review, merge gate, linked-issue, exemption
---

# ADR-0142 — A web UI pull request shows its screenshots

**Status:** Accepted.

## Context

A reviewer judges a web UI change by looking at it. The `deliver-change` and
`deliver-hpac-change` skills already asked for an after shot of a new page and
a before and after pair for a changed one, but nothing an author fills in
asked for them:

- the pull request template had no Screenshots section;
- no check failed a `src/web` change that showed none.

So PR #525 reached review with only after shots, because a page screenshot
cannot show the browser's native datalist popup, and PR #524's first version
had light-mode shots only. A rule stated only in a skill holds only while the
author remembers it
([lesson 0023](../lessons/0023-a-rule-the-template-never-asks-for.md)).

## Decision

**A pull request that changes a rendered web file links its screenshots from
the body, or says why it needs none.**

- **Rendered file**: a `.tsx` or `.css` under `src/web/src/`, not a
  `*.test.*` file.
- **A screenshot** is an image committed under `docs/screenshots/` and linked
  by a `raw.githubusercontent.com` URL pinned to a commit
  ([lesson 0017](../lessons/0017-a-screenshot-linked-by-a-page-url-renders-broken.md)).
- **The exemption** is one line, for a change with nothing visible (a
  refactor, a test-only change, a non-rendering hook):

  ```text
  No screenshot needed: <what changed, and why nothing on screen did>
  ```

  Its reason carries at least four words, as the `feature-coverage`
  exemption's does
  ([ADR-0090](ADR-0090-an-exemption-cites-the-claims-it-preserves.md)).
- **The template** has a `## Screenshots` section that shows both forms, and a
  checkbox under "Repository checks".
- **The check** is `tools/pr-screenshots.mjs`, run by a `screenshots` job in
  `linked-issue.yml`, which re-runs on `edited` so fixing the body clears it.
  It reads only the body and the changed-file list. A screenshot counts only
  shown as an image (`![…](…)` or `<img src=…>`), linked to this repository
  when `GITHUB_REPOSITORY` is set. Text inside an HTML comment (an unclosed one
  runs to the end) or a fenced code block does not count, so the template's
  guidance left in a body satisfies nothing, and an exemption whose reason is
  the template's `<…>` placeholder is refused. It passes a pull request that
  touches no rendered file.
- **A local run checks something**: with no changed-file list the tool diffs
  the branch against `origin/main`, and with no body it fails with its usage
  ([ADR-0073](ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md)).
- **Review judges the content**: whether the pair is complete, the before shot
  comes from `origin/main`, a native popup was captured at OS level, and light
  and dark were both shot when the issue asks. The skills say how.
- `screenshots` joins the required status checks in
  `docs/github-ruleset.json`.

## Alternatives considered

- **Skill wording only.** Already in place, and skipped twice in a week.
- **Template section without a check.** A section left empty looks the same as
  one forgotten.
- **Visual regression or pixel diffs.** Out of scope: it proves a change is
  small, not that a reviewer saw it.
- **Judging the shots automatically** (a before and after pair, both themes).
  Rejected: the check would need to know what the change was for. It stays
  deterministic and leaves that to review.
- **Rule in the workflow's shell.** Rejected: a guard kept in CI configuration
  holds only in CI
  ([ADR-0073](ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md),
  [lesson 0001](../lessons/0001-a-guard-that-lives-only-in-ci-is-not-a-guard.md)).
  The tool can be run locally and is tested beside the others.

## Consequences

- A rendered web change cannot merge without a screenshot link or a reason.
- The ruleset change takes effect when an administrator applies
  `docs/github-ruleset.json`.
