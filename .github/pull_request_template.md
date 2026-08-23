## What changed

<!-- One or two sentences. The PR title becomes the commit message — make it good. -->

## Why

<!--
Required. The `linked-issue` check fails without a closing keyword, because the
keyword in this body is the only thing that makes GitHub close the issue on
merge — the body becomes the squash commit message. "See #123" does not count.
If there is no issue behind this change, open one first.
-->

Closes #

## How it was verified

<!-- Commands run, tests added, what you actually checked. Not "should work". -->

---

### Does this change touch anonymization, PII handling, or credentials?

- [ ] **Yes** — redaction, prompts, summaries, uploads, logging, or auth
- [ ] No

If yes:

- [ ] A privacy/model-contract test proves the boundary or identifier claim
- [ ] The prompt version was bumped if prompt text changed
- [ ] `agents/anonymization-auditor.md` reviewed the diff
- [ ] No credential, request body, or report content is written to a log

### Checklist

- [ ] No hardcoded user-facing strings — new copy is a key in `locales/en-CA.json`
- [ ] Assertions use Shouldly; tests are named `Given_..._When_..._Then_...`
- [ ] Diagrams are Mermaid, not ASCII
- [ ] Generated files were regenerated, not hand-edited
- [ ] SOLID; any Gang of Four pattern used is named in the type name
- [ ] ADR written for any decision; requirements captured; affected READMEs updated
- [ ] **No assumptions.** Gaps in the requirements were asked about, not guessed.
      Anything that had to be assumed is written down below, marked, and in the code.
