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
correction**: recorded, and not overwritten by a later run.

Each stamp gains a `target_hash` — a hash of the French as generated or
pinned. Without it there is no way to tell a corrected value from a generated
one. The rule is then one sentence: **if the French moved, a human asserted it.**

| English | French | State | What happens |
|---|---|---|---|
| unchanged | unchanged | `current` | Nothing. |
| changed | unchanged | `stale` | Re-translated, as before. |
| unchanged | changed | `corrected` | Accepted. Re-stamped `provider: "human", reviewed: true`. |
| changed | changed | `corrected` | The same. Somebody editing both was editing both on purpose. |

A correction does **not** drive an English re-translation. English is authored
by hand, and a machine translating French back into it would let generated
text become the source of truth, which is the opposite of this repository's
model.

**Editing both languages at once is not a conflict.** An earlier draft of this
decision failed loudly there, on the reasoning that a machine must not choose
between two human edits. But there is no choosing to do: the author wrote both
sides, and stopping to ask would be second-guessing a deliberate act and
making the honest path — update the wording in both languages together — the
one that gets punished.

Editing **only** the English is the other half of the same sentence. It is a
request for a fresh translation, and it is the one way a recorded correction is
ever machine-translated again. That keeps English the source of truth and
leaves a way back for a key somebody corrected once and later reworded.

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

The point of a check is to stop something wrong from being invisible, not to
be strict for its own sake — the same reasoning as the `#`-stub rule in
[ADR-0054](ADR-0054-local-build-stubs-missing-translations.md). A hand edit
being silently overwritten is wrong and was invisible. Somebody updating both
languages together is neither.

## Alternatives

- **Make French co-authoritative and back-translate into English.** Rejected:
  it makes generated text a source of truth, and an English string edited by a
  machine from French is no longer wording anybody chose.
- **Overwrite the correction and let English always win.** Rejected: it is
  today's behaviour, and it discards deliberate human work silently.
- **Fail when both languages change at once.** Rejected after being written
  down: the author edited both on purpose, so there is nothing for a human to
  adjudicate, and the check would fire hardest on somebody doing the
  conscientious thing.
- **Require a marker to opt a French edit in.** Rejected: a rule that only
  works when remembered is the failure mode being fixed.

## Consequences

- `fr-CA.meta.json` gains a `target_hash` on every key.
- A correction survives every run except one that edits its English alone,
  which is the deliberate way to ask for it to be regenerated.
- A French edit is never silently discarded, and its provenance names who
  wrote it.

## Related

- [ADR-0021](ADR-0021-ci-translation-opens-a-pull-request.md) — English as source of truth
- [ADR-0054](ADR-0054-local-build-stubs-missing-translations.md) — the `#` stub, and what a check is for
- [ADR-0056](ADR-0056-fr-ca-locale-files-are-tracked-not-gitignored.md) — `fr-CA.json` is tracked
- [ADR-0057](ADR-0057-same-repo-pull-requests-translate-in-pr.md) — translating on the branch
