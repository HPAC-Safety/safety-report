---
title: Lessons
description: What a lesson records, when to write one, and the index of the lessons written so far.
type: guide
---

# Lessons

A bug is usually a specification defect wearing implementation clothes. The code
did something nobody wanted, which means either no claim covered the case or a
claim covered it wrongly. Fixing only the code leaves that gap exactly where it
was, and the gap is what produces the same bug again next quarter
([ADR-0085](../decisions/ADR-0085-a-lesson-flows-upstream-into-the-specification.md)).

A lesson records what the specification should have said. It is read on a design
pass, alongside `/features` and the ADRs — not looked up after something breaks.

## When to write one

A bug fix that reveals a specification gap writes a lesson, in the same pull
request as the fix. A fix that reveals nothing — a typo, a dependency bump, a
rename — does not.

A lesson does not replace an ADR or a scenario. A fix that also makes a durable
architectural decision still gets an ADR, and the lesson cites it. A fix that
changes user-facing behavior still gets a scenario, and the lesson cites its
claim ID.

## Two kinds, and what each one owes

**A product lesson** is about what the system does. Its remedy is a claim: a
scenario is added or corrected, and the lesson cites the claim ID that now
proves it. It does not change a skill — restating product behavior in a skill
creates a second place for it to drift from `/features`.

**A process lesson** is about how we work: tooling, CI, hooks, conventions, the
delivery workflow, what an agent is expected to do. No scenario can prove it,
so it has no claim to add. Its remedy is a **skill** — the one that would have
prevented it — updated in the same pull request as the lesson. The skill states
the general rule; the lesson keeps the incident.

An agent reads the skills before it starts. It does not read this index looking
for a mistake it has not made yet, which is why a process lesson that stops
here is a story rather than a rule
([ADR-0085](../decisions/ADR-0085-a-lesson-flows-upstream-into-the-specification.md)).

Some lessons are genuinely one-off and produce neither. Say so in the lesson
rather than inventing a rule to hang on a skill.

## The shape

One file per lesson, `NNNN-kebab-slug.md`, numbered in the order they are
written. Frontmatter carries the date, the issue, and a status. The body states
four things and stops:

- **Symptom** — what was observed, in the terms it was observed in.
- **Root cause** — why it happened, not which line was edited.
- **Spec delta** — what changed upstream: the claim added or corrected, the ADR
  written, the guard moved.
- **Scenario** — the claim ID that now proves it, or an explicit statement that
  no scenario can.
- **Skill** — for a process lesson, the skill that now carries the general rule
  and what it says. A product lesson writes "none — the claim is the remedy."


A lesson that turns out to be wrong is corrected in place, or marked superseded
in its frontmatter status, like an ADR.

## Index

| Lesson | What it cost us | Remedy |
|---|---|---|
| [0001 — A guard that lives only in CI is not a guard](0001-a-guard-that-lives-only-in-ci-is-not-a-guard.md) | 38 failing tests on every fresh clone, invisible to CI | `coding-conventions`, `hpac-safety-conventions` |
| [0002 — Provenance that hashes only one side of a pair](0002-provenance-that-hashes-only-one-side-of-a-pair.md) | Hand-written French silently overwritten by the translator | `localize-hpac-app` |
| [0003 — A number is claimed the moment someone else merges](0003-a-number-is-claimed-the-moment-someone-else-merges.md) | Three ADR numbers taken out from under a branch in one afternoon | `deliver-change`, `deliver-hpac-change` |
| [0004 — A rule read once is not a rule checked again](0004-a-rule-read-once-is-not-a-rule-checked-again.md) | Eleven files edited directly on `main` on the second issue of a session, after the worktree rule was followed correctly on the first | `deliver-change`, `deliver-hpac-change` |
| [0005 — An outcome computed and never recorded](0005-an-outcome-computed-and-never-recorded.md) | Every submitted image/video stayed unviewable forever; no reviewer endpoint could have worked | `REQ-MED-010` |
| [0006 — A choice code nobody could supply, and a reporter choice nobody recorded](0006-an-internal-identifier-leaked-into-the-authoring-screen.md) | Administrators asked for an option code they could not know; every report naming an unlisted type-ahead value was refused | `REQ-QB-092`, `REQ-QB-095` |
| [0007 — A question key shown to the person who cannot choose it](0007-a-question-key-shown-to-the-person-who-cannot-choose-it.md) | The question editor showed the stable question key as a field an administrator had to invent or appeared able to change | `REQ-QB-087`, `REQ-QB-096` |
| [0008 — Containers outlive the worktree that started them](0008-containers-outlive-the-worktree-that-started-them.md) | `./dev-up.sh` timed out because a removed worktree's containers still held the dev ports | `deliver-change`, `deliver-hpac-change` |
| [0009 — A choice list nobody could see](0009-a-choice-list-nobody-could-see.md) | The "Where" question's choices never appeared on the choice-lists page, because choices had two homes | `REQ-QB-099`, `REQ-QB-100` |
| [0010 — A coverage gate found in CI, not before the pull request](0010-a-coverage-gate-found-in-ci-not-before-the-pull-request.md) | A pull request with every test green failed CI's branch-coverage ratchet | `deliver-change`, `deliver-hpac-change`, `tools/coverage-check.sh` |
| [0011 — A branch rebased before its push is behind by the time it is green](0011-a-branch-rebased-before-its-push-is-behind-by-the-time-it-is-green.md) | A pull request reported green was already behind `main`, which moved while its checks ran | `deliver-change`, `deliver-hpac-change` |
| [0012 — Upload translated as download](0012-upload-translated-as-download.md) | Nine French attachment strings told reporters their files were being downloaded | `REQ-WLD-026`, `REQ-WLD-027` |
| [0013 — A generated file with a whole-tree total conflicts with every branch](0013-a-generated-file-with-a-whole-tree-total-conflicts-with-every-branch.md) | Pull requests kept conflicting on `docs/traceability.md`, and `main` went stale after squash merges | `coding-conventions`, `hpac-safety-conventions` |
| [0014 — A local image cache hides a withdrawn upstream](0014-a-local-image-cache-hides-a-withdrawn-upstream.md) | `main` and every pull request failed to pull the test S3 server while developer machines passed on a cached copy | `test-from-scenarios`, `test-hpac-safety` |
| [0015 — A storage format shown as a display format](0015-a-storage-format-shown-as-a-display-format.md) | Reviewers and reporters read dates and times as raw ISO 8601, with a "translation" repeating it | `REQ-MOD-075`, `REQ-MOD-076`, `REQ-SUB-068` |
| [0016 — A push filtered by paths starts no run to supersede yours](0016-a-push-filtered-by-paths-starts-no-run-to-supersede-yours.md) | The traceability bot lost a push race to the translation bot on PR #392, and `docs` stayed red until the matrix was regenerated by hand | `deliver-change`, `deliver-hpac-change` |
| [0017 — A screenshot linked by a page URL renders broken](0017-a-screenshot-linked-by-a-page-url-renders-broken.md) | Every screenshot in PR #414's description, and earlier ones with relative paths, rendered as a broken image | `deliver-change`, `deliver-hpac-change` |
| [0018 — A persisted checkout token outranks the PAT on the remote](0018-a-persisted-checkout-token-outranks-the-pat-on-the-remote.md) | The translation bot's push authenticated as `GITHUB_TOKEN`, so the CI runs it started waited for a maintainer to approve them | `deliver-change`, `deliver-hpac-change` |
| [0019 — A language code the provider never offered](0019-a-language-code-the-provider-never-offered.md) | Every French-to-English machine translation failed with a 400, because DeepL has no `EN-CA` and the unit test asserted it | `REQ-WLD-028`, `REQ-WLD-029`, `test-from-scenarios`, `test-hpac-safety` |
| [0020 — A copy change that ran no browser test](0020-a-copy-change-that-ran-no-browser-test.md) | A French-strings-only pull request merged with e2e skipped, and main's French review-page test broke, because CI's path filters left out `locales/` | `REQ-MOD-075`, `deliver-change`, `deliver-hpac-change` |
| [0021 — A consent question found by a key it was never seeded under](0021-a-consent-question-found-by-a-key-it-was-never-seeded-under.md) | Every seeded database sent the publication-consent answer to the model, because the Worker matched consent by a key the seed never used | `REQ-AI-009`, `REQ-QB-027`, `test-from-scenarios`, `test-hpac-safety` |
| [0022 — A closed list kept where the author never looks](0022-a-closed-list-kept-where-the-author-never-looks.md) | A pull request guessed the exemption category `copy` and failed `feature-coverage`, because the closed list lived only in ADR-0090 and the tool | `deliver-hpac-change` |
| [0023 — A rule the template never asks for](0023-a-rule-the-template-never-asks-for.md) | Two web UI pull requests reached review without their before or dark-mode screenshots, because the template never asked and no check refused | `deliver-change`, `deliver-hpac-change` |
