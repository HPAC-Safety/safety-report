Feature: Question bank and form
Questions are stored as complete, immutable bilingual revisions. An edit to a
question nobody has answered creates a new revision instead of patching an
existing one. Once an answer exists, an edit retires the question and creates
a new one in its place, so an old answer always correlates to the question as
it was actually worded.

Background:
  Given the question bank stores each question as a stable, non-localized key
  And each revision has a monotonically increasing revision number for its key
  And at most one live question exists for a stable key

@ignore
Scenario: Editing an unanswered question creates a new revision instead of mutating one
  Given an active question revision exists for a stable key
  And no answer references that question
  When an Administrator changes its wording, help text, translations, options, type, order, privacy, active state, required state, or system state
  Then a new complete revision is created with the next revision number
  And the previous revision is left unchanged
  And the question keeps its identifier

@ignore
Scenario: Editing an answered question retires it and creates a new one
  Given a question has been answered on at least one report
  When an Administrator changes its wording
  Then the original question is stamped as deleted
  And a new question is created with a new identifier
  And the new question carries the same stable key
  And the new question starts its own revision numbering
  And the answers already given still refer to the retired question and its original wording

@ignore
Scenario: An answer on a deleted report still forces a fork
  Given the only answer to a question is on a report that has been deleted
  When an Administrator changes that question's wording
  Then the original question is stamped as deleted
  And a new question is created with a new identifier

@ignore
Scenario: A retired question can never be brought back
  Given a question has been stamped as deleted
  When anything attempts to restore, revive, or revise it
  Then the attempt is rejected
  And an Administrator who wants it back authors it again as a new question

@ignore
Scenario: Only one question per key is live at a time
  Given a stable key has a retired question and a live one
  When anything resolves that key
  Then it resolves to the live question
  And a second live question for the same key is rejected

@ignore
Scenario: Publication consent revises in place even when answered
  Given the consent_publish question has been answered on at least one report
  When an Administrator changes its wording
  Then a new revision is created for it
  And the question keeps its identifier
  And it is never stamped as deleted

@ignore
Scenario: Only an Administrator may create a revision
  Given a member does not have the Administrator role
  When that member attempts to save a question revision
  Then the API rejects the attempt

@ignore
Scenario: Editing a question copies the latest revision into a new one
  Given an Administrator requests to edit a question with an existing revision
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
  Then the submission DTO records an answer entry for that revision with a null value
  And no value is synthesized

@ignore
Scenario: An answer to a picker stores the words the reporter saw
  Given a reporter is shown a picker, type-ahead, or multi-select question
  When the reporter chooses a value and submits
  Then the stored answer holds that value's label exactly as it was shown
  And it holds no option code and no reference to an option row
  And relabelling or removing that option afterwards leaves the stored answer unchanged

@ignore
Scenario Outline: Every answer is stored in one invariant written form
  Given a reporter answers a <type> question with <entered>
  When the answer is persisted
  Then the stored value is <stored>

Examples:
  | type       | entered                        | stored              |
  | yes_no     | yes                            | yes                 |
  | yes_no     | oui, in French                 | yes                 |
  | yes_no     | no                             | no                  |
  | date       | the 21st of September 2026     | 2026-09-21          |
  | time       | half past two in the afternoon | 14:30               |
  | short_text | a line of prose                | that line, as typed |

@ignore
Scenario: A select answer records the reporter's language and waits for the other
  Given a reporter answering in French chooses a value from a curated list
  When the answer is persisted
  Then the stored value is the French label they saw
  And the answer records that it was given in French
  And the answer is flagged for an Administrator to supply English
  And nothing on the submission path translates it

@ignore
Scenario: A curated list's other language is not copied onto the answer
  Given a curated choice offers both official languages
  When a reporter picks it in one language
  Then the stored answer holds only the language they saw
  And it is flagged for translation like any other select answer

@ignore
Scenario: An Administrator supplies the second language of an answer
  Given a stored answer is flagged for translation
  When an Administrator types the other language, or presses Translate and saves
  Then the answer holds both languages
  And it is no longer flagged
  And the value the reporter gave is unchanged

@ignore
Scenario: Only an Administrator may translate
  Given a member does not have the Administrator role
  When that member requests a translation
  Then the API rejects the request

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
  And every other answer, including dates, times, provinces, injury severities, and aircraft details, remains a stored string read through its question key

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

Scenario: A reporter adds a choice the type-ahead did not offer
  Given a type-ahead question is backed by a shared choice list
  When a reporter submits an answer naming a site the list does not offer
  Then the site is added to the shared list as a reporter-added choice
  And it carries the language the reporter typed it in
  And it is marked for an Administrator to supply the other language
  And the next reporter is offered it

Scenario: Two reporters naming the same new site produce one choice
  Given a reporter has already added a site to a shared choice list
  When another reporter submits the same site name
  Then the existing choice is reused rather than duplicated
  And an administrator's wording is never replaced by a reporter's

Scenario: A choice an administrator removed is not revived by a reporter
  Given an Administrator removed a choice from a shared list
  When a reporter submits that same value again
  Then the choice stays removed from the list
  And the reporter's answer still records the value they typed

Scenario: A type-ahead offers the live list while its revision records what was shown
  Given a type-ahead revision was saved when the shared list was shorter
  When a choice is added to that list afterwards
  Then the question now offers the longer list
  And the revision still records the shorter one

Scenario Outline: Only a type-ahead reads the live list
  Given a <type> revision is backed by a shared choice list
  When a choice is added to that list afterwards
  Then the question offers <offered>

Examples:
  | type          | offered          |
  | autocomplete  | the live list    |
  | single_select | its own snapshot |
  | multi_select  | its own snapshot |

Scenario: A retired shared list leaves a type-ahead showing what it recorded
  Given a type-ahead revision was built from a shared choice list
  When that shared list is retired entirely
  Then the question still offers the choices its revision recorded

@ignore
Scenario: A multi-select may allow reporter additions the same way a type-ahead does
  Given an Administrator authors a multi-select question backed by a shared choice list
  When they enable reporter additions on it
  Then the question offers the live list the same way a type-ahead does
  And a single-select question offers no such control

@ignore
Scenario: A reporter adds a choice a multi-select did not offer
  Given a multi-select question with reporter additions allowed is backed by a shared choice list
  When a reporter submits a value the list does not offer
  Then the value is added to the shared list as a reporter-added choice
  And it is marked for an Administrator to curate, exactly like a type-ahead's reporter-added choice

@ignore
Scenario: An ordinary multi-select never accepts an unlisted value
  Given a multi-select question does not have reporter additions allowed
  When a reporter submits a value the list does not offer
  Then the submission is rejected

@ignore
Scenario: A statement or a group collects no answer
  Given an Administrator authors a statement or a group question
  Then it cannot be marked required, private, or system
  And it cannot be made conditional on another question or be the condition for one
  And it does not appear in the set of answer-producing revisions a submission must record

@ignore
Scenario: A question may be grouped under a group question
  Given a group question exists as a section heading
  When an Administrator makes another question grouped under it
  Then the form renders that question together with the group heading and its other children

@ignore
Scenario: Only a group question may be a grouping parent
  Given a question that is not a group
  When an Administrator tries to group another question under it
  Then the attempt is rejected

@ignore
Scenario: A group cannot itself be grouped under another group
  Given two group questions exist
  When an Administrator tries to group one under the other
  Then the attempt is rejected

@ignore
Scenario: A question cannot be grouped under itself
  Given a group question exists
  When an Administrator tries to group it under itself
  Then the attempt is rejected

@ignore
Scenario: Grouping is unaffected by conditional dependency and vice versa
  Given a question is both conditional on a yes/no question and grouped under a group question
  When an Administrator reads its saved revision
  Then both facts are recorded independently
  And clearing one leaves the other unchanged

@ignore
Scenario: Regrouping follows a parent that stops being a group
  Given a question is grouped under a group question
  When an Administrator retypes that parent away from the group type, or deletes it
  Then the child's next revision is ungrouped rather than naming a heading that no longer exists

Scenario: A question can be made conditional only on a yes/no or single-select question
  Given an active question asks for something other than yes/no or single-select
  When an Administrator tries to make another question conditional on it
  Then the attempt is rejected
  And a yes/no question is accepted as the condition instead
  And a single-select question naming one of its live options is accepted as the condition instead

Scenario: A single-select parent's dependency records the required option
  Given a single-select question asking whether the pilot flies hang gliders or paragliders
  When an Administrator makes a rating question depend on the "hang glider" option
  And an Administrator makes a different rating question depend on the "paraglider" option
  Then each rating question's saved dependency names its own required option

Scenario: A single-select dependency must name one of the parent's current options
  Given a single-select question offering hang glider and paraglider
  When an Administrator tries to make another question depend on an option the parent does not offer
  Then the attempt is rejected

Scenario: A yes/no dependency does not name an option
  Given a yes/no question
  When an Administrator makes another question depend on it
  Then the dependency needs no required option, because the condition is always "answered yes"

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
  | type          | outcome              |
  | autocomplete  | stores those options |
  | single_select | stores those options |
  | multi_select  | stores those options |
  | time          | is rejected          |
  | short_text    | is rejected          |
  | yes_no        | is rejected          |

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

Scenario: Translation is offered for question wording and for a select answer's second language
  Given an Administrator is authoring a question in one official language
  When they ask for the other language to be translated
  Then the request goes to the application's own API rather than to a provider from the browser
  And the translated text is returned as a draft that is not saved anywhere
  And the same action is available for the second language of a select answer awaiting translation
  And no narrative, free-text answer, or summary is ever translated this way
  And nothing is translated unless an Administrator asked for it

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
Scenario: An Administrator sees which choices reporters added
  Given a signed-in Administrator opens the manage-choice-lists page
  Then each reporter-added choice is marked as such
  And the page says how many are waiting to be reviewed

@ui
Scenario: An Administrator corrects a reporter-added choice
  Given a signed-in Administrator opens the manage-choice-lists page
  When they correct the wording of a reporter-added choice and save
  Then the corrected wording is shown in the list

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
Scenario: Only yes/no and single-select questions are offered as a condition
  Given a signed-in Administrator is authoring a new question
  Then the condition picker offers only the yes/no and single-select questions on the form

@ui
Scenario: Naming a required option appears only for a single-select condition
  Given a signed-in Administrator is authoring a new question
  When they choose a yes/no question as the condition
  Then no required-option control is offered
  When they choose a single-select question as the condition instead
  Then a required-option control offers that question's live options

@ui
Scenario: Questions are reordered from the keyboard
  Given a signed-in Administrator opens the manage-questions page
  When they move the second question up using its move-up control
  Then the two questions have swapped places in the list

@ui
Scenario: Editing an unanswered question from the dashboard shows its new version
  Given a signed-in Administrator opens the manage-questions page
  And the first question has never been answered
  When they edit its English wording and save
  Then the list shows the new wording and a higher version number

@ui
Scenario: Editing an answered question warns that it will be replaced
  Given a signed-in Administrator opens the manage-questions page
  And the first question has been answered
  When they edit its English wording
  Then the page says that saving retires this question and creates a new one
  When they save
  Then the list shows one question for that key, with the new wording

@ui
Scenario: An Administrator sees answers awaiting a second language
  Given a signed-in Administrator opens the answers-awaiting-translation page
  Then each answer is listed with its question, its value, and the language it was given in
  And the page says how many are waiting

@ui
Scenario: An Administrator translates an answer from the queue
  Given a signed-in Administrator opens the answers-awaiting-translation page
  When they press Translate on the first answer and save
  Then that answer leaves the queue
  And the value the reporter gave is unchanged

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
