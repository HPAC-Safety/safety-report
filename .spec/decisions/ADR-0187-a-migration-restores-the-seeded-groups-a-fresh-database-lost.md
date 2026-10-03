---
title: A migration restores the seeded groups a fresh database lost
description: A database created from scratch seeds every question through InitialSchema's legacy shape, which has no grouping, so From, Pilot, and Aircraft arrive empty. A repair migration re-links each seeded question that never had a group, revising or forking it like any edit. The browser suite walks a fixture that must equal what a freshly migrated database sends.
type: adr
status: accepted
date: 2026-10-02
decision-makers: Chase Florell
keywords: seed, group, grouped_under_question_id, InitialSchema, migration, fork, fixture, Playwright, ADR-0020, ADR-0071, ADR-0076
---

# ADR-0187 — A migration restores the seeded groups a fresh database lost

**Status:** Accepted. Decided by the owner on 2026-10-02 in
[#754](https://github.com/HPAC-Safety/safety-report/issues/754). Applies
[ADR-0071](ADR-0071-an-answered-question-forks-instead-of-revising.md) and
[ADR-0076](ADR-0076-statement-and-group-question-types.md) to the seeded
questions. It changes no schema and no rule.

## Context

- `InitialSchema` (2026-08-23) calls `WriteLegacySensitivitySchema`, which writes
  whatever `QuestionBankSeed.Questions` holds when the migration runs, through
  the legacy table shape. That shape has no grouping column.
- The seed was empty until 2026-09-22, when `SeedQuestionBankFromTypeformFixtures`
  filled it and wrote it again through the current shape, groups included.
- A database created before then got the full seed once, from the later
  migration. A database created after it — staging, and production when it is
  created — gets every question early without its group. The later guarded
  insert then skips each one as already present.
- So **From**, **Pilot**, and **Aircraft** arrived with no questions under them,
  and each question was asked on a page of its own.
- The browser suite stubs `/api/v1/questions` with hand-written groups, so no
  test ever saw what a real migrated database sends.

## Decision

1. **A repair migration, and nothing else.** `RestoreSeededGroups` groups each
   seeded question under the group the seed names. `InitialSchema` and the seed
   writer are not changed.
2. **Only where nothing chose otherwise.** A question is grouped only while it
   is live, no revision of any question with its key ever had a group, and its
   group is live and still a group. An Administrator's own grouping, ungrouping,
   or deleted group stands.
3. **Like any edit (ADR-0071).** A question no answer references gets a new
   revision and keeps its identifier. An answered one forks under the same key,
   with every choice, and its old answers stay with the revision they were given
   under.
4. **A re-run changes nothing.**
5. **The browser suite walks what the migrations seed.**
   `tests/e2e/fixtures/seeded-questions.json` is the API's response from a
   freshly migrated database. An acceptance scenario fails the moment the two
   differ, and rewrites the fixture when asked. The group pages are walked
   against it in English and French.

## Consequences

- Staging's groups return at the next release, and production's on creation.
- On staging, an answered child of a group becomes a new question with the same
  key. Reporter and pilot names are still read by role (ADR-0154).
- A change to the seed or to a migration that touches it now fails the fixture
  scenario until the fixture is regenerated and committed, which puts the
  change in front of the browser suite.

## Considered options

- **Make `InitialSchema` write nothing**, as when it shipped. Rejected by the
  owner: it edits a shipped migration, and the repair already covers fresh
  databases.
- **Revise in place even when answered.** Rejected: grouping is a revision
  fact, and ADR-0071 governs every edit to it.
- **A full-stack browser run against a real API.** Rejected for now: new CI
  infrastructure, where a seed-derived fixture with a drift check proves the
  same.
