# ADR-0021 — The CI translation job opens a pull request, and never translates on one

**Status:** Accepted

Extends [ADR-0007](ADR-0007-localization.md), which decided that French is
generated in CI. This one decides *how* — the change detection, the write path,
and the split between what runs on `main` and what runs on a pull request.
The choice of provider is [ADR-0022](ADR-0022-translation-provider-is-configuration.md).

## Context

`locales/en-CA.json` is the source of truth for every piece of UI chrome.
`locales/fr-CA.json` is generated from it. Three things had to be decided before
any of that could run in GitHub Actions:

1. How a run knows what to translate.
2. Where the generated French lands.
3. What happens when the workflow is triggered by a fork's pull request.

The third is not hypothetical. This repository is public and takes fork pull
requests, and `ci.yml` deliberately runs fork-authored code.

## Decision

### Change detection is a content hash per key

`locales/fr-CA.meta.json` stores, per key, the SHA-256 of the English string its
French was made from, alongside the provider and a `reviewed` flag:

```json
{
  "form.submit": {
    "source_hash": "9f86d081…",
    "provider": "chat-completions:vendor/a-model",
    "reviewed": false
  }
}
```

A key is stale exactly when that hash no longer matches the English. Editing one
label re-translates one label. Everything else is left byte-for-byte alone.

### The job opens a pull request; it never pushes to `main`

A human reads the French before it ships. The branch is `chore/fr-CA-translations`,
rebuilt from `main` and force-pushed every run, so there is one open pull request
that updates rather than a queue of them.

### On `pull_request` it verifies and never generates

```mermaid
flowchart TD
    pr["pull_request<br/>(including forks)"] --> chk["ci.yml · i18n<br/>translate-locale.mjs --check"]
    chk --> reads["reads three files.<br/>constructs no translator."]
    push["push to main"] --> gen["i18n-translate.yml<br/>translate-locale.mjs --generate"]
    gen --> call["one batched provider call"]
    call --> prq["opens a pull request"]
    prq --> human["a human reads the French"]
    human --> main["main"]
```

`--check` and `--generate` are separate modes of `tools/translate-locale.mjs`,
and there is **no default mode** — running it with neither flag exits 2. A tool
whose no-argument behaviour is the side-effecting one gets invoked that way by
accident exactly once.

`--check` never reaches `createTranslator`. That is the property, not a comment:
a fork cannot make this repository spend an inference call, and cannot write a
generated locale file, because the code path that would do either is not in the
mode that fork-authored pull requests run.

### The offline stub cannot reach `main`

The test suite needs a translator that makes no network call, so
`tools/translator.mjs` ships a `stub` provider. It stamps `provider: "stub"` in
the provenance, and `--check` **fails** on that stamp. A development stand-in
must never weaken a guarantee the production one makes — so the stand-in's
output is rejected by the same gate that protects everything else, no matter
what anyone sets in a workflow.

### Glossary-pinned keys take their French from `locales/glossary.json`

They are never put in a request. They are stamped `provider: "glossary"`,
`reviewed: true` — because a human wrote that French by hand, and nothing in the
job decided it. An edit to the *English* of a pinned key does not change the
French; only HPAC may say when that wording changes.

## Alternatives

- **Push the French straight to `main`.** Simplest, and wrong. Machine French
  would reach a bilingual public with nobody having read it, and the "human
  reviews the French" step in ADR-0007 would exist only on paper. Being blocked
  by the `main` ruleset is a happy coincidence, not the reason.
- **A whole-file diff of `en-CA.json`.** Any commit touching the file
  re-translates every key. Four hundred re-translated strings to review because
  someone fixed a typo is a review nobody does, and the French churns for no
  reason.
- **A timestamp per key.** A fresh checkout has no history and every mtime is
  the checkout time, so it degrades to "translate everything" on the first run
  of every job.
- **A single `translated_at` for the whole file.** Same failure as the whole-file
  diff, plus it cannot express "this one key is stale".
- **Generate on `pull_request` too, so the French is in the same pull request
  as the English.** Tempting, and it is the reason this ADR exists. It requires
  either `pull_request_target` — which runs fork-authored code with a write
  token and secrets in scope — or a token that a fork can reach. Both hand an
  attacker inference spend and a write path to generated files, in exchange for
  saving one round trip. Rejected outright.
- **A second status-check context for the translation check.** A new context has
  to be added to `docs/github-ruleset.json` and made required before it means
  anything. The existing `i18n` job already owns locale correctness, so this is
  a step in it. See [ADR-0011](ADR-0011-ci-contexts-precede-their-checks.md).

## Consequences

- **A pull request opened with the built-in `GITHUB_TOKEN` triggers no
  workflows.** That is GitHub's loop protection, and it means CI never reports
  on the translation pull request, so its required checks sit unfulfilled and it
  cannot merge on its own.

  The workflow therefore uses `TRANSLATION_PR_TOKEN` when it is present, and
  falls back to `GITHUB_TOKEN` with a `::warning::` naming the consequence.
  Without that secret the pull request is still opened and still correct; it has
  to be nudged by hand (close and reopen, or push an empty commit) before it can
  merge.

  **`TRANSLATION_PR_TOKEN` is a fine-grained personal access token scoped to
  this repository alone**, with `Contents: Read and write` and
  `Pull requests: Read and write` and nothing else. Not a classic PAT: a classic
  token's `repo` scope covers every repository the user can reach, which is a
  blast radius wildly out of proportion to opening one translation pull request.

  A **GitHub App** installation token would be better still — it is not tied to
  a person, so it survives someone leaving, and it does not expire on a calendar.
  It is not required here because it means creating and installing an App for a
  single workflow. If the fine-grained token's expiry becomes an annoyance, that
  is the upgrade, and it needs no change to this workflow beyond the secret's
  contents.
- `locales/fr-CA.json` and `locales/fr-CA.meta.json` are generated files. Hand
  editing either is pointless — the next run overwrites it. A bad translation is
  fixed by changing the English or by pinning the term in `glossary.json`.
- `reviewed` is never set to `true` by the job. It is the record of what a human
  has actually read, and a flag a machine can set is not that record.
- The `i18n` job now fails a pull request that edits `en-CA.json` without
  regenerating French. That is intended: it is the same class of failure as
  editing a generated file, and the message says to merge and let the
  translation workflow open a pull request with the French in it.
- Until `locales/en-CA.json` exists (#8), both modes emit a `::notice::` and
  exit 0 rather than failing every pull request over a file this issue does not
  own.
