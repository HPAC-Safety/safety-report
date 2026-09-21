Feature: Question bank and form
  Questions are stored as a sequence of complete, immutable bilingual
  revisions. Every administrator edit creates a new revision instead of
  patching an existing one, so a report always answers a specific, historical
  revision.

  Background:
    Given the question bank stores each question as a stable, non-localized key
    And each revision has a monotonically increasing revision number for its key

  @ignore
  Scenario: Editing a question creates a new revision instead of mutating one
    Given an active question revision exists for a stable key
    When an Administrator changes its wording, help text, translations, options, type, order, section, privacy, active state, required state, or system state
    Then a new complete revision is created with the next revision number
    And the previous revision is left unchanged

  @ignore
  Scenario: Only an active Administrator may create a revision
    Given a member is not an active Administrator
    When that member attempts to save a question revision
    Then the API rejects the attempt

  @ignore
  Scenario: Editing a question copies the latest revision into a new one
    Given an active Administrator requests to edit a question with an existing revision
    When the API prepares the edit DTO
    Then it loads the latest revision and copies all fields into that DTO
    When the Administrator saves the edit
    Then the API validates both languages and all options, then saves a new complete row rather than patching the existing revision

  @ignore
  Scenario: Only the latest active, non-deleted revision is shown on the form
    Given a stable key has multiple revisions
    And only one of them is both active and not deleted
    When the API assembles the current form
    Then that revision is the one included for the key
    And an older active revision never reappears after a later revision deactivates or deletes the question

  @ignore
  Scenario: Form questions are ordered deterministically
    Given the current form includes several question revisions
    When the API orders them for display
    Then they are ordered by sort order
    And ties are broken by stable key

  @ignore
  Scenario: consent_publish is the only question that can never be optional
    Given the form is assembled for a reporter
    When the reporter submits without an answer to consent_publish
    Then the API rejects the submission
    And an Administrator cannot save a consent_publish revision that is optional

  Scenario: An Administrator chooses whether an ordinary question must be answered
    Given an Administrator authors an ordinary question
    When they mark it as one reporters must answer
    Then the new revision records that it is required
    And marking it optional again records that on a further new revision

  @ignore
  Scenario: consent_publish must resolve to an explicit yes or no
    Given the consent_publish revision has no preselected value
    When the submitted value is absent, null, of the wrong type, or does not resolve to an explicit yes or no
    Then the API rejects the submission

  @ignore
  Scenario: Skipping an ordinary question still records that it was shown
    Given a reporter is shown an optional answer-producing revision
    When the reporter leaves it blank
    Then the submission DTO records an answer entry for that revision with a nullable value or empty option selection
    And no value is synthesized

  @ignore
  Scenario: Statements and groups never produce answer entries
    Given a statement or group/section revision is shown on the form
    When the reporter submits the form
    Then no answer entry exists for that revision

  @ignore
  Scenario: A skipped file-upload question produces an answer with no attachment
    Given a file-upload question revision is shown and left empty
    When the reporter submits the form
    Then an answer entry exists for that revision with no associated attachment parts

  @ignore
  Scenario: Only consent is projected onto the report aggregate
    Given a submitted report has answers to several ordinary questions
    When those answers are persisted
    Then only the consent_publish answer is projected onto the report aggregate as a publication invariant
    And every other answer, including dates, times, provinces, injury severities, and aircraft details, remains a revision-bound answer interpreted through its question key and revision metadata

  @ignore
  Scenario: Privacy is a property of the revision, not the answer
    Given an answer is created against a private question revision
    When the answer is persisted
    Then it stores the exact revision identifier and a privacy snapshot
    And the answer is available only to authorized admin flows and to the Worker as labeled recognition context
    And it never becomes public content

  @ignore
  Scenario: Creating a revision preserves the question bank invariants
    Given an Administrator saves a new revision
    Then the stable key is a non-empty, unique, non-localized identifier
    And both English and French labels are present for an answer-producing question
    And an option-requiring type has at least one valid bilingual option and every other type has none
    And option codes are unique within the revision
    And only consent_publish may be marked system
    And the consent_publish revision is active, yes/no, private, and excluded from summary input despite being stored as an answer

  @ignore
  Scenario: A report may answer a known superseded revision
    Given a reporter's browser session began before an Administrator edited the form
    And the browser still references the previously shown, non-deleted revision
    When the reporter submits the form
    Then the API validates the answer against that superseded revision's historical type, options, and privacy
    And accepts the submission

  @ignore
  Scenario: Unknown or deleted revisions are rejected at submission
    Given a submitted answer references a revision ID that is unknown or has been deleted
    When the API validates the submission
    Then the API rejects the submission

  @ignore
  Scenario: A revision can be soft-deleted only when no answer references it
    Given a question revision has never been referenced by any answer, including answers on deleted reports
    When an Administrator deletes it
    Then the deletion succeeds

  @ignore
  Scenario: A referenced revision can never be deleted
    Given a question revision is referenced by at least one answer, including an answer on a deleted report
    When an Administrator attempts to delete it
    Then the deletion is rejected
    And the revision remains available as history indefinitely
    And deactivating it through a new revision is the normal way to remove it from future forms

  Scenario: A shared choice list is copied into the revision that uses it
    Given a shared choice list offers several bilingual options
    When an Administrator saves a question revision that uses that list
    Then the revision holds its own complete copy of those options
    And each copy records the shared item it came from

  Scenario: Editing a shared choice list never changes a revision already built from it
    Given a question revision was built from a shared choice list
    When an Administrator relabels an option, adds one, and removes another from that list
    Then the existing revision still offers exactly the options it was saved with
    And a revision saved afterwards offers the edited list instead

  Scenario: Removing an option from a shared list keeps every snapshot of it
    Given a question revision copied an option from a shared choice list
    When an Administrator removes that option from the list
    Then the option is retired from the list rather than erased
    And the revision's copy of it is unchanged

  Scenario: A question can be made conditional only on a yes/no question
    Given an active question asks for something other than yes or no
    When an Administrator tries to make another question conditional on it
    Then the attempt is rejected
    And a yes/no question is accepted as the condition instead

  Scenario: A question cannot be conditional on itself or form a cycle
    Given a question is already conditional on a yes/no question
    When an Administrator tries to make that yes/no question conditional on it
    Then the attempt is rejected
    And a question offered as its own condition is rejected the same way

  Scenario: Publication consent can never be made conditional
    Given the consent_publish question exists
    When an Administrator tries to make it conditional on another question
    Then the attempt is rejected

  Scenario: Rearranging the form writes a new revision for every question that moved
    Given several active questions sit in a known order
    When an Administrator rearranges them
    Then each question that moved has a new revision recording its new position
    And a question that did not move keeps its current revision
    And no two questions are left claiming the same position

  Scenario Outline: A question type either takes options or does not
    Given an Administrator authors a <type> question
    When they supply bilingual options with it
    Then the revision <outcome>

    Examples:
      | type         | outcome                  |
      | autocomplete | stores those options     |
      | single_select| stores those options     |
      | multi_select | stores those options     |
      | time         | is rejected              |
      | short_text   | is rejected              |
      | yes_no       | is rejected              |

  Scenario: A question key is normalized and cannot be reused
    Given an Administrator authors a question with a loosely typed key
    Then the stored key is lowercase and underscore-separated
    And a key that reduces to nothing at all is rejected

  Scenario: Retiring a question keeps it and its history
    Given an active question has been asked
    When an Administrator deletes it
    Then the question is stamped as deleted rather than removed
    And it refuses any further revision

  Scenario: Publication consent can never be deleted or deactivated
    Given the consent_publish question exists
    When an Administrator tries to delete it
    Then the attempt is rejected
    And trying to stop asking it is rejected the same way

  Scenario: A retired choice list refuses further edits
    Given a shared choice list offers several bilingual options
    When an Administrator retires the whole list
    Then its options are retired with it
    And adding, renaming, or rearranging it is rejected

  Scenario: A choice list is rearranged as a whole or not at all
    Given a shared choice list offers several bilingual options
    When an Administrator arranges every option into a new order
    Then the list takes that order
    And an arrangement that omits or repeats an option is rejected

  Scenario: Translation is offered only for question wording
    Given an Administrator is authoring a question in one official language
    When they ask for the other language to be translated
    Then the request goes to the application's own API rather than to a provider from the browser
    And the translated text is returned as a draft that is not saved anywhere
    And no report, answer, or summary is ever translated this way

  Scenario: A server with no translation credential still authors questions
    Given no translation provider is configured outside development
    When the authoring screen asks whether translation is available
    Then it is told that translation is unavailable
    And the answer carries no credential and no provider detail

  Scenario: A development server translates through a stand-in rather than refusing
    Given a development server has no translation provider configured
    When an Administrator asks for the other language to be translated
    Then the text comes back unchanged through the same interface
    And the screen is told it is a stand-in so nobody mistakes it for a translation
    And a server outside development never substitutes one

  @ui
  Scenario: An Administrator drafts the French from the English
    Given a signed-in Administrator is authoring a new question
    When they write the English wording and press Translate
    Then the French field is filled with the translation
    And the French field remains editable

  @ui
  Scenario: An Administrator drafts the English from the French
    Given a signed-in Administrator is authoring a new question
    When they write the French wording and press Translate
    Then the English field is filled with the translation

  @ui
  Scenario: A question cannot be saved in one language
    Given a signed-in Administrator is authoring a new question
    When only one official language has been written
    Then saving is unavailable
    When the other language is written as well
    Then saving becomes available

  @ui
  Scenario: Translation is not offered when the server has no provider
    Given a signed-in Administrator is authoring a question on a server with no translation provider
    Then the Translate action is unavailable and says so

  @ui
  Scenario: A development stand-in says what it is
    Given a signed-in Administrator is authoring a question on a development server
    Then the Translate action works and the screen says the text is copied unchanged

  @ui
  Scenario: An Administrator authors a question from the dashboard
    Given a signed-in Administrator opens the manage-questions page
    When they add a paragraph-text question in both official languages
    Then the new question appears in the list with its type and version

  @ui
  Scenario: The options editor appears only for a type that takes options
    Given a signed-in Administrator is authoring a new question
    When they choose the type-ahead list type
    Then the page offers a shared choice list and an option editor
    When they choose the single-line text type instead
    Then the page offers neither

  @ui
  Scenario: Only yes/no questions are offered as a condition
    Given a signed-in Administrator is authoring a new question
    Then the condition picker offers only the yes/no questions on the form

  @ui
  Scenario: Questions are reordered from the keyboard
    Given a signed-in Administrator opens the manage-questions page
    When they move the second question up using its move-up control
    Then the two questions have swapped places in the list

  @ui
  Scenario: Editing a question from the dashboard shows its new version
    Given a signed-in Administrator opens the manage-questions page
    When they edit the first question's English wording and save
    Then the list shows the new wording and a higher version number

  @ui
  Scenario: Deleting a question removes it from the list
    Given a signed-in Administrator opens the manage-questions page
    When they delete the second question
    Then it is gone from the list

  @ui
  Scenario: A rejected save tells the Administrator why
    Given a signed-in Administrator is authoring a new question
    When they save a question whose key is already in use
    Then the page shows the reason the save was refused
    And the question is not added to the list

  @ui
  Scenario: The editor carries an existing question's settings into the form
    Given a signed-in Administrator opens the manage-questions page
    When they open the first question for editing
    Then the form is filled with its current wording, type, and behaviour
    And its key cannot be changed
