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
- [ ] Generated files were regenerated with their owning tool
- [ ] Every markdown file added or changed declares its title, description, and type
- [ ] Documentation and issue acceptance criteria were updated where needed
