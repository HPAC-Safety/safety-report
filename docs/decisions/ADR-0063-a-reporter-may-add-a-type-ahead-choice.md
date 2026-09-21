---
status: accepted
date: 2026-09-21
decision-makers: Chase Florell
keywords: autocomplete, option sets, reporter-added, curation, snapshot, question bank
---

# ADR-0063 — A reporter may add a missing type-ahead choice, and an autocomplete renders the live list

## Context

"Where did this happen?" is a type-ahead over a curated list of flying sites —
Cooper's, Merrill, King Eddie, Woodside. The list will never be complete. Free
flight happens wherever the air is good, and a pilot who had an incident at
Mount 7 has three options if the list does not offer it: pick the nearest wrong
answer, leave it blank, or abandon the report. All three are worse for safety
than letting them type the site name.

Letting them type it collides with
[ADR-0058](ADR-0058-shared-option-sets-with-a-revision-snapshot.md), which is
the decision that makes shared lists safe. A question revision **snapshots** its
choices when it is created, and answers point at that snapshot, so editing a
shared list can never rewrite what a past reporter was offered. A choice added
after a revision was saved is, by construction, not in that revision. Render
from the snapshot and Mount 7 is invisible to the next pilot until a safety
officer happens to publish a new revision of that question — which is not a
solution, it is the problem with extra steps.

There is a second collision.
[ADR-0062](ADR-0062-administrators-may-machine-translate-question-text.md) drew
a line two days ago: machine translation is for question wording only, "never a
narrative, never an answer, never a summary", because sending reporter content
to a third party is a different decision from sending our own. A site name a
reporter typed is reporter-entered text.

## Decision

### An autocomplete renders the live list; its snapshot remains the record

`QuestionChoices.For` is the one place this is written down. For every
option-bearing type it returns the revision's frozen snapshot. For
`QuestionType.Autocomplete` **backed by a live shared set**, it returns the
set's current items instead.

The snapshot is not removed and does not become decorative — it still answers
"what was this reporter shown", which is what a reviewer reading a two-year-old
report needs. The live list answers a different question: "what do we offer
now". Both are true; they are just not the same question.

This carve-out is deliberately narrow:

- **Only `Autocomplete`.** A pick-one list is a closed set an administrator
  curated — province, injury severity — and showing a choice the revision never
  mentioned would make its record misleading for no benefit. An autocomplete is
  the one type whose whole purpose is a list too long and too open-ended to
  curate completely.
- **Only when the set is live.** A retired set leaves the revision rendering
  exactly what it recorded, rather than rendering nothing.

### A reporter's value is recorded at submission, never before

`OptionSet.AddFromReporter` is a domain operation the submission path calls
inside its existing transaction. Nothing is written while the reporter is
typing, which is product invariant #2 unchanged: no report data reaches a
server before the final submit.

Three cases, and the differences are the point:

- **The code is already offered.** Two pilots typing "Mount 7" the same weekend
  produce one row, not two, and a reporter's spelling never overwrites an
  administrator's wording.
- **The code exists but was removed.** It is returned **without being revived**.
  An administrator removed it on purpose; a reporter typing it again must not
  undo that. Their answer still points at a real row — the list simply does not
  offer it.
- **The code is new.** A new item is created and flagged `added_by_reporter`.

### It is marked, and an administrator curates it

`option_set_items.added_by_reporter` is what makes this safe rather than a slow
leak of junk into the form. `/admin/choice-lists` shows the count of unreviewed
reporter-added choices and marks each one, so a safety officer can fix a
spelling, correct the French, merge a duplicate, or remove it. Removal is a
soft delete, so revisions that already snapshotted it keep their copy.

### The second language is machine-translated at submission

A reporter types one language. Both are required, as everywhere else in the
question bank. The submission path translates the typed value through
`ITranslator` and supplies both labels; `AddFromReporter` does no translating
itself, because which service drafted a label is the caller's business.

**This widens ADR-0062's scope, and it should be read as a real change rather
than a detail.** Reporter-entered text now reaches a translation provider. Two
things bound it: the text is a place name typed into a type-ahead, not a
narrative, and the result is visible to administrators as a reporter-added item
rather than being filed silently. It is still reporter content leaving the
system, and ADR-0062's scope statement is amended to say so plainly instead of
contradicting the code.

## Consequences

- One additive migration: `added_by_reporter` plus an index on
  `(option_set_id, added_by_reporter)`, which is the curation query.
- The list a pilot sees for a type-ahead can now change between two reports
  without any administrator action. That is the intent, and it is why the
  snapshot still exists.
- A site added by one reporter is visible to every other reporter immediately.
  With an anonymous form that is a spam and privacy surface — a free-text box
  accepts anything, including a person's name. The owner's position is that the
  form will require authentication; **that decision is not made by this ADR and
  is not implemented here.** Until it is, the exposure is real and the
  curation screen is the only control.
- `QuestionView` gains `ChoicesComeFromLiveList`, so the authoring screen shows
  an administrator the same list a reporter would see.

## Alternatives rejected

**Keep rendering the snapshot; reporter-added choices appear only after an
administrator publishes a new revision.** Preserves ADR-0058 untouched.
Rejected because it does not deliver the feature: the next pilot at Mount 7
still cannot pick it, which is the entire problem.

**Render the snapshot merged with anything added since.** Keeps the frozen
record and shows new entries. Rejected as the worst of both: the reporter sees
a list that no single revision describes, and the rule for what is in it is
harder to explain than either pure option.

**Let a reporter's entry revive a choice an administrator removed.** Falls out
naturally from the existing `Add` behaviour. Rejected: removal is the only
curation tool there is, and an entry that comes back when a reporter retypes it
is not removed.

**Hold reporter-added choices for approval before offering them.** Safer
against spam and against someone typing a person's name. Rejected by the
owner — it breaks "available the next time somebody fills in a location", which
is the requirement — in favour of requiring authentication on the form.

**Translate the typed value in the browser, or not at all.** In the browser
publishes the credential (ADR-0062). Not at all leaves half the membership
looking at a blank choice.

## Related

- [ADR-0058](ADR-0058-shared-option-sets-with-a-revision-snapshot.md) — amended by the carve-out above
- [ADR-0062](ADR-0062-administrators-may-machine-translate-question-text.md) — scope widened by this ADR
- [ADR-0016](ADR-0016-data-driven-question-bank.md) — the question set is data
- [`/features/question-bank-and-form/question-bank-and-form.feature`](../../features/question-bank-and-form/question-bank-and-form.feature)
