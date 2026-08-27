# ADR-0028 — A name in a narrative becomes a role word, not a placeholder

**Status:** Narrowed by the
[one-call specification](../../features/ai-anonymization/ai-anonymization.feature). Whole-identity role
replacement remains; the older multi-stage pipeline context is superseded.
**Date:** 2026-08-22

## Context

Reporters refer to themselves and to the pilot by name far more often than
anyone expects. "Sarah spiralled in from 200 feet." "I am filing this for Marc."
The structured name fields are dropped outright, but the narrative is the part
that has to survive: it is the safety lesson, and it is what stage 2 summarizes.

So the deterministic scrub has to put *something* where the name was, and what it
puts there is read by a language model a moment later.

## Decision

**Replace the name with the role word for the structured field it came from** —
"the pilot" for a name given in the pilot-in-command answer, "the reporter" for
one given in the reporter answer.

> Sarah spiralled in from 200 feet → the pilot spiralled in from 200 feet

**When the reporter is the pilot, one role word covers both.** If the same name
appears in both structured answers, it is replaced by "the pilot": a report about
a crash reads better, and no less safely, as "the pilot". The same tie-break
applies to a bare given name shared by two people with different surnames —
matching runs longest-token-first, so full names and surnames resolve correctly,
and only a genuinely ambiguous given name falls through to the pilot word. Which
role word appears is a readability question; the name being absent is the safety
question, and it is absent either way.

**Role words are supplied per language, by the caller.** `ScrubVocabulary` holds
the two words; the repository ships `EnglishCanada` — "the reporter", "the pilot"
— and `FrenchCanada` — **"le déclarant"**, **"le pilote"**. There is no locale
lookup inside the scrub, so adding a language is supplying two words rather than
editing the scrub.

`déclarant` is the standard term for someone filing an official report and
matches the institutional register of a safety authority. These are HPAC
terminology decided by a person, not machine output, and they must never be
re-translated.

**The French role words are always masculine, whoever was flying.** Uniformly,
without exception, and not varied to match the reporter or the pilot.

This is the whole point of the decision rather than a detail of it. English lets
you write "the pilot" and say nothing about the person; **French forces an
article**, so the scrub has to make a choice that English never surfaces. Varying
it would mean the output encodes the person's gender — and "la pilote" in a
fifty-person flying community narrows the field considerably. Matching the
article to the person would put back the exact fact the scrub had just removed,
in the one language where the grammar makes it unavoidable and therefore easy to
miss.

Masculine is the grammatical generic in French, so uniformity costs nothing
linguistically and buys the entire anonymising property.

**An elided article is contracted rather than left broken.** French writes
"d'Élise", and substituting the name alone yields "d'le pilote", which is not
French and hands stage 2 exactly the broken prose role words exist to avoid. The
scrub contracts "de" + "le" to "du". Only the article the vocabulary actually
carries is contracted; anything else is left as the reporter wrote it, because
guessing further at French grammar is how a deterministic pass starts inventing.

**What the uniform article does and does not cover.** It covers *the words the
scrub writes*. It does not cover the reporter's own prose: "elle s'est posée",
"sa voile", "elle était la pilote" pass through stage 1 untouched, and so does
"she broke her ankle" in English.

That is a boundary rather than a bug, and it is worth being exact about, because
a document that claimed otherwise would be promising a property the code does not
deliver. De-gendering free prose means rewriting sentences, which requires
understanding them — precisely what a deterministic stage must not attempt. A
regular expression that tried would mangle reports and still miss cases, and the
failure would be silent.

So the division is:

- **Stage 1** guarantees that a name becomes a role word and that the role word
  it writes never encodes gender. Tested, in both languages.
- **Stage 2** writes new prose and is instructed not to carry gender across; it
  is the only stage that can rephrase a sentence.
- **Stage 3** reads the generated summary and flags what identifies, gendered
  detail included, for the human who approves it.

Stage 1 promising more than that would be a false promise, and a false promise
about anonymisation is worse than a disclosed gap — someone downstream stops
looking.

## Consequences

- The scrubbed text stays grammatical prose, so stage 2 is summarizing a sentence
  rather than a fragment, and the summary it produces reads as though the name
  had never been there.
- The role word is *more* informative than the name was. "The pilot deployed the
  reserve" tells a reader which person in the account did it; "[name] deployed
  the reserve" tells them nothing and invites the model to speculate.
- Adding a language is supplying two words, not editing the scrub.
- A French report scrubs exactly as an English one does. There is no language in
  which stage 1 degrades to "drop the narrative".
- The uniform article is **asserted**, not just documented:
  `FrenchNarrativeTests` scrubs a report in which a woman was flying and asserts
  the substitution the scrub performed. An earlier version asserted the absence
  of "la pilote" against a fixture that never contained it, which could not fail;
  a separate test now feeds "elle était la pilote" through and pins that stage 1
  leaves the reporter's own words alone. A future contributor who "fixes" the agreement out of
  linguistic instinct gets a red test explaining why it is not a bug.
- The two French words are pinned by a test that asserts them literally. That is
  the one place in this suite where asserting exact text is right: they are a
  human decision, not generated output, so a change to either should be a
  deliberate act that fails a test until somebody makes it.
- **Follow-up:** these belong in `locales/glossary.json` as pinned terms, so the
  translation job can never rewrite them. That file does not exist yet and its
  format is owned by another issue, so it is not created here.

## Alternatives rejected

**`[redacted]`.** Considered and rejected by the repository owner. It reads as a
censored document rather than a report, and it degrades the stage 2 summary: the
model is handed a sentence with a hole in it and has to work out what kind of
thing was removed. It also loses the distinction between the two people in the
account, which is often the point of the sentence.

**`[name]`.** Same problem, one step milder. It says a person was removed but not
which person, so "I am filing this for [name]" and "[name] spiralled in" become
indistinguishable to the summarizer.

**Initials, or a consistent pseudonym.** Actively worse than either. Both are
stable identifiers by another name: to the fifty people who fly that site, "S.W."
and "Pilot A, who was flying a tandem that day" identify exactly one person.

**Leave the pronouns and delete the name.** "spiralled in from 200 feet" with no
subject is not a sentence, and the model will supply a subject.

**"l'auteur du rapport" for the reporter in French.** Accurate, and it reads
heavily on repetition — a summary refers to the reporter several times, and a
four-word noun phrase in each of them turns readable prose into administrative
prose. `déclarant` is one word and is the term the register already uses.

**Agreeing the French article with the person** — "la pilote" when a woman was
flying. The instinct is correct grammar and wrong anonymisation: it re-introduces
the person's gender into a text whose entire purpose is that the person cannot be
picked out. Rejected explicitly rather than left to a future contributor's
judgement, which is why there is a test asserting it.

## Related

- [ADR-0027](ADR-0027-deterministic-scrub-design.md) — the rest of stage 1
- [ADR-0003](ADR-0003-anonymization-pipeline.md)
- `docs/anonymization-policy.md` — the rule as policy
- `src/HpacSafety.Core/Features/Anonymization/README.md`
