# Question bank and form

## Canonical model

A question is a stable logical key with a sequence of complete immutable
revisions. A revision is the unit displayed and referenced by an answer. Every
administrator edit creates a new complete revision; no display-affecting value
is updated in place.

Each revision contains:

- a unique revision identifier, stable question key, and monotonically
  increasing revision number;
- English and French label text and optional English and French help text;
- question type;
- the complete ordered bilingual option set, when applicable;
- form sort order and optional section/group key;
- `is_private`, `is_active`, `is_system`, and `is_required` flags;
- creation timestamp and the revision it supersedes, when any;
- a nullable `deleted` timestamp governed by the deletion rules below.

Options may be immutable child rows tied to the revision. Their code, labels,
and order are part of that revision and cannot be shared mutably with another
revision.

Changing wording, help, translations, options, type, order, section, privacy,
active state, required state, or system state creates a new complete revision.
This avoids reconstructing a historical form from a mixture of mutable and
versioned tables.

## Current form selection

For each stable question key, the API finds its latest revision and includes it
only when that revision is active and not deleted. This prevents an older active
revision from reappearing after a later revision deactivates or deletes the
question. Included revisions are ordered by sort order and then by stable key
as a deterministic tie-breaker. The response carries both translations so a locale
toggle never has to replace the question identities already shown.

The query is a read DTO; it does not expose persistence entities. It includes
the revision ID, key, type, section, flags, bilingual copy, and bilingual
options needed to render and validate the form.

## Question types

The required answer shapes are short text, long text, email, phone, date,
number, single select, multi-select, yes/no, checkbox, and file upload. A file-
upload answer associates zero or more multipart attachments with its exact
revision. A statement and a group/section collect no answer. Dropdowns versus radio buttons
are presentation choices for the same single-select domain type.

The Typeform-derived question set is seed/import input, not hardcoded form
logic. The database remains authoritative after initial seeding.

## Required and optional behavior

`consent_publish` is the only system question and the only required question.
It is a yes/no question with no preselected value. The API rejects a submission
when it is absent, null, has the wrong type, or does not resolve to an explicit
yes or no.

Every ordinary answer-producing question is optional. Skipping one must not
block submission or synthesize a value. The submission DTO still records that
the revision was shown, using a nullable value or empty option selection.
Statements and groups do not produce answer entries. A skipped file-upload
question produces an answer entry with no associated attachment parts.

Consent is the only answer projected onto the report aggregate because it is a
publication invariant. Dates, times, provinces, injury severities, aircraft,
and other ordinary data remain revision-bound answers. Consumers interpret
them through the question key and revision metadata rather than duplicate typed
report columns.

## Privacy

Privacy belongs to the complete revision. An answer stores the exact revision
identifier and a privacy snapshot so later queries remain simple and historical
behavior is explicit. Private answers are available only to authorized admin
flows and to the Worker as labeled recognition context. They never become
public content.

## Editing

Only an active Administrator may create a revision. The editor loads the latest
revision, copies all fields into an edit DTO, validates both languages and all
options, and saves a new complete row/aggregate. It never patches an existing
revision.

Creation must preserve these invariants:

- stable keys are non-empty, unique logical identifiers and are not localized;
- revision numbers increase once per stable key;
- both labels are present for answer-producing questions;
- option-requiring types have at least one valid bilingual option and other
  types have none;
- option codes are unique within a revision;
- only `consent_publish` may be system or required;
- the consent revision is always active, yes/no, private, and excluded from
  summary input despite being stored as an answer;
- sort order is deterministic; duplicate values are allowed only because the
  stable-key tie-breaker is defined.

## Stale and deleted revisions at submission

A report may be submitted against a known, non-deleted superseded revision that
the browser was previously shown. This preserves a reporter's work when an
administrator edits the form mid-session. The API validates answers using that
revision's historical type, options, and privacy.

Unknown revision IDs and deleted revisions are rejected. A question revision
can be soft-deleted only when no answer references it, including answers on
deleted reports. Consequently, a legitimately answered revision remains
available as history forever.

## Current implementation divergence

Main currently has a stable `Question` whose order, active flag, privacy, and
role can change, while wording/type/options live below `QuestionVersion`. It
also projects several ordinary answers onto typed report properties. Both
choices are superseded by the complete-revision model above. See
[implementation status](implementation-status.md).
