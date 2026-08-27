# Question bank and form

Supporting detail for [`question-bank-and-form.feature`](question-bank-and-form.feature)
that doesn't fit Gherkin.

## Revision fields

Each revision contains:

- a unique revision identifier, stable question key, and monotonically
  increasing revision number;
- English and French label text and optional English and French help text;
- question type;
- the complete ordered bilingual option set, when applicable;
- form sort order and optional section/group key;
- `is_private`, `is_active`, `is_system`, and `is_required` flags;
- creation timestamp and the revision it supersedes, when any;
- a nullable `deleted` timestamp.

Options may be immutable child rows tied to the revision. Their code, labels,
and order are part of that revision and cannot be shared mutably with another
revision.

## Current form query

The query the API uses to assemble the form is a read DTO; it does not expose
persistence entities. It includes the revision ID, key, type, section, flags,
bilingual copy, and bilingual options needed to render and validate the form.
The response carries both translations so a locale toggle never has to replace
the question identities already shown.

## Question types

The required answer shapes are short text, long text, email, phone, date,
number, single select, multi-select, yes/no, checkbox, and file upload.
Dropdowns versus radio buttons are presentation choices for the same
single-select domain type.

The Typeform-derived question set is seed/import input, not hardcoded form
logic. The database remains authoritative after initial seeding.

## Current implementation divergence

Main currently has a stable `Question` whose order, active flag, privacy, and
role can change, while wording/type/options live below `QuestionVersion`. It
also projects several ordinary answers onto typed report properties. Both
choices are superseded by the complete-revision model in the feature file. See
[implementation status](../implementation-status/implementation-status.md).
