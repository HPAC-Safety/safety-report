Feature: Typeform question import and export
An Administrator brings the question bank in from a pair of Typeform JSON
exports — one English, one French — instead of authoring every question by
hand, and can export the current bank back to the same two-file shape. Import
never saves a question by itself; it prefills the ordinary authoring screen,
which an Administrator still reviews and saves one question at a time.

Background:
  Given an Administrator has an English Typeform export and a matching French one

@REQ-TF-001
Scenario: Import requires both languages
  When an Administrator submits only one of the two files
  Then the import is rejected
  And no draft is produced

@REQ-TF-002
Scenario: A field's ref appears in the English file but not the French one
  Given a field's ref appears in the English file but not the French one
  When the pair is mapped
  Then a draft is still produced for that field
  And its French text defaults to the English text
  And the draft is flagged that French still needs review

@REQ-TF-003
Scenario: A choice's ref appears in the English file but not the French one
  Given a multiple_choice field's choice ref appears in the English file but not the French one
  When the pair is mapped
  Then a draft is still produced with that choice
  And the choice's French label defaults to its English label
  And the choice is flagged that French still needs review

@REQ-TF-004
Scenario Outline: A Typeform field type maps to a question type
  Given a Typeform field of type <typeform_type>
  When the pair is mapped
  Then it produces a draft of type <question_type>

Examples:
  | typeform_type   | question_type |
  | short_text      | short_text    |
  | long_text       | long_text     |
  | email           | email         |
  | phone_number    | phone         |
  | date            | date          |
  | file_upload     | file_upload   |
  | yes_no          | yes_no        |
  | dropdown        | single_select |
  | statement       | statement     |

@REQ-TF-005
Scenario: A single-select multiple-choice field imports as single-select
  Given a Typeform multiple_choice field that does not allow multiple selection
  When the pair is mapped
  Then it produces a single-select draft seeded from its choices

@REQ-TF-006
Scenario: A multi-select multiple-choice field imports as multi-select
  Given a Typeform multiple_choice field that allows multiple selection
  When the pair is mapped
  Then it produces a multi-select draft seeded from its choices

@REQ-TF-008
Scenario: A group field flattens into a heading and its children
  Given a Typeform group field containing several nested fields
  When the pair is mapped
  Then it produces one group draft from the field's title
  And one draft per nested field, each grouped under it

@REQ-TF-009
Scenario: A contact-info field flattens the same way a group does
  Given a Typeform contact_info field containing name, phone, and email subfields
  When the pair is mapped
  Then it produces one group draft from the field's title
  And one draft per subfield, each grouped under it

@REQ-TF-010
Scenario: The generated answer-recap screen is not imported
  Given a Typeform statement field whose description only interpolates other fields
  When the pair is mapped
  Then no draft is produced for it

@REQ-TF-011
Scenario: A field type with no equivalent is rejected, not silently dropped
  Given a Typeform field of a type this system does not support
  When the pair is mapped
  Then the import report lists it as not imported
  And no draft is produced for it

@REQ-TF-012
Scenario: A field with only linear flow is not flagged as branching logic
  Given a Typeform field whose jump logic has only its unconditional fallback action
  When the pair is mapped
  Then no pending logic note is recorded for that field

@REQ-TF-013
Scenario: Any real branching condition is flagged, not silently dropped or auto-mapped
  Given a Typeform field whose jump logic includes a real condition
  When the pair is mapped
  Then the produced draft is unconditional
  And a pending logic note is recorded naming that field and its original logic

@REQ-TF-014
Scenario: An Administrator resolves a pending logic note
  Given a pending logic note exists from a prior import
  When an Administrator wires the equivalent condition by hand and deletes the note
  Then the note no longer appears in the pending list

@REQ-TF-015
Scenario: Import never saves a question by itself
  Given a pair of Typeform files is imported
  Then no question exists in the bank until an Administrator reviews and saves its draft

@REQ-TF-016
Scenario: The imported draft's key comes from the Typeform ref
  Given a Typeform field with a given ref
  When the pair is mapped
  Then the produced draft's key is that ref, normalized

@REQ-TF-017
@ui
Scenario: Re-importing the same form updates in place
  Given a signed-in Administrator opens the manage-questions page
  When they import a Typeform draft whose key matches an existing question
  Then choosing to review it opens the existing question for editing instead of creating a new one

@REQ-TF-018
Scenario: Export produces a zip of two Typeform-shaped files
  Given the question bank has several live questions
  When an Administrator exports it
  Then the result is a zip containing an English Typeform-shaped file and a French one

@REQ-TF-019
Scenario: Export preserves data Typeform has no field for
  Given a live question has a stable key, a dependency, and a group membership
  When it is exported
  Then the exported field carries that data in a namespaced extension object
  And a plain Typeform file otherwise validates without it

@REQ-TF-020
Scenario: Exporting and reimporting reproduces the same drafts
  Given the question bank has several live questions
  When an Administrator exports it and imports the result back in
  Then the resulting drafts match the original questions' key, type, wording, and options

@REQ-TF-022
Scenario: A date question's Allow future dates setting survives an export and reimport
  Given a live date question that allows future dates and another that does not
  When an Administrator exports it and imports the result back in
  Then each date question's draft allows future dates exactly as the original did
  And a date field in a plain Typeform file, with no hpac object, imports without allowing future dates

@REQ-TF-021
Scenario: Only an Administrator may import or export
  Given a member does not have the Administrator role
  When that member attempts to import or export
  Then the API rejects both attempts
