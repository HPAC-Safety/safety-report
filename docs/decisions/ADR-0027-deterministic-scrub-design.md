# ADR-0027 — The deterministic scrub is a closed chain over labelled fields

**Status:** Superseded by [ADR-0038](ADR-0038-question-privacy-and-llm-anonymization.md)
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

**The zero value of `ScrubFieldKind` is the safe one.** A field whose handling
nobody decided is `Unclassified` and is **dropped**, exactly as a question nobody
has classified is Restricted until someone decides otherwise
(`docs/data-handling.md`). An ordinary kept-and-scrubbed answer is `FreeText`,
which somebody has to choose. The failure this prevents is concrete: an
administrator adds a "next of kin" question, the role mapping misses it, and a
fail-open default publishes the name in full.

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
stripped every number would take the safety lesson with it. The keyword may be
followed by a closed list of filler words — "my HPAC number **is** 48213", "HPAC
**ID** 48213" — because that is how people actually write it; the list is closed
rather than "any word", so "another club member landed at 1500 feet" keeps its
altitude.

**Every structured answer is also a token list for the free text, contact
details included.** `Location` and `AircraftIdentity` contribute their words;
`ContactDetail` and `MemberIdentifier` contribute their **whole value only**.
That closes the two holes no pattern can: an `@handle` or a street address, and a
member number written with no keyword near it — "I gave them my number, 48213".
Whole value only, because splitting an address into words would harvest "West"
and delete the wind direction from every sentence that mentions it.

**Token matching is accent-insensitive, and names split on hyphens and
apostrophes.** A reporter who types "Renée" into the name field and "Renee" three
paragraphs down has not stopped being identifiable, and in a bilingual system
that spelling drift is the norm. Every letter of a harvested token becomes the
class of every letter sharing its unaccented base, built from Unicode rather than
from a hand-written table. Names also split on hyphens and apostrophes, or half
of "Sarah-Jane" walks straight through.

The whitespace tolerance is bounded by how much of the token precedes the seam:
"Halcyon3" may find "Halcyon 3", but a model answer of "A1" may not find "a 1"
and delete a glide ratio out of the middle of a sentence. A run that short is not
a brand with a number after it.

Matching also tolerates a **trailing "s"** ("the Whitlocks" names the same family
as "Whitlock's", which an apostrophe already caught), **either Unicode
normalization form** (a browser may send "é" as one code point or as "e" plus a
combining acute, and the two are not equal byte for byte), and **whitespace that
moved** (a field reading "Halcyon 3" finds "Halcyon3"). None of these is exotic;
each was reaching the summarizer intact.

**The whole answer and its parts are gated differently, and getting this wrong
leaked twice in opposite directions.**

The whole answer is matched unless it is a single character. Short whole answers
are short surnames and short brands — Ng, Wu, Li, Vo, Ha, Cox, UP — and an
earlier version gated the whole answer on the same minimum as its parts, so a
three-letter launch name never became a matcher at all. One character is the
exception: that is an initial, it identifies nobody, and matching it would turn
every standalone "a" into "the pilot".

Within a longer answer, a part is matched from **two** characters for a name and
**four** for a place or an aircraft. Two, because "Sarah Ng" has to produce a
matcher for "Ng"; four for places, because two- and three-letter parts of a brand
are overwhelmingly ordinary words and deleting "air" from a flying report deletes
the report.

That leaves the French particle problem, which length cannot solve: "de" and
"la" must survive in "Marc de la Roche" while "Le" must not in "thanh le".

**The discriminator is structure, and it must never be capitalisation.** An
earlier version of this decision used `char.IsLower` on the answer, reasoning
that French particles are conventionally lower case and surnames capitalised.
That made redaction depend on the reporter's shift key, and it failed in both
directions: "thanh le" leaked the surname because it was typed on a phone, and
"Marc DE LA ROCHE" — surname in capitals, standard on official French-Canadian
forms — deleted "de la" from the narrative. Whether a real person is identifiable
must not turn on how they typed their own name.

A particle is instead a word that sits **between** other parts of the name: three
or more space-separated words, with at least one after it. "de" and "la" in
"Marc de la Roche" qualify; "le" in "thanh le" is the final word and therefore
the surname. It must also be a whole space-separated word — in "marie-le
tremblay" the hyphen makes "le" half of a compound given name, and a hyphen does
not depend on casing either.

The residual is accepted and stated: in a three-word name whose middle word is a
genuine standalone surname — "Marie Le Blanc" — that surname is matched as part
of the whole answer but not on its own. Narrower than the casing rule it
replaced, and it fails the same way regardless of how the name was typed. Two would take the
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

- `HpacSafety.Core` keeps **zero runtime dependencies**. The patterns are
  `[GeneratedRegex]`, which the SDK provides, so there is nothing to add. Note
  the precise claim: "zero package references" is shorthand and is not literally
  true, because `Directory.Build.props` injects `Roslynator.Analyzers` into every
  project. It is analyzer-only with `PrivateAssets=all` and contributes nothing
  to the compiled output; the test reads the assembly's actual references, which
  is the claim worth making.
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
- **Every token is matched in one pass.** Names and places were two stages, each
  looping its tokens and rewriting the value every time, which let a later token
  match inside text an earlier one had just written. A single alternation,
  longest branch first, cannot rescan its own output.

  **That fixes one of the two ways "the the reporter" arose, and it is worth
  being exact about which.** The reporter's own prose supplies the other: when a
  person's name is also an ordinary word — a reporter surnamed "Pilot" — the
  word "pilot" in their narrative is a token too, so "The pilot then threw the
  reserve" still puts an article in front of a role word that has one. A final
  pass drops the duplicate, keeping **the article the scrub wrote** and the
  reporter's capitalisation, because keeping the reporter's would put "la pilote"
  back into a French summary.

  What that pass cannot fix is the mislabelling underneath: the reporter meant
  the generic word, and the scrub cannot tell that from the surname, so the
  sentence now says "the reporter" where it meant the pilot. Redacting is still
  the right call — it might genuinely have been the surname — and the human
  reviewer is the backstop. Stated here rather than left for someone to discover.
- **A regex timeout never carries the report with it.**
  `RegexMatchTimeoutException` exposes the subject text on `Input`, and that
  text is the raw narrative. It is caught and replaced with a domain exception
  that names no content, so a timeout fails the report loudly — still reaching a
  human through `FailSummarization` — without the narrative riding along into a
  log.
- **The precise date and time never reach stage 2.** The reporter submits an
  actual date and clock time — the time stored encrypted as Restricted data —
  and only the coarse forms travel onward: month and year, and a `TimeOfDay`
  bucket. A precise time plus a province plus an aircraft type is another
  aggregation that identifies one person.

  **The bucket boundaries are deliberately not in this feature.** Stage 1 is
  handed the bucket the same way it is handed the province, and owns the
  invariant rather than the arithmetic: whatever the field said, only the bucket
  survives. Copying "morning is before 11:00" here would create a second
  definition, and a drifted boundary publishes the wrong time of day about a
  real crash. The mapping from a clock time to a bucket arrives with the schema
  work in [PR #62](https://github.com/HPAC-Safety/safety-report/pull/62) and is
  not on this branch; until it lands the caller supplies the bucket, and nothing
  in this feature changes when it does.

  The narrowing covers the **structured** date and time answers. A date or time
  written into the narrative passes through — free text is prose, and a rule
  that stripped every number from it would take the altitudes and airspeeds too.
  That residue is what stages 3 and 5 read for.

  The three empty-ish states stay distinct: `Unknown` means the form asked and
  the reporter did not answer, and is published; `NotAnswered` means the form has
  no time question, and the field is dropped; midnight is neither — it is a real
  answer that buckets as morning.
- **A site that appears only in the narrative is not caught.** Stage 1 finds what
  matches a pattern or what the reporter also typed into a structured answer.
  That gap is why stages 3 and 5 exist and why a human approves every
  publication. It must not be closed by shipping a list of Canadian site names.
- **The same is true of a social handle or a mailing address.** Nothing
  pattern-matches `@sarahflies`. It is removed when the reporter also gave it in
  a contact field, and not otherwise. `docs/anonymization-policy.md` says so
  where it lists what is always removed, because a policy that promises more
  than the code delivers is worse than one that admits the gap.
- Adding a category means adding one stage and one golden-file case. It does not
  mean touching the other seven.
- **Known follow-up, outside this change:** `ISummarizer`, `IPiiAuditor`, and
  `ITranslator` all take a `string`, so invariant 4 — raw text never reaches a
  model — is a convention held up by an XML comment rather than by the type
  system. `ScrubbedReport` has an `internal` constructor and is therefore an
  unforgeable proof token; typing those ports to take it would make the invariant
  a compile error. Those ports belong to `Reporting` and to the worker, so it is
  filed as [#61](https://github.com/HPAC-Safety/safety-report/issues/61) rather
  than done here.

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
