---
status: accepted
date: 2026-09-19
decision-makers: Chase Florell
keywords: i18n, DeepL, gitignore, ADR-0021, ADR-0054
---

# ADR-0056 — `locales/fr-CA.json` and `locales/fr-CA.meta.json` are tracked files, not gitignored

**Status:** Accepted. Narrows
[ADR-0054](ADR-0054-local-build-stubs-missing-translations.md), whose
`.gitignore` consequence rested on a premise that was never true for this
repository.

## Context

[ADR-0054](ADR-0054-local-build-stubs-missing-translations.md) added
`locales/fr-CA.json` and `locales/fr-CA.meta.json` to `.gitignore`, reasoning
that ".gitignore has no effect on a path once it is genuinely tracked (a
merge from main still updates it normally)" — i.e. that these files were
already committed, and the entry only stopped a developer's local `#`-stub
from being swept up by an accidental `git add -A`.

That premise was false: neither file had ever been committed to this
repository. `git log --all -- locales/fr-CA.json` returns nothing.

The consequence, discovered by running the real `i18n-translate.yml`
workflow end to end for the first time (after `DEEPL_API_KEY` was added,
issue #144/#145): the workflow's own step does
`git add locales/fr-CA.json locales/fr-CA.meta.json` before committing to
`chore/fr-CA-translations`. Against an untracked, gitignored path, that `git
add` fails outright:

    The following paths are ignored by one of your .gitignore files:
    locales/fr-CA.json
    locales/fr-CA.meta.json
    hint: Use -f if you really want to add them.

Under the step's `set -euo pipefail`, this is a hard failure. DeepL is still
called and billed for every run — the translate step logs "N translated"
before the failure — but the result is discarded every time, because the
job dies before opening or updating the pull request that would let a human
ever see it. The `#`-stub check and every other guard in ADR-0054 are
unaffected by this; only the CI-to-PR path was broken.

## Decision

Remove `locales/fr-CA.json` and `locales/fr-CA.meta.json` from `.gitignore`.
They are ordinary tracked files, generated only by CI and only ever changed
by the `chore/fr-CA-translations` pull request a human reviews and merges,
exactly as [ADR-0021](ADR-0021-ci-translation-opens-a-pull-request.md)
describes. Nothing else about ADR-0054 changes: the `#`-stub mechanism, the
`predev`/`prebuild` hooks, and `verifyLocales`'s rejection of a committed
`#`-prefixed value or `provider: "stub"` stamp all still hold, and none of
them depended on the gitignore entry to do their job — a developer's local
stub now simply shows up as an ordinary uncommitted diff in `git status`,
which is easier to notice, not harder.

## Why this choice

**The two files were never at risk of an accidental `git add -A`, because
they never existed to be added.** The gitignore entry protected against a
scenario that could not occur until the workflow ran for the first time and
tried to create them — at which point the same entry blocked the one thing
that was supposed to happen.

**No alternative preserves the ignore.** Passing `git add -f` in the
workflow would work around the symptom but leave the false premise and the
`.gitignore` entry in place for the next person to trust.

## Consequences

- `.gitignore`: the `locales/fr-CA.json` / `locales/fr-CA.meta.json` entry
  removed.
- The next successful run of `i18n-translate.yml` commits these files for
  the first time, opening `chore/fr-CA-translations` as ADR-0021 describes.
- [ADR-0054](ADR-0054-local-build-stubs-missing-translations.md)'s
  `.gitignore` consequence is narrowed by this ADR; its stub mechanism is
  unaffected.

## Related

- [ADR-0021](ADR-0021-ci-translation-opens-a-pull-request.md)
- [ADR-0054](ADR-0054-local-build-stubs-missing-translations.md)
