# ADR-0027 — The deterministic scrub is a closed chain over labelled fields

**Status:** Accepted
**Date:** 2026-08-22

## Context

[ADR-0003](ADR-0003-anonymization-pipeline.md) fixed the shape of the pipeline:
five stages, deterministic first. It did not say how stage 1 is built, and three
questions had to be answered before any of it could be written.

**What does the scrub receive?** A single blob of text is the obvious answer and
the wrong one. Half the rules are about *structured* answers — drop the phone
field, generalize the `Where:` field — and a blob has already thrown away which
part of it was which. Matching on label text instead (`"Where:"`) fails on the
first reworded question and on every report filed in French, because the question
set is data and its wording lives in the database
([ADR-0016](ADR-0016-data-driven-question-bank.md)).

**What is "a region"?** `docs/anonymization-policy.md` says "Province is kept;
the site is not", and the form asks for the province in its own dropdown, next to
the free-text `Where:`. There is no other geographic vocabulary in this system.

**How do the rules compose?** Eight categories of identifier, each with its own
rule, several of which interfere with each other if run in the wrong order.

## Decision

**The scrub takes labelled fields, not text.** A `ScrubRequest` is a list of
`ScrubField(Kind, Label, Value)` plus the province. `ScrubFieldKind` says how a
field must be *handled*; the label is carried through untouched and never matched
against. The worker maps a `Report` onto this, because it is the thing that knows
about `QuestionRole` and persistence.

**`ScrubFieldKind` is not `SensitivityTier`.** The tier of a field is a property
of the question and answers "who may see this". The kind answers "what does the
scrub do with it", and the two do not line up: a launch site and a manufacturer
are both Internal, and one is generalized to a province while the other is
discarded. The default kind is `Other` — kept, but passed through every
identifier stage — so an unclassified field is scrubbed rather than dropped.

**The region is the province, and nothing finer.** It comes from the reporter's
own structured province answer, never from the site name: deriving "British
Columbia" from "Mount Seven" is inferring a location rather than reading one, and
it is the same class of mistake as inferring a certification class from a model
name (invariant 2). With no province answered, the location field is dropped
outright — "when in doubt, redact".

**The stages are a chain of responsibility, and the chain is closed.**
`ScrubStage` and every stage are `internal`; the chain is assembled inside
`DeterministicScrub` and nowhere else. There is no options object, no stage
registry, and no way to construct a scrub with the email stage missing. Order is
fixed and load bearing: structured answers, email, URL, member number, phone,
names, places and aircraft.

**Membership identifiers are matched on the word, not on a digit shape.** HPAC
publishes no member-number format, so there is nothing to match. A bare run of
digits in a flying report is far more likely to be an altitude, and a rule that
stripped every number would take the safety lesson with it. `HPAC #48213`,
`member number 48213`, and `member no. 48213` are caught; a bare `48213` in prose
is not, and the structured member-number field is dropped outright regardless.

**Token matching is accent-insensitive, and names split on hyphens and
apostrophes.** A reporter who types "Renée" into the name field and "Renee" three
paragraphs down has not stopped being identifiable, and in a bilingual system
that spelling drift is the norm. Every letter of a harvested token becomes the
class of every letter sharing its unaccented base, built from Unicode rather than
from a hand-written table. Names also split on hyphens and apostrophes, or half
of "Sarah-Jane" walks straight through.

The minimum sub-token lengths are a judgement and worth stating: **three
characters for a name, four for a place or an aircraft.** Two would take the
French name particles — "de", "la", "du", "le" — out of every French narrative
the system ever scrubs; three-letter parts of a place or a brand are
overwhelmingly ordinary words, and deleting "air" from a flying report deletes
the report. The whole multi-word answer is always matched regardless of length,
and so is the surname, which is the part that identifies somebody.

**Removed identifiers with no natural role leave a `[removed]` marker.** It keeps
the sentence grammatical for stage 2 and it is visible to a reviewer, who can
tell "something was taken out here" from "the reporter never said". It carries no
locale, which is why it is a bracketed token rather than a word. Names are the
exception and get a role word — [ADR-0028](ADR-0028-role-words-in-place-of-names.md).

## Consequences

- `HpacSafety.Core` keeps zero package references. The patterns are
  `[GeneratedRegex]`, which the SDK provides, so there is nothing to add. A test
  fails the day that stops being true.
- The whole stage is provable in a plain unit test: no database, no network, no
  model, no clock, no configuration.
- Two golden-file cases assert what must **survive** — an altitude, a
  certification class, "de la vallée" in a French narrative. They are as load
  bearing as the absence assertions: a scrub that deletes everything passes every
  absence assertion ever written.
- Every pattern carries a 250 ms match timeout. This runs over text a member of
  the public typed into a form, and a pattern that can be made to backtrack for
  minutes is a denial of service with a friendly face.
- **Over-redaction is the accepted failure mode.** The phone rule will sometimes
  take a ten-digit number that was not a phone; the URL rule will sometimes take
  a typo with no space after a full stop. A vague summary is recoverable.
- **A site that appears only in the narrative is not caught.** Stage 1 finds what
  matches a pattern or what the reporter also typed into a structured answer.
  That gap is why stages 3 and 5 exist and why a human approves every
  publication. It must not be closed by shipping a list of Canadian site names.
- Adding a category means adding one stage and one golden-file case. It does not
  mean touching the other seven.

## Alternatives rejected

**Scrub a text blob.** Simplest signature, and it cannot implement half the
rules, because "drop the contact fields outright" needs to know which text was a
contact field.

**Match on the field label.** Breaks the first time an administrator rewords the
question, and breaks immediately for French reports. The question set is data;
its wording is not an API.

**Reuse `SensitivityTier` as the handling rule.** Tempting — one enum instead of
two — and wrong. Restricted/Internal/Publishable does not distinguish "discard"
from "generalize", and overloading it would have meant either a wrong result for
the launch site or a fourth tier that is not a tier.

**A public, configurable chain.** Would have made the stages easy to unit-test
individually and would have created an extension point whose only purpose is to
let a caller run the pipeline with a stage missing. The golden-file suite tests
the assembled chain, which is the thing that actually runs.

**A `[redacted]` marker for everything, names included.** Rejected in
[ADR-0028](ADR-0028-role-words-in-place-of-names.md).

**Strip every run of five or more digits.** Would catch an unlabelled member
number. It would also delete altitudes, airspeeds, and glide ratios — the
content of the report. The audit stages are the right place for that residual
risk.

## Related

- [ADR-0003](ADR-0003-anonymization-pipeline.md), [ADR-0016](ADR-0016-data-driven-question-bank.md),
  [ADR-0018](ADR-0018-feature-folders-in-core.md), [ADR-0028](ADR-0028-role-words-in-place-of-names.md)
- `src/HpacSafety.Core/Features/Anonymization/README.md`
- `docs/anonymization-policy.md`, `docs/data-handling.md`
