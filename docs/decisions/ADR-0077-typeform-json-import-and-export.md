---
status: accepted
date: 2026-09-21
decision-makers: Chase Florell
keywords: typeform, import, export, question bank, bilingual, seeding, ref, extension block
---

# ADR-0077 — Question bank import/export uses Typeform's own JSON, not QSF

## Context

The question bank is authored one question at a time through `QuestionEditor`.
The organization's existing occurrence-report form was built and is still
exported from Typeform, and an administrator wants to bring that form's
questions in rather than retype 20-odd questions and their choice lists by
hand, and wants a way back out again — ideally one that could be re-imported
into Typeform itself later, since Typeform's own JSON import accepts a file
in the shape it exports.

This was scoped, in an earlier pass of this same discussion, as "QSF"
(Qualtrics Survey Format) support. Reviewing the real exports the
organization actually has —
`formENG.json`/`formFR.json`, root `"type": "quiz"`, `welcome_screens`,
`fields[]`, `logic[]` with jump actions — showed that framing was wrong: this
is Typeform's native JSON, unrelated to Qualtrics QSF. This ADR replaces that
framing entirely; nothing here is Qualtrics-specific.

The two files are separate documents (different Typeform form `id`, e.g.
`ZzIBaNLP` for English, `eeqpY9V8` for French), not one file with embedded
translations. Correlating the same logical field across both requires an
identifier stable across both exports — Typeform's field-level and
choice-level `ref` GUID is exactly that (`id` differs per language export;
`ref` does not, confirmed against the real files).

## Decision

### Import takes an English file and a French file, matched by `ref`

`POST /api/admin/typeform/import` accepts both files. Every field/choice
`ref` present in one file must be present in the other; a `ref` missing from
either side is a **hard import error**, reported before anything is parsed
into a draft — this is a paired authoring source, not a best-effort merge.

### Import produces review drafts, never persisted rows

Parsing produces `ImportedQuestionDraft` values shaped like
`SaveQuestionRequest` — the same DTO manual authoring already produces — with
both languages populated from the matched EN/FR pair. Nothing is written to
`questions`/`question_revisions` by the import call itself. An administrator
reviews and saves each draft one at a time through the existing
`QuestionEditor`, exactly as if they had typed it — import is a prefill step
in front of authorship the system already requires (ADR-0061/ADR-0062), never
a way around it.

### The stable `Key` is the Typeform `ref`

`Question.Key` (ADR-0071) is assigned from the field's `ref` GUID directly,
not independently generated. Re-importing the same form later resolves to
the same key and, through the ordinary edit path, revises or forks the
existing question rather than creating a duplicate.

### Type mapping

| Typeform `type` | HPAC `QuestionType` |
|---|---|
| `statement`, except the auto-generated answer-recap screen (see below) | `Statement` ([ADR-0076](ADR-0076-statement-and-group-question-types.md)) |
| `group`; `contact_info` | `Group` header + one flattened child question per nested/sub field, each `GroupedUnderQuestionId`-linked to the header |
| `short_text`; a `contact_info` `first_name`/`last_name` subfield | `ShortText` |
| `long_text` | `LongText` |
| `email` | `Email` |
| `phone_number` | `Phone` |
| `date` | `Date` |
| `file_upload` | `FileUpload` |
| `yes_no` | `YesNo` |
| `multiple_choice`, single-select | `SingleSelect` |
| `multiple_choice`, multi-select, no `allow_other_choice` | `MultiSelect`, new `OptionSet` seeded from `Choices` |
| `multiple_choice`, multi-select, `allow_other_choice: true` | `MultiSelect`, new `OptionSet`, reporter-addition enabled ([ADR-0063](ADR-0063-a-reporter-may-add-a-type-ahead-choice.md) amendment, below) |
| `dropdown` | `SingleSelect` — dropdown-versus-radio is presentation, per the existing `QuestionType` doc comment, so this is a disclosed, deliberate lossy point: export always re-emits `multiple_choice`, never `dropdown` |
| `logic[]` reducible to one yes/no-parent "answered yes" gate | `DependsOnQuestionId` |
| Any other `logic[]` shape (multi-field AND/OR, a multi-select parent, an OR across option values) | not imported as a condition — see below |
| Matrix, slider, rank-order, constant-sum, or any other field type Typeform can emit but the sample does not use | rejected, listed in the import report as unsupported |

`OptionSet`s are always created fresh per imported question, never merged into
an existing one — an administrator merges by hand afterward through the
existing option-set editor if that turns out to be wanted, which keeps the
importer from needing fuzzy matching.

### Amending ADR-0063: reporter-addition is not `Autocomplete`-only

`allow_other_choice` in the real data appears only on `multiple_choice`
fields with multiple selection (pilot ratings, aircraft type) — never on a
single-select. Its behavior — a reporter types a value the fixed list does
not offer — is the exact shape ADR-0063 already built for `Autocomplete`:
`OptionSet.AddFromReporter`, the three reuse/removed/new cases, the
`added_by_reporter` curation flag. Building a second "free text beside a
fixed choice" mechanism to cover it would duplicate that machinery for no
reason.

`QuestionRevision` gains `AllowsReporterAdditions` (bool). It is fixed `true`
for every `Autocomplete` revision — preserving today's behavior exactly, with
no migration data change since it was previously implied by the type alone —
and author-controlled for `MultiSelect`, defaulting `false`. `QuestionChoices.For`
renders the live `OptionSet` list, and `OptionSet.AddFromReporter` is reachable
at submission, whenever `AllowsReporterAdditions` is `true` and the revision
is backed by a live set — regardless of whether the type is `Autocomplete` or
`MultiSelect`. `SingleSelect` is deliberately left out of this widening, the
same way ADR-0074 named `multi_select`/`autocomplete` parents out of scope
rather than silently extending to them: nothing in the current form needs it.

This is recorded here, in the ADR that motivates it, and as a
`### Amended by ADR-0077` section appended to ADR-0063 itself, per house style
— ADR-0063's body is not rewritten.

### The auto-generated recap screen is recognized and skipped

Typeform appends a `statement` field summarizing prior answers with
`{{field:...}}` merge tags (seen in both sample files as the final field).
It carries no authored content of its own. The importer recognizes a
`statement` whose description is entirely merge-tag interpolation and drops
it rather than importing a `Statement` question that will only ever show
literal, unresolved `{{field:...}}` text to a reporter.

### Export produces a zip of the same two-file shape, with an extension block

`POST /api/admin/typeform/export` returns a zip of `questions-en.json` and
`questions-fr.json`, each a valid Typeform-shaped document (`fields[]`,
`logic[]`, and the minimal top-level wrapper Typeform's own format expects —
not full account/branding metadata, which this system never had to begin
with).

Fields this schema has that Typeform has none for — `Key` (redundant with
`ref` but explicit), `IsPrivate`, `DependsOnQuestionId`/`DependsOnOptionCode`,
`GroupedUnderQuestionId`, `OptionSet` provenance — round-trip through a
namespaced `hpac` object added to each field, referencing other fields by
`ref` rather than internal `TinyId` (the only identifier stable across a
round trip). A plain Typeform importer — including, if the open item below
confirms it, Typeform's own — ignores an object key it does not recognize; a
re-import into this system reads `hpac` back for exact fidelity. Export
never regenerates a `dropdown` Typeform field (see the type-mapping table
above); that distinction is not preserved.

### Unsupported branching logic is never silently dropped

A `logic[]` shape this ADR's mapping table does not cover still imports its
question — unconditionally, so the rest of its content is not lost — but its
raw Typeform logic JSON is written to a new `pending_import_logic` table
(import-batch-scoped: `ImportBatchId`, the Typeform field's `ref`/label for
identification before the question is even saved, the raw logic JSON,
`CreatedAt`). An administrator reviews the list, wires the condition by hand
on the saved revision using the ordinary conditional-question authoring UI,
and deletes the note.

This table is **hard-deleted**, a deliberate and narrow exception to the
soft-deletion convention (product invariant #8) — the same shape of argument
as the `admin_users` drop, on its own facts, not a precedent for anything
else: it holds transient import scratch notes, never report or answer data,
and its entire purpose is to disappear once an administrator has acted on it.

### The question-bank seed comes from these two files

`QuestionBankSeed` — currently empty (`446fbbd` cleared it, pending "a
correct question set" per its own comment) — is populated by running this
importer once against `formENG.json`/`formFR.json`, reviewing the resulting
drafts exactly as an administrator would, and checking in the result. This
supersedes the seed-authoring half of
[ADR-0020](ADR-0020-seeding-by-migration.md), which transcribed
`docs/form-spec.md` by hand; see the amendment appended there. It also
doubles as the first real end-to-end proof this importer produces a correct
question set, ahead of any synthetic fixture test.

## Consequences

- New migration: `AllowsReporterAdditions` and `GroupedUnderQuestionId` on
  `question_revisions` ([ADR-0076](ADR-0076-statement-and-group-question-types.md)
  covers the latter's rationale), and the `pending_import_logic` table.
- `docs/form-spec.md` stops being the seed's source of truth; it remains
  useful, hand-maintained evidence of what the live Typeform page currently
  asks, regenerable by `tools/extract-typeform.py`.
- `QuestionKey.Normalize` must accept a GUID-with-dashes shape, since `Key`
  is now sometimes assigned from one directly rather than always typed by an
  administrator — verified during implementation, not assumed here.
- Whether the `hpac` extension block genuinely survives a round trip through
  Typeform's own import tool is unverified; "re-importable into Typeform" is
  a design goal this ADR enables, not a tested claim.

## Alternatives rejected

**Support Qualtrics QSF**, the original framing. Rejected once the actual
files were reviewed — the organization has no Qualtrics forms.

**Two files correlated by field order or `id` instead of `ref`.** Field
order breaks the moment the two language forms diverge even slightly; `id`
is confirmed different per language export in the sample data. `ref` is the
only identifier both files agree on.

**Generate our own `Key`, keep `ref` only as transient import-run metadata.**
Considered, to keep `Key` purely HPAC-authored. Rejected: it throws away
free re-import idempotency for no stated benefit, and the organization
explicitly wants a path back into Typeform, where `ref` is already the
identity Typeform itself uses.

**A second, `MultiSelect`-specific "Other" mechanism**, instead of widening
ADR-0063. Rejected — see the amendment above; it would duplicate
`OptionSet.AddFromReporter` and its curation queue for behavior that is
already built.

**Best-effort re-derivation on reimport instead of an extension block**,
accepting lossy round trips. Rejected by the owner: `Key`, `IsPrivate`, and
the dependency/grouping graph are exactly the facts an administrator has
already curated, and losing them on every export/reimport cycle defeats the
point of exporting at all.

**Silently drop a question with unsupported branching logic.** Rejected: it
discards real content an administrator authored in Typeform for no offsetting
safety. The pending-logic note keeps it recoverable.

## Related

- [ADR-0076](ADR-0076-statement-and-group-question-types.md) — `Statement`/`Group`, `GroupedUnderQuestionId`
- [ADR-0063](ADR-0063-a-reporter-may-add-a-type-ahead-choice.md) — amended here to cover `MultiSelect`
- [ADR-0074](ADR-0074-a-single-select-parent-may-enable-a-conditional-question.md) — precedent for naming scope boundaries explicitly
- [ADR-0058](ADR-0058-shared-option-sets-with-a-revision-snapshot.md) — the `OptionSet` snapshot mechanism reused for imported choices
- [ADR-0020](ADR-0020-seeding-by-migration.md) — amended here: the seed's source changes
- [ADR-0071](ADR-0071-an-answered-question-forks-instead-of-revising.md) — the stable `Key` this ADR assigns from `ref`
- [`/features/typeform-question-import-export/typeform-question-import-export.feature`](../../features/typeform-question-import-export/typeform-question-import-export.feature)
- Issue #235
