---
title: Pull request template
description: The checklist and sections every pull request in this repository opens with.
type: template
---

## What changed

<!-- One or two sentences. The PR title becomes the squash commit message. -->

## Why

<!-- Every PR closes a focused issue. -->

Closes #

## Specification delta

<!--
What changed upstream, and what this satisfies. Name the scenarios, the
specification pages, and the boundary this change respects. If it built
something no scenario describes, either the specification was incomplete —
fix it here — or the change exceeded its scope.

A change that alters no behavior may skip the scenario only by citing the
claims it leaves standing (ADR-0090). Write these two lines at the start of a
line, outside this comment:

    No .feature scenario needed: <category> — <what changed, and why no behavior did>
    Claims preserved: REQ-XXX-000, REQ-YYY-000

The category is one of these, and nothing else:

- refactor — behavior is unchanged; the code that produces it moved
- styling — appearance only, with no change to what the page does
- dependency — a package or lock version moved
- test-only — tests changed and no production code did
- build — build, packaging, or tooling configuration
- revert — an earlier change is being undone
- docs — documentation only
-->

## Screenshots

<!--
Required when this changes a rendered web file (a .tsx or .css under
src/web/src, not a test). The screenshots check fails the pull request
otherwise (ADR-0142).

A changed page or component: a before shot, taken from a build of origin/main,
and an after shot. A new one: an after shot. Commit them under
docs/screenshots/<dir>/, named before-* and after-*, and show each as an image
linked by a raw URL pinned to the commit that added it:

    ![after](https://raw.githubusercontent.com/<owner>/<repo>/<sha>/docs/screenshots/<dir>/after-<name>.png)

A web change with nothing visible (a refactor, a test-only change, a
non-rendering hook) writes this line instead, at the start of a line, outside
this comment:

    No screenshot needed: <what changed, and why nothing on screen did>
-->

## Verification

<!-- Commands run, tests added, and manual checks actually performed. -->

## Privacy and specification

- [ ] Product behavior matches `/features`, or the affected specification pages are updated here
- [ ] The scenario was written or amended before the implementation, and nothing here is untraced to one
- [ ] If this claims `No .feature scenario needed:`, it names one of the categories listed under **Specification delta** and the claim IDs the change leaves standing (ADR-0090)
- [ ] Report/question/model/attachment/auth/publication changes have a focused boundary test
- [ ] Runtime summarization still uses one prompt and one model call per attempt
- [ ] Documents remain private and are not anonymized, parsed, sent to AI, inline-rendered, or published
- [ ] No credential, report content, client filename, prompt, or model response is logged

## Repository checks

- [ ] UI copy is localized and English/French catalogue keys remain in parity
- [ ] .NET assertions use Shouldly and tests use Given/When/Then structure
- [ ] A rendered web change links before and after screenshots (after only for a new page), or says `No screenshot needed:` and why
- [ ] Generated files were regenerated with their owning tool
- [ ] Every markdown file added or changed declares its title, description, and type
- [ ] Documentation and issue acceptance criteria were updated where needed
