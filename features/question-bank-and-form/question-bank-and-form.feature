Feature: Question bank and form
  Questions are stored as a sequence of complete, immutable bilingual
  revisions. Every administrator edit creates a new revision instead of
  patching an existing one, so a report always answers a specific, historical
  revision.

  Background:
    Given the question bank stores each question as a stable, non-localized key
    And each revision has a monotonically increasing revision number for its key

  Scenario: Editing a question creates a new revision instead of mutating one
    Given an active question revision exists for a stable key
    When an Administrator changes its wording, help text, translations,
      options, type, order, section, privacy, active state, required state,
      or system state
    Then a new complete revision is created with the next revision number
    And the previous revision is left unchanged

  Scenario: Only the latest active, non-deleted revision is shown on the form
    Given a stable key has multiple revisions
    And only one of them is both active and not deleted
    When the API assembles the current form
    Then that revision is the one included for the key
    And an older active revision never reappears after a later revision
      deactivates or deletes the question

  Scenario: Form questions are ordered deterministically
    Given the current form includes several question revisions
    When the API orders them for display
    Then they are ordered by sort order
    And ties are broken by stable key

  Scenario: consent_publish is the only required question
    Given the form is assembled for a reporter
    When the reporter submits without an answer to consent_publish
    Then the API rejects the submission
    And every other answer-producing question may be skipped without blocking
      submission

  Scenario: consent_publish must resolve to an explicit yes or no
    Given the consent_publish revision has no preselected value
    When the submitted value is absent, null, of the wrong type, or does not
      resolve to an explicit yes or no
    Then the API rejects the submission

  Scenario: Skipping an ordinary question still records that it was shown
    Given a reporter is shown an optional answer-producing revision
    When the reporter leaves it blank
    Then the submission DTO records an answer entry for that revision with a
      nullable value or empty option selection
    And no value is synthesized

  Scenario: Statements and groups never produce answer entries
    Given a statement or group/section revision is shown on the form
    When the reporter submits the form
    Then no answer entry exists for that revision

  Scenario: A skipped file-upload question produces an answer with no attachment
    Given a file-upload question revision is shown and left empty
    When the reporter submits the form
    Then an answer entry exists for that revision with no associated
      attachment parts

  Scenario: Only consent is projected onto the report aggregate
    Given a submitted report has answers to several ordinary questions
    When those answers are persisted
    Then only the consent_publish answer is projected onto the report
      aggregate as a publication invariant
    And every other answer, including dates, times, provinces, injury
      severities, and aircraft details, remains a revision-bound answer
      interpreted through its question key and revision metadata

  Scenario: Privacy is a property of the revision, not the answer
    Given an answer is created against a private question revision
    When the answer is persisted
    Then it stores the exact revision identifier and a privacy snapshot
    And the answer is available only to authorized admin flows and to the
      Worker as labeled recognition context
    And it never becomes public content

  Scenario: Creating a revision preserves the question bank invariants
    Given an Administrator saves a new revision
    Then the stable key is a non-empty, unique, non-localized identifier
    And both English and French labels are present for an answer-producing
      question
    And an option-requiring type has at least one valid bilingual option and
      every other type has none
    And option codes are unique within the revision
    And only consent_publish may be marked system or required
    And the consent_publish revision is active, yes/no, private, and excluded
      from summary input despite being stored as an answer

  Scenario: A report may answer a known superseded revision
    Given a reporter's browser session began before an Administrator edited the
      form
    And the browser still references the previously shown, non-deleted
      revision
    When the reporter submits the form
    Then the API validates the answer against that superseded revision's
      historical type, options, and privacy
    And accepts the submission

  Scenario: Unknown or deleted revisions are rejected at submission
    Given a submitted answer references a revision ID that is unknown or has
      been deleted
    When the API validates the submission
    Then the API rejects the submission

  Scenario: A revision can be soft-deleted only when no answer references it
    Given a question revision has never been referenced by any answer,
      including answers on deleted reports
    When an Administrator deletes it
    Then the deletion succeeds

  Scenario: A referenced revision can never be deleted
    Given a question revision is referenced by at least one answer, including
      an answer on a deleted report
    When an Administrator attempts to delete it
    Then the deletion is rejected
    And the revision remains available as history indefinitely
