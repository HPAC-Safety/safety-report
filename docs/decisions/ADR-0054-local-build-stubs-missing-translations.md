---
title: The local build stubs missing translations; CI still does the actual translating
description: "Two separate mechanisms, kept deliberately apart by where they run: 1."
type: adr
status: accepted
date: 2026-09-19
decision-makers: Chase Florell
keywords: i18n, DeepL, translation, dev experience, ADR-0021
---

# ADR-0054 — The local build stubs missing translations; CI still does the actual translating

**Status:** Accepted; its `.gitignore` consequence is narrowed by
[ADR-0056](ADR-0056-fr-ca-locale-files-are-tracked-not-gitignored.md).
`locales/fr-CA.json` and `locales/fr-CA.meta.json` are tracked files, not
gitignored — the premise below that they were already committed was never
true. Everything else in this ADR (the `#`-stub mechanism, the
`predev`/`prebuild` hooks, `verifyLocales`'s checks) is unaffected.

## Context

[ADR-0021](ADR-0021-ci-translation-opens-a-pull-request.md) made French
generation CI-only, on a push to main, opening its own reviewed PR — never
on a pull request, so untrusted fork code can never trigger a paid model
call or slip in unreviewed French. That boundary is correct and stays: the
translation credential (`DEEPL_API_KEY`) must live only in CI, never on a
developer's machine.

The gap it leaves: a developer working locally, on a branch that adds new
English chrome keys (issue #140's homepage spike, for instance), sees every
new key fall back silently to English when they toggle to French — correct
behavior, but it gives no visible signal that a key is untranslated, and no
way to see the French layout while building a feature.

## Decision

Two separate mechanisms, kept deliberately apart by where they run:

1. **Locally, at build time — no credential, no API call.** `src/web`'s
   `dev` and `build` scripts gain a `pre`-hook running a new tool,
   `tools/stub-missing-translations.mjs`. It compares `locales/en-CA.json`
   and `locales/fr-CA.json` and, for any key present on one side but
   missing on the other, writes that key into the missing file with its
   source text prefixed `#` — e.g. an English-only key `nav.contact:
   "Contact"` gets a French entry `nav.contact: "#Contact"`. This runs both
   directions: a French-only key added directly gets the same `#`-prefixed
   treatment on the English side. The prefix is a visible, unambiguous
   "not yet translated" marker in the UI itself if one ever reaches it, not
   just a log line a developer might miss.

2. **In CI, on push to main — the only place the credential exists.** The
   existing `i18n-translate.yml` workflow and `tools/translate-locale.mjs
   --generate` are unchanged: they plan translation for any English key
   whose `fr-CA.meta.json` stamp is missing or stale, call DeepL once, and
   overwrite whatever was there — including a local `#`-stub, which has no
   meta stamp and is therefore always queued for real translation. CI's
   real output replaces the stub; the `#` never reaches a human reviewer's
   eyes as anything but a transient local artifact.

`tools/translate-locale.mjs --check` (the `i18n` CI job's verify step,
ADR-0021) gains one more rule: a committed locale value that still starts
with `#` fails the check, the same way a `provider: "stub"` stamp already
does. A `#`-prefixed placeholder must never merge to main — if `--check`
ever sees one, either the CI generation step didn't run for that key or
someone hand-committed a local stub.

**Narrowed by [ADR-0057](ADR-0057-same-repo-pull-requests-translate-in-pr.md)
and issue #207, for the pre-commit hook only.** "Must never merge to main"
is unchanged and CI still enforces it. But once ADR-0057 had
`i18n-translate.yml` commit the French straight onto a same-repo pull
request's branch, a `#` stub stopped being evidence of a mistake on a branch
and became a known, temporary state that a workflow resolves within seconds
of the push. **A stale `source_hash` is the same state by the same argument**
(#213): editing an existing English string is precisely what
`planTranslation` queues for re-translation, and with no DeepL credential
locally there is nothing the author can do about it either. The hook therefore reports both as a notice on a branch and still refuses
them on `main`, through `--allow-pending-translation`, which nothing in CI
passes. A stub in
`en-CA.json` stays fatal everywhere: no workflow writes English, so that one
is always the author's to fix.

`locales/fr-CA.json` and `locales/fr-CA.meta.json` were added to
`.gitignore` here, on the premise that they were already committed and the
entry would only stop a developer's local, stub-filled copy from being
accidentally `git add -A`'d. That premise was false — neither file had ever
been committed — and the entry instead blocked CI's own commit of the real
files. See [ADR-0056](ADR-0056-fr-ca-locale-files-are-tracked-not-gitignored.md),
which removes the entry; they are ordinary tracked files.

## Why this choice

**The credential boundary is exactly where ADR-0021 already drew it.**
Nothing about local stubbing needs a translation API — it is a pure,
local, deterministic text transform. The actual translating still only
ever happens in CI, with the one credential that lives there.

**A visible marker beats a silent fallback.** English-fallback-with-no-
signal is correct today but easy to forget about; `#Contact` in the French
UI is impossible to miss, on purpose, and disappears the moment CI does its
job.

**Reuses the generator's own change-detection.** `planTranslation` already
queues any key with no matching `source_hash` stamp — a `#`-stub has none,
so it needs no special-casing in `translate-locale.mjs` beyond the new
`--check` guard.

## Alternatives

- **A developer-supplied `DEEPL_API_KEY` for local generation.** Rejected —
  considered and dropped in favor of this design: it would put the real
  credential on every contributor's machine, which is exactly the exposure
  ADR-0021 avoided by keeping generation CI-only.
- **Commit real French in every PR.** Rejected outright — reverses
  ADR-0021's actual security property for developer convenience that
  doesn't need it.
- **Log a warning instead of a visible `#` prefix.** Rejected: a build log
  is easy to miss; a visibly wrong string in the running app is not.

## Consequences

- New file: `tools/stub-missing-translations.mjs`.
- `src/web/package.json`: `predev`/`prebuild` scripts added.
- `tools/translate-locale.mjs`: `verifyLocales` rejects a `#`-prefixed
  committed value.
- `.gitignore`: `locales/fr-CA.json`, `locales/fr-CA.meta.json` added, later
  removed by [ADR-0056](ADR-0056-fr-ca-locale-files-are-tracked-not-gitignored.md).
- `features/web-localization-and-design/web-localization-and-design.feature`
  gains a scenario for this behavior.

## Related

- [ADR-0021](ADR-0021-ci-translation-opens-a-pull-request.md)
- [ADR-0022](ADR-0022-translation-provider-is-configuration.md)
