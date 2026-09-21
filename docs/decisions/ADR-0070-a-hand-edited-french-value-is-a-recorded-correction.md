---
status: accepted
date: 2026-09-21
decision-makers: Chase Florell
keywords: localization, translation, provenance, fr-CA, human correction, source of truth
---

# ADR-0070 — A hand-edited French value is a recorded correction

**Status:** Accepted. Narrows
[ADR-0021](ADR-0021-ci-translation-opens-a-pull-request.md) and
[ADR-0056](ADR-0056-fr-ca-locale-files-are-tracked-not-gitignored.md): English
remains the source of truth and `fr-CA.json` is still generated, but a hand
edit to it is now recorded rather than silently absorbed.

## Context

`fr-CA.meta.json` stamped a `source_hash` — a hash of the **English**. Nothing
hashed the French. That left a hole nobody could see:

- A human improves a French string while its English is unchanged.
- `verifyLocales` finds `source_hash` still matching and reports nothing.
- `planTranslation` puts the key in `unchanged`, so the edit survives.
- The stamp still reads `provider: "deepl:FR-CA:prefer_more"`.

So the edit persisted **under provenance claiming a machine wrote text a
person wrote**. Worse, the moment the English changed, `source_hash` went
stale, the key was queued for translation, and the human's wording was
overwritten with no record that it had ever existed.

"Never hand-edit `fr-CA.json`" is a sound rule and is not changing. But the
file is hand-editable in practice, and a rule enforced by nothing, whose
violation is invisible and whose punishment is silent data loss, is not a
rule. It is a trap.

## Decision

English stays the source of truth. A hand-edited French value is a **human
correction**: recorded, and never overwritten by a later run.

Each stamp gains a `target_hash` — a hash of the French as generated or
pinned. With both hashes, one key has four possible states:

| English | French | State | What happens |
|---|---|---|---|
| unchanged | unchanged | `current` | Nothing. |
| changed | unchanged | `stale` | Re-translated, as before. |
| unchanged | changed | `corrected` | Accepted. Re-stamped `provider: "human", reviewed: true`. Never sent to a translator again. |
| changed | changed | `conflicted` | **Fails loudly, naming the key.** Nothing is written. |

A correction does **not** drive an English re-translation. English is authored
by hand, and a machine translating French back into it would let generated
text become the source of truth, which is the opposite of this repository's
model.

A `conflicted` key stops both `--check` and `--generate`. It is not treated as
pending work, because no workflow can resolve it: a machine choosing between
two deliberate human edits is exactly how one of them disappears.

Glossary-pinned keys are unaffected. They are already never sent to a
translator and never overwritten, and `glossary.json` remains the one place a
machine does not touch.

### The keys that already existed

A stamp with no `target_hash` reports as `unknown`, and nothing new is
asserted about it. All 144 existing stamps were backfilled in this change by
hashing their current French — an assertion the repository already made
implicitly, since nothing had been hand-edited. That closes the hole for every
key immediately instead of one key at a time as each is next translated.

## Why

The alternative was to keep enforcing "never hand-edit" by hoping. The rule's
whole value is protecting generated output from drift, and it was protecting
nothing: drift was undetectable, and the only thing the rule reliably produced
was the silent loss of somebody's work.

Recording a correction costs one hash per key and makes both outcomes honest.
A human's wording survives, and the provenance says who wrote it.

Failing loudly on a conflict rather than picking a winner is the same
reasoning as the `#`-stub rule in
[ADR-0054](ADR-0054-local-build-stubs-missing-translations.md): the point of a
check is to stop something wrong from being invisible, not to be convenient.

## Alternatives

- **Make French co-authoritative and back-translate into English.** Rejected:
  it makes generated text a source of truth, and an English string edited by a
  machine from French is no longer wording anybody chose.
- **Overwrite the correction and let English always win.** Rejected: it is
  today's behaviour, and it discards deliberate human work silently.
- **Keep both and translate neither on a conflict.** Rejected: the two can
  then drift apart in meaning with nothing to catch it.
- **Require a marker to opt a French edit in.** Rejected: a rule that only
  works when remembered is the failure mode being fixed.

## Consequences

- `fr-CA.meta.json` gains a `target_hash` on every key.
- A correction is never machine-translated again, so its English can no longer
  be edited without a decision.
- `--generate` refuses to write anything while a conflict is unresolved.

## Related

- [ADR-0021](ADR-0021-ci-translation-opens-a-pull-request.md) — English as source of truth
- [ADR-0054](ADR-0054-local-build-stubs-missing-translations.md) — the `#` stub, and what a check is for
- [ADR-0056](ADR-0056-fr-ca-locale-files-are-tracked-not-gitignored.md) — `fr-CA.json` is tracked
- [ADR-0057](ADR-0057-same-repo-pull-requests-translate-in-pr.md) — translating on the branch
