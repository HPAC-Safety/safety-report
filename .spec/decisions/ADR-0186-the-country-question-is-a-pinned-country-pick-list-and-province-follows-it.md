---
title: The Country question is a pinned country pick list, and Province follows it
description: The seeded Country question becomes an optional single-select of every ISO 3166-1 country, named by CLDR, with Canada and the United States pinned first. Province is shown only when Country is Canada. An answered Country forks with the same key and old yes/no answers are left as given.
type: adr
status: accepted
date: 2026-10-02
decision-makers: Chase Florell
keywords: country, province, single-select, pick list, ISO 3166-1, CLDR, pin, conditional question, fork, migration, Typeform import, ADR-0071, ADR-0074, ADR-0136
---

# ADR-0186 — The Country question is a pinned country pick list, and Province follows it

**Status:** Accepted. Decided by the owner on 2026-10-02 in
[#750](https://github.com/HPAC-Safety/safety-report/issues/750). Applies
[ADR-0071](ADR-0071-an-answered-question-forks-instead-of-revising.md),
[ADR-0074](ADR-0074-a-single-select-parent-may-enable-a-conditional-question.md),
[ADR-0095](ADR-0095-a-question-owns-its-choices-outside-its-revisions.md),
[ADR-0128](ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md)
and [ADR-0136](ADR-0136-choices-are-listed-alphabetically-in-the-readers-language.md)
to one seeded question. It changes no schema and no rule.

## Context

The seeded **Country** question (key `2a401575_8cf4_4c87_b8df_95798d07a772`) was a
yes/no question carried over from the Typeform, with the help text "Did the
occurrence happen in Canada?". It never named the country, and **Province**
(`849ea0c4_b36e_44a7_936e_e578967907a3`) was asked of everyone.

## Decision

1. **Country is an optional single-select** of every ISO 3166-1 country: 249
   entries. The label stays "Country" / "Pays"; the help text is "Country where
   the occurrence happened." / "Pays où l'évènement a eu lieu."
2. **Each choice's code is its alpha-2 code**, in the lowercase form every
   choice code takes (`QuestionKey.Normalize`): `ca`, `us`. Its English and
   French wording is the CLDR region name for en-CA and fr-CA, with the first
   letter capitalised, generated once with Node's `Intl.DisplayNames` and frozen
   into the migration. The migration's header names the Node, ICU, and CLDR
   versions. The wording is not regenerated at runtime and not machine
   translated: both labels are recorded as written by a person.
3. **Canada and the United States are pinned first**
   ([ADR-0136](ADR-0136-choices-are-listed-alphabetically-in-the-readers-language.md));
   every other choice is not pinned. The open list reads Canada, United States,
   a separator, then every other country alphabetically in the reader's
   language (in French: Canada, États-Unis, a separator, the rest). No new UI:
   the form already sorts and draws each pin group.
4. **Province is conditional on Country = Canada**
   ([ADR-0074](ADR-0074-a-single-select-parent-may-enable-a-conditional-question.md),
   [ADR-0128](ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md)):
   its condition names the Country question and the `ca` choice by ID. A
   reporter who leaves Country blank is **never asked Province**; an optional
   Country cannot be skipped and still reach Province. That is accepted.
5. **The change is made the way an Administrator's edit is made**
   ([ADR-0071](ADR-0071-an-answered-question-forks-instead-of-revising.md)): a
   migration gives Country, and Province, a new revision when no answer
   references the question, and otherwise retires it and starts a new question
   with the same key, carrying every choice. It changes nothing unless Country
   is still a yes/no question with the seeded wording, so a re-run, or a
   database an Administrator already changed, is left alone. A fresh database
   reaches the same shape through the same migration; `QuestionBankSeed` is not
   edited, because a seed is history.
6. **Existing yes/no answers are left as given.** They stay on the retired
   Country question, under the wording they were answered with, and are neither
   back-filled nor rewritten. A fork is chosen for exactly that reason.
7. **Any other live question conditional on the old yes/no Country** ("answered
   yes") is moved to "Country is Canada", which is exactly what a yes meant: a
   yes/no condition left on a single-select parent would never be met. The
   migration reports each with a notice. None is seeded.
8. **The historical import ([#566](https://github.com/HPAC-Safety/safety-report/issues/566))
   maps the old answer to the new choice:** `true` becomes Canada; `false`
   becomes no answer, since "not Canada" names no country. Building the importer
   is out of scope here.
9. **No flag emoji** beside a country. It was considered and dropped: flags
   render as letters on some platforms and name nothing to a screen reader.

## Consequences

- Province's section appears only after Canada is chosen, and a Province answer
  exists only for a report that answered Canada.
- A report filed before this change keeps its yes/no answer on the retired
  question; the admin report page shows it under the wording it was given.
- The 249 names will drift from CLDR as it is revised. That is deliberate: the
  list is the question's own choices, and an Administrator fixes or replaces a
  name like any other ([ADR-0128](ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md)).
- The form's type-ahead is not used: a list of 249 in a select-only combobox is
  scrolled and keyboard-searched, and a country cannot be added by a reporter.

## Considered options

- **A type-ahead.** Rejected: a reporter-added country would be a free value, and
  the point is a fixed, bilingual, comparable list.
- **A hand-written country list.** Rejected: CLDR names are maintained in both
  official languages and are the same source the browser uses.
- **Back-fill old answers** (`yes` to Canada). Rejected by the owner: answers are
  immutable ([ADR-0072](ADR-0072-every-answer-is-stored-as-a-string.md)).
- **Make Province unconditional but optional, as before.** Rejected: it asks a
  question that does not apply outside Canada.
