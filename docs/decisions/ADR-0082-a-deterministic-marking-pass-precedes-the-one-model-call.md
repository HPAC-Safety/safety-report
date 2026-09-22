---
status: accepted
date: 2026-09-22
decision-makers: Chase Florell
keywords: privacy, anonymization, worker, deterministic marking, question bank
---

# ADR-0082 — A deterministic marking pass precedes the one model call

**Status:** Supersedes the "deterministic scrub, regex stages, scrub
vocabulary, markers" removal clause of
[ADR-0038](ADR-0038-question-privacy-and-llm-anonymization.md). ADR-0038's
privacy partition (`report_content` / `private_context`, immutable
per-question `IsPrivate`) is unchanged and still governs what reaches the
model.

## Context

ADR-0038 made anonymization exclusively an LLM responsibility: private
context is supplied only as recognition help, and the deterministic scrub
stage that preceded it was removed as duplicated, brittle work that still
could not resolve identifiers written only in prose.

That argument holds for identifiers the model has to infer from context. It
does not hold for the narrower case where a private answer's exact text — or
an obvious fragment of it, such as a first name lifted from a full name — is
reproduced verbatim in a non-private field. That case is a plain string
match, not a language-understanding problem, and leaving it entirely to model
judgment accepts avoidable variance on the easiest instances of the same
failure the LLM step exists to prevent.

## Decision

Before the Worker builds the prompt for the one model call, it runs a
deterministic marking pass over `report_content`:

- For every `private_context` field, build a set of match candidates: the
  field's whole value, plus each of its whitespace-separated word tokens.
  Tokens shorter than a minimum length, and tokens on a small curated
  stopword list (common words that collide with generic vocabulary), are
  discarded as candidates. Every private answer participates, regardless of
  its question type — a date or boolean rarely matches, but nothing is
  excluded by type.
- Matching is case-insensitive and whitespace-normalized.
- Candidates are matched against `report_content` values longest-first, so a
  whole-value match is not fragmented by its own tokens matching first.
- Each match is replaced with `[PRIVATE:<question-key>]`, using the stable
  key of the private question the match came from — not a generic
  placeholder — so the model has a deterministic signal for which role or
  generalization applies, instead of inferring it purely from surrounding
  narrative.

`private_context` is still sent to the model in full, unchanged, alongside
the marked `report_content`. Marking catches exact and near-exact string
leaks; `private_context` remains the model's backstop for anything the
marking pass cannot catch by string matching — paraphrases, misspellings, a
partial mention below the token-length threshold, a translated form.

This is a pre-processing step inside the same Worker attempt, not a new
pipeline stage. It runs before, and only before, the one model call; it does
not add a call, a service, or a retry path of its own. The prompt (v2) is
updated to instruct the model to resolve every `[PRIVATE:<question-key>]`
marker it sees into the correct role phrase or a safe generalization, and to
never let the marker itself, or a fragment of the value it stood in for,
reach either summary text.

## Consequences

- The easiest class of identity leak — the reporter's own words repeating an
  exact private value — is caught by a plain string match instead of resting
  entirely on model behavior, while the model still does the contextual work
  ADR-0038 assigned it for everything the marker pass misses.
- The stopword list and minimum token length are implementation constants,
  tuned in code and tests, not enumerated here — they are expected to grow as
  false positives are found, without requiring a new ADR.
- `features/ai-anonymization/ai-anonymization.feature`'s "no deterministic
  text scrubber... runs" scenario is reworded: it now asserts no *second
  model call*, PII-audit call, or translation call runs, and adds scenarios
  for the marking pass itself.
- Marking never removes safety-relevant content — it only ever replaces a
  private-value match with a marker naming its source question, leaving the
  rest of the sentence intact for the model to work with.

## Alternatives rejected

**Token matching restricted to free-text/type-ahead question types.** Adds a
type-based exclusion list to maintain and reason about for a marginal
precision gain; a select/date/boolean value rarely produces a false-positive
match in prose, so the added rule does not pay for itself.

**Whole-value matching only, no token matching.** Misses the common case of a
first name alone standing in for a full name already captured as private
context — exactly the kind of leak this ADR exists to catch deterministically
rather than leaving to model inference.

**A generic marker (e.g. `[REDACTED]`) instead of a question-key-tagged one.**
Loses the deterministic signal for which role phrase applies, pushing that
inference back onto the model for every match — the same variance this ADR
is narrowing.

**Marking replaces `private_context` instead of supplementing it.** Marking
only catches literal string matches; dropping `private_context` would remove
the model's only means of recognizing a paraphrased, misspelled, or otherwise
non-literal repetition of the same identity.

## Supersedes

- The "deterministic scrub, regex stages, scrub vocabulary, markers, and
  their tests are removed" clause of
  [ADR-0038](ADR-0038-question-privacy-and-llm-anonymization.md).
