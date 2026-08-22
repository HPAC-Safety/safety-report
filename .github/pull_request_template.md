## What changed

<!-- One or two sentences. The PR title becomes the commit message — make it good. -->

## Why

<!-- Link the issue. If there isn't one, say what prompted this. -->

Closes #

## How it was verified

<!-- Commands run, tests added, what you actually checked. Not "should work". -->

---

### Does this change touch anonymization, PII handling, or credentials?

- [ ] **Yes** — redaction, prompts, summaries, uploads, logging, or auth
- [ ] No

If yes:

- [ ] A golden-file test asserts the specific identifier is absent
- [ ] The prompt version was bumped if prompt text changed
- [ ] `agents/anonymization-auditor.md` reviewed the diff
- [ ] No credential, request body, or report content is written to a log

### Checklist

- [ ] No hardcoded user-facing strings — new copy is a key in `locales/en-CA.json`
- [ ] Assertions use Shouldly; tests are named `Given_..._When_..._Then_...`
- [ ] Diagrams are Mermaid, not ASCII
- [ ] Generated files were regenerated, not hand-edited
