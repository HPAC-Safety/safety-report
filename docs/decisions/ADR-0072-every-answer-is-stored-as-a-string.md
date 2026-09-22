---
title: "Every answer is stored as a string, in the reporter's language"
description: One column, one string, for every answer of every type.
type: adr
status: accepted
date: 2026-09-21
decision-makers: Chase Florell
keywords: answers, option codes, string values, ISO 8601, locale, translation, question bank
---

# ADR-0072 — Every answer is stored as a string, in the reporter's language

## Context

`report_answers` stores two shapes today. A text-shaped question writes `value`;
a select-shaped question writes a list of invariant option codes, and the words
those codes stand for live in `question_revision_options`, one join away.

The codes are not foreign keys — nothing enforces them — but they behave like
foreign keys, and that is the problem. An answer of `springbank` means nothing
on its own. Reading it requires the answer's revision, that revision's frozen
option rows, and a language to pick a label in.
[ADR-0058](ADR-0058-shared-option-sets-with-a-revision-snapshot.md) exists in
large part to keep that join honest: it snapshots a shared list into each
revision precisely so a removed item cannot leave a stored answer pointing at
nothing.

The owner's ruling is that an answer should carry its own words. If the reporter
picked "Calgary / Springbank" then that is what the report says, and no amount of
later editing anywhere can change what it says.

## Decision

**One column, one string, for every answer of every type.**

`report_answers.value` is a nullable string and is the whole answer.
`selected_option_codes` is removed. A select, picker, or type-ahead answer
stores the literal text the reporter chose, exactly as it was shown.

### The stored form of each type

Text-shaped answers store what was typed. Everything else has one invariant
written form, so a consumer never has to guess:

| Shape | Stored form | Example |
|---|---|---|
| Boolean | `yes` or `no`, always these two tokens | `no` |
| Date | ISO 8601 date | `2026-09-21` |
| Time | ISO 8601 time | `14:30` |
| Date and time | ISO 8601 date and time with offset | `2026-09-21T14:30:00-06:00` |
| Select of any kind | the option's label, as shown | `Calgary / Springbank` |
| Free text | as typed | `Wind picked up on final.` |

A French reporter's yes is `no`'s counterpart `yes`, not `oui`. The boolean and
date/time forms are machine-readable storage, localized only at render, because
[ADR-0060](ADR-0060-conditional-questions-depend-on-a-boolean-question.md) makes
a conditional question depend on a parent being answered yes and that check must
not depend on which language the reporter used.

**ISO 8601 is the storage form and nothing more.**
[ADR-0035](ADR-0035-dateonly-datetimeoffset-timeonly-datetime-is-banned.md)
stands unchanged: the domain still uses `DateOnly`, `TimeOnly`, and
`DateTimeOffset`, parsing at the persistence boundary and never passing a date
around as text. This ADR says what the column holds, not what the code holds.

### A select answer is stored in the reporter's language only

**~~Scoped to select/picker/type-ahead answers.~~ Widened to every answer
shape, and `needs_translation` replaced by `translation_source`, by
[ADR-0080](ADR-0080-every-answer-gets-a-worker-translated-second-language.md).**

`report_answers` gains `locale` — the official language the reporter was using
when they answered — and `needs_translation`.

A select answer stores one string, in that locale, and is flagged. This applies
to a value the reporter picked from a curated list as much as to one they typed
into a type-ahead: the answer records what that reporter saw, and the fact that
an administrator once authored the other language of a similar-looking list item
is a fact about the list, not about the answer.

**This is the expensive half of this decision and it was chosen with the cost
named.** Picking curated "Mont Sept" flags an answer for translation although
"Mount Seven" is sitting in `option_set_items` already, so administrators will
translate text the system could have copied. The owner chose the uniform rule
over the cheaper one: every select answer is handled identically, and there is
no second path where an answer is silently filled in from a list it is no longer
attached to.

### Dropping `selected_option_codes`, argued on its own facts

Product invariant #8 forbids physically deleting application records, and
[ADR-0065](ADR-0065-no-user-records-identity-is-the-token-subject.md) says
plainly that its one carved exception — dropping `admin_users` — does not
generalize and that any future drop needs its own argument. This is that
argument.

`report_answers.selected_option_codes` has never held a value in any deployed
environment, because nothing can write to it. There is no report submission
endpoint: `POST /api/v1/reports` is specified and unimplemented, every
submission scenario in `/features/report-submission` carries `@ignore`, and
`docs/implementation-status.md` records "no report endpoint". The only code
that has ever produced a `report_answers` row is a test, against a database
created and discarded by Testcontainers.

So the column is dropped rather than retained empty. The facts are the same
shape as the `admin_users` argument and they are equally narrow: this rests on
"no writer exists in any deployed environment", not on "the data is not worth
keeping". The moment the submission endpoint ships, this argument expires, and
a later change to the answer shape would owe a backfill rather than a drop.

### An administrator supplies the second language

A flagged answer appears in an administrator queue. They type the other language
or press Translate, exactly as they do when authoring a question
([ADR-0062](ADR-0062-administrators-may-machine-translate-question-text.md)).
Saving either way writes `value_translated` and clears the flag.

**Nothing on the submission path translates anything.** This reverses the
widening that [ADR-0063](ADR-0063-a-reporter-may-add-a-type-ahead-choice.md)
made: reporter-entered text no longer reaches a translation provider because a
reporter submitted it. It reaches one only when a signed-in administrator asks,
through `/api/admin/translate`, which requires the `Administrator` policy. The
scope line in ADR-0062 returns to what it said before — translation is an
administrator's drafting aid — with the single addition that what an
administrator may draft now includes an answer's second language.

**Superseded by [ADR-0080](ADR-0080-every-answer-gets-a-worker-translated-second-language.md):**
an administrator is no longer the only source of a second language. The
Worker fills `value_translated` mechanically via the same `ITranslator` port,
off the request path, for every answer including narrative ones;
`translation_source` (`auto`/`human`) replaces the implicit "an administrator
filled it" assumption this section made. An administrator may still edit
`value_translated` afterward, which is how `translation_source` becomes
`human`.

### What the revision snapshot is still for

`question_revision_options` keeps every row. It is no longer consulted to
render an answer or to validate one, but it remains the record of the complete
set of choices a reporter was offered, which is a different and still necessary
fact: "they chose Springbank" and "they chose Springbank from these forty" are
not the same statement, and a reviewer assessing a report needs the second.

## Consequences

- **An answer is legible on its own.** An export, a reviewer screen, and a
  two-year-old report all read one column.
- **A removed or relabelled option cannot reach an answer**, which is now true
  structurally rather than by the snapshotting discipline ADR-0058 imposes.
- **Validation changes shape.** `ReportAnswer.ForOptions` checked a submitted
  code against the revision's options. The equivalent check is that the
  submitted label is one the revision offered — still a check against the
  snapshot, now on text.
- **Answers become bulkier and duplicate their labels.** Forty aerodromes
  answered by two hundred reporters is two hundred copies of a place name. The
  ADR-0034 volume argument applies here as it does in ADR-0058.
- **Administrators gain a recurring task** proportional to select answers from
  French reporters, which is the cost named above.
- **`needs_translation` is a queue, so it needs an index**, and the queue is a
  privacy surface: it shows reporter-entered text to administrators, which is
  already true of the report review screens but is now true of a list that spans
  reports.
- A boolean answer is compared as text. `"yes"` is the only truthy form, and a
  stored `"oui"` is a bug rather than an alternative spelling.

## Alternatives rejected

**Keep option codes.** Nothing to migrate, answers stay compact, and a
relabelled option updates everywhere at once. Rejected by the owner: an answer
that cannot be read without three other rows is not a record of what was said,
and "updates everywhere at once" is the failure mode, not the feature.

**Store the value in both languages, copied off the item at submission.**
Removes the translation queue for curated picks entirely and flags only
reporter-typed values. Rejected by the owner in favour of one uniform rule,
with the queue cost accepted explicitly.

**Store the value and the code.** Belt and braces: legible answers, with the
code available for aggregation. Rejected because two records of the same fact
diverge, and the first time they disagree there is no rule for which wins.

**Localize the boolean and date forms too.** Consistent with storing a select
value in the reporter's language. Rejected because
[ADR-0060](ADR-0060-conditional-questions-depend-on-a-boolean-question.md)'s
conditional check, every date comparison, and every export would each need both
vocabularies, to record nothing the locale column does not already say.

## Related

- [ADR-0080](ADR-0080-every-answer-gets-a-worker-translated-second-language.md) — widens the bilingual mechanism to every answer shape, Worker-driven
- [ADR-0058](ADR-0058-shared-option-sets-with-a-revision-snapshot.md) — the snapshot's remaining purpose
- [ADR-0063](ADR-0063-a-reporter-may-add-a-type-ahead-choice.md) — submission-time translation removed
- [ADR-0062](ADR-0062-administrators-may-machine-translate-question-text.md) — scope narrowed back to administrator-initiated
- [ADR-0060](ADR-0060-conditional-questions-depend-on-a-boolean-question.md) — the yes/no check is now a string comparison
- [ADR-0035](ADR-0035-dateonly-datetimeoffset-timeonly-datetime-is-banned.md) — unchanged; ISO 8601 is storage only
- [ADR-0071](ADR-0071-an-answered-question-forks-instead-of-revising.md) — the other half of this change
- [`/features/question-bank-and-form/question-bank-and-form.feature`](../../features/question-bank-and-form/question-bank-and-form.feature)
