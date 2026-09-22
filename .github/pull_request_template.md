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
-->

## Verification

<!-- Commands run, tests added, and manual checks actually performed. -->

## Privacy and specification

- [ ] Product behavior matches `/features`, or the affected specification pages are updated here
- [ ] The scenario was written or amended before the implementation, and nothing here is untraced to one
- [ ] Report/question/model/attachment/auth/publication changes have a focused boundary test
- [ ] Runtime summarization still uses one prompt and one model call per attempt
- [ ] Documents remain private and are not anonymized, parsed, sent to AI, inline-rendered, or published
- [ ] No credential, report content, client filename, prompt, or model response is logged

## Repository checks

- [ ] UI copy is localized and English/French catalogue keys remain in parity
- [ ] .NET assertions use Shouldly and tests use Given/When/Then structure
- [ ] Generated files were regenerated with their owning tool
- [ ] Documentation and issue acceptance criteria were updated where needed
