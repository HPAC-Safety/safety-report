# ADR-0028 — A name in a narrative becomes a role word, not a placeholder

**Status:** Accepted
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
the two words and `ScrubVocabulary.EnglishCanada` is the only one this repository
ships. There is no locale lookup inside the scrub and no default for a report
filed in French.

## Consequences

- The scrubbed text stays grammatical prose, so stage 2 is summarizing a sentence
  rather than a fragment, and the summary it produces reads as though the name
  had never been there.
- The role word is *more* informative than the name was. "The pilot deployed the
  reserve" tells a reader which person in the account did it; "[name] deployed
  the reserve" tells them nothing and invites the model to speculate.
- Adding a language is supplying two words, not editing the scrub.
- **Open question, deliberately not answered here:** the fr-CA role words. A
  French string is exactly the kind of value AGENTS.md forbids inventing, so
  `ScrubVocabulary` has no French entry and a French report cannot be scrubbed
  until HPAC supplies the wording. The seam is in place; the words are not.

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

## Related

- [ADR-0027](ADR-0027-deterministic-scrub-design.md) — the rest of stage 1
- [ADR-0003](ADR-0003-anonymization-pipeline.md)
- `docs/anonymization-policy.md` — the rule as policy
- `src/HpacSafety.Core/Features/Anonymization/README.md`
