---
title: Reporter and pilot names are read by question role
description: Four more optional QuestionRole values (reporter_first_name, reporter_last_name, pilot_first_name, pilot_last_name) name the answers the admin report list shows by stable identity; a migration backfills the seeded questions that already carry them, and the admin_report_queue view projects the current values, never the public side.
type: adr
status: accepted
date: 2026-09-27
decision-makers: Chase Florell
keywords: question role, QuestionRole, admin report list, admin_report_queue, reporter name, pilot name, question bank, ADR-0016, ADR-0055, ADR-0071, ADR-0116, ADR-0117
---

# ADR-0154 — Reporter and pilot names are read by question role

## Status

Accepted. It extends `QuestionRole` (ADR-0016) the same way ADR-0117 and
ADR-0119 use it for the two consent questions, and amends `AGENTS.md`
invariant 1 ("Two system questions are the only answers read by name") in the
same pull request.

## Context

[Issue #571](https://github.com/HPAC-Safety/safety-report/issues/571): a
reviewer scanning `/admin/reports` cannot tell who reported an occurrence or
who was flying without opening every report. The product owner's decision is
that each row shows the reporter's name (the "From:" first and last name
answers) and the pilot's name (the "Pilot:" first and last name answers), each
optional and shown independently, blank when unanswered, never collapsed when
the two are the same person.

That is new: invariant 1 says only publication consent and media consent are
"the only answers read by name." Naming four more answers by their question's
current wording (`"First name"` appears twice in the seeded form, once under
each group) would be reading by position — brittle the moment an
administrator rewords a group heading or a label, and wrong the moment a
question forks (ADR-0071), since a fork gives the live question a new
`TinyId`.

## Decision

**The mechanism is `QuestionRole`, unchanged.** Four more members:

```csharp
public enum QuestionRole
{
    None = 0,
    ConsentPublish = 1,
    ConsentMedia = 2,
    ReporterFirstName = 3,
    ReporterLastName = 4,
    PilotFirstName = 5,
    PilotLastName = 6,
}
```

`QuestionRole`'s own doc comment already describes exactly this shape: "a role
is optional metadata on an ordinary question, not a second kind of question…
an administrator can move a role to a different question, clear it, or delete
the question that carries it." Nothing about `IsSystem` is touched, and
neither of the two consent questions changes. The four new roles are ordinary
roles on ordinary, non-system questions:

- **They stay optional.** `IsRequired` lives on the revision and is untouched;
  a role carries no requiredness of its own, so an administrator does not have
  to make either name mandatory to give it a role.
- **They survive a fork.** `Question.Fork` already carries `Role` onto the
  replacement (`new Question(Key, false, Role, at)`) — this was true before
  this ADR, for the same reason `Key` survives a fork. An answer's
  `question_id` points at whichever question row (live or retired) was live
  when it was given, and that row's `Role` was set when it was created or
  last reassigned, so a historical answer resolves correctly without knowing
  the fork chain exists.
- **They stay deletable and reassignable.** Unlike the two system questions,
  an administrator may delete a name question, retype it, or move its role
  elsewhere with `Question.AssignRole` (already on the aggregate, previously
  exercised only by tests). This pull request does not add an admin-facing way
  to *assign* one of the four new roles through the UI or API — only to seed
  it — because nothing in issue #571 asks for that, and inventing it would be
  the improvisation `AGENTS.md` "Missing requirements" forbids. A future issue
  that wants an administrator to move a name role onto a different question
  asks for it explicitly.
- **Never read on the public side.** `admin_report_queue` is the only view
  touched; `public_reports` and the public feed endpoints do not join
  `report_answers` for these roles at all, so there is no path for a name to
  reach a published report.

### Existing data needs a migration, not just a seed change

`QuestionBankSeedWriter` writes every seeded row with an
`INSERT … WHERE NOT EXISTS` guard (ADR-0020), so editing
`QuestionBankSeed.Questions` to carry the new roles only affects a *fresh*
database — every already-deployed environment has these four questions seeded
with `role = 'none'`. The migration in this pull request:

1. widens `ck_questions_role` to the six-member list (schema change, from the
   updated `QuestionRole` enum);
2. backfills `role` for every `questions` row whose `key` is one of the four
   seeded stable keys, live or already-forked, since a forked-away row is
   still what an old answer resolves through:

   | Key | Role |
   |---|---|
   | `da89ae06_f229_4f38_8faa_e9c5bafef2f3` | `reporter_first_name` |
   | `3d662189_41cb_4430_9db8_7b2e4861df53` | `reporter_last_name` |
   | `52afac6c_b30c_4bd2_a052_212fa9249a45` | `pilot_first_name` |
   | `41c4d104_82c5_4f31_9d86_cb95e35622e4` | `pilot_last_name` |

3. recreates `admin_report_queue` (ADR-0055, ADR-0116) with `reporter_name`
   and `pilot_name`: each is `NULLIF(TRIM(first || ' ' || last), '')` over a
   lateral join of `report_answers` to `questions` filtered to the four
   roles, so a report with only one of the pair still shows it, and a report
   with neither gives `null` — rendered blank, the same way `consent` already
   gives `null` for unanswered (ADR-0130).

The backfill keys on `questions.key`, the stable identity ADR-0071 already
guarantees, not on English wording — an administrator who later renames "From:"
to something else does not lose the projection, and a French-first
installation (there is none today, but nothing here assumes English) would
still resolve by key.

### A role lives on at most one live question, enforced in the database

`Question.AssignRole`'s own doc comment already says "a role lives on at most
one active question at a time; that is enforced by the question bank, not
here." Before this pull request nothing enforced it anywhere: two live
questions could carry `ConsentPublish`, and `admin_report_queue`'s
`MAX(answer.value) FILTER (…)` would silently pick one of the two arbitrarily.
`ix_questions_role` closes that gap the same way `ix_questions_key` already
closes the equivalent one for `key`: unique among live rows, filtered —
`role <> 'none' AND deleted IS NULL` — since `none` is what every ordinary
question defaults to and there is no reason to limit how many questions have
no role.

**Whether Fork's momentary two-rows-sharing-a-value survives this was checked,
not assumed.** `Question.Fork` retires the old row (`Retire(at)`, setting
`Deleted`) and returns a new one carrying the same `Key` and `Role`; a caller
adds the replacement and calls `SaveChangesAsync` once, so for one instant in
the change tracker both rows exist with the same role and only one is
retired. `QuestionRolePersistenceTests` proves this against real PostgreSQL by
forking the seeded `ReporterFirstName` question after answering it: EF Core
emits the `UPDATE` that retires the old row before the `INSERT` of the
replacement in the same batch, so the partial index's `deleted IS NULL` half
already excludes the old row by the time the new row's uniqueness is checked.
This is exactly the ordering `ix_questions_key` has relied on since ADR-0071,
so nothing about adding a second partial-unique column changes it — the same
`Retire`-then-add-then-`SaveChanges` shape works for both.

**The index also caught a real, previously silent duplication in the
acceptance test fixtures.** `QuestionBankSeed`'s publication-consent question
keeps the real Typeform form's original field id as its key (imported, not
authored), never the `QuestionKey.ConsentPublish` constant. Several
step-definition files queried for the consent question *by that constant*
rather than by role, found nothing, and — reasonably, at the time — created
and saved their own second `ConsentPublish`-role question so the scenario had
something to answer. That has always meant the shared acceptance database ran
with two live questions carrying the same role; nothing before this pull
request could tell. Adding `ix_questions_role` turned that into 277 failing
scenarios in one run. The fix reads the seeded question by role everywhere these files touch a
really-migrated database — across `HpacSafety.Acceptance.Tests`
(`ReportSubmissionEndpointSteps.ConsentRevisionId`,
`ReportReviewSteps.ConsentQuestion`, `ReviewActionSteps.BootedReports.Seed`,
`SummarizationOutboxSteps.Seed`, `MediaConsentSteps.PublicationConsentOnTheForm`),
`HpacSafety.Worker.Tests` (`SummarizeReportProcessorTests`), and
`HpacSafety.Api.Tests` (`ReportSubmissionEndpointTests`,
`ChoiceParentLinkRaceTests`, `ReportReviewCommandEndpointTests`,
`CommentEndpointTests`, `ReportReviewEndpointTests`) — instead of inventing a
second question, so the fixtures now hold the same invariant the database
enforces.

## Consequences

- Reading four more answers by name is a narrow, named exception to REQ-MOD-030
  ("no answer text or summary text appears in the list") — amended in the same
  pull request to say so, and to the out-of-scope line in
  `.spec/features/moderation-authentication-and-publication/README.md`.
- **Listing reports is not an audited read** (REQ-MOD-030): no `ViewedRawReport`
  entry is written for it. The reporter's and pilot's names are therefore the
  one piece of answer text a reviewer can read without an audit entry
  recording it — the same narrowness as the REQ-MOD-030 exception itself, not
  a second one. Opening a report stays the audited read of everything else:
  every other answer, the summary pair, and any attachment.
- `ix_questions_role` makes "a role lives on at most one live question" a
  database fact, not only the convention `Question.AssignRole`'s doc comment
  already claimed. A future bug that assigns two roles to the same value can
  no longer ship silently; it fails the write instead of picking one answer
  arbitrarily in `admin_report_queue`.
- A question bank that never answered these two groups (a deployment that
  removed them, or one seeded before this migration ran on a database an
  administrator then emptied) shows blank names, not an error — the view's
  join is a `LEFT JOIN LATERAL`, and `AssignRole`/deletion already model "no
  question currently carries this role."
- If a future need reads a fifth answer by name, it is one more `QuestionRole`
  member and one more migration in this same shape, not a new mechanism.
