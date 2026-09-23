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

@REQ-QB-001
@ignore
Scenario: Editing an unanswered question creates a new revision instead of mutating one
  Given an active question revision exists for a stable key
  And no answer references that question
  When an Administrator changes its wording, help text, translations, type, order, privacy, active state, required state, or system state
  Then a new complete revision is created with the next revision number
  And the previous revision is left unchanged
  And the question keeps its identifier

@REQ-QB-002
@ignore
Scenario: Editing an answered question retires it and creates a new one
  Given a question has been answered on at least one report
  When an Administrator changes its wording
  Then the original question is stamped as deleted
  And a new question is created with a new identifier
  And the new question carries the same stable key
  And the new question starts its own revision numbering
  And the answers already given still refer to the retired question and its original wording

@REQ-QB-003
@ignore
Scenario: An answer on a deleted report still forces a fork
  Given the only answer to a question is on a report that has been deleted
  When an Administrator changes that question's wording
  Then the original question is stamped as deleted
  And a new question is created with a new identifier

@REQ-QB-004
@ignore
Scenario: A retired question can never be brought back
  Given a question has been stamped as deleted
  When anything attempts to restore, revive, or revise it
  Then the attempt is rejected
  And an Administrator who wants it back authors it again as a new question

@REQ-QB-005
@ignore
Scenario: Only one question per key is live at a time
  Given a stable key has a retired question and a live one
  When anything resolves that key
  Then it resolves to the live question
  And a second live question for the same key is rejected

@REQ-QB-006
@ignore
Scenario: Publication consent revises in place even when answered
  Given the consent_publish question has been answered on at least one report
  When an Administrator changes its wording
  Then a new revision is created for it
  And the question keeps its identifier
  And it is never stamped as deleted

@REQ-QB-007
@ignore
Scenario: Only an Administrator may create a revision
  Given a member does not have the Administrator role
  When that member attempts to save a question revision
  Then the API rejects the attempt

@REQ-QB-008
@ignore
Scenario: Editing a question copies the latest revision into a new one
  Given an Administrator requests to edit a question with an existing revision
  When the API prepares the edit DTO
  Then it loads the latest revision and copies all fields into that DTO
  When the Administrator saves the edit
  Then the API validates both languages, then saves a new complete row rather than patching the existing revision

@REQ-QB-009
Scenario: Only the latest active, non-deleted revision is shown on the form
  Given a stable key has multiple revisions
  And only one of them is both active and not deleted
  When the API assembles the current form
  Then that revision is the one included for the key
  And an older active revision never reappears after a later revision deactivates or deletes the question

@REQ-QB-010
Scenario: Form questions are ordered deterministically
  Given the current form includes several question revisions
  When the API orders them for display
  Then they are ordered by sort order
  And ties are broken by stable key

@REQ-QB-011
Scenario: The current form is public
  Given no bearer token is presented
  When a request asks for the current form
  Then the API answers rather than refusing the request

@REQ-QB-012
Scenario: A group question's response nests its children rather than repeating them
  Given a live group question exists as a section heading
  And another live question is grouped under it
  When the API assembles the current form
  Then the group's entry carries that question as a child, in order
  And the child does not also appear as its own top-level entry

@REQ-QB-013
Scenario: The current form's response includes a question's conditional dependency
  Given a question is conditional on a yes-or-no question
  When the API assembles the current form
  Then the conditional question's entry names the question it depends on

@REQ-QB-014
@ignore
Scenario: consent_publish is the only question that can never be optional
  Given the form is assembled for a reporter
  When the reporter submits without an answer to consent_publish
  Then the API rejects the submission
  And an Administrator cannot save a consent_publish revision that is optional

@REQ-QB-015
Scenario: An Administrator chooses whether an ordinary question must be answered
  Given an Administrator authors an ordinary question
  When they mark it as one reporters must answer
  Then the new revision records that it is required
  And marking it optional again records that on a further new revision

@REQ-QB-016
@ignore
Scenario: consent_publish must resolve to an explicit yes or no
  Given the consent_publish revision has no preselected value
  When the submitted value is absent, null, of the wrong type, or does not resolve to an explicit yes or no
  Then the API rejects the submission

@REQ-QB-017
@ignore
Scenario: Skipping an ordinary question still records that it was shown
  Given a reporter is shown an optional answer-producing revision
  When the reporter leaves it blank
  Then the submission DTO records an answer entry for that revision with a null value
  And no value is synthesized

@REQ-QB-018
@ignore
Scenario: An answer to a picker stores the words the reporter saw
  Given a reporter is shown a picker, type-ahead, or multi-select question
  When the reporter chooses a value and submits
  Then the stored answer holds that value's label exactly as it was shown
  And it holds no option code and no reference to an option row
  And relabelling or removing that option afterwards leaves the stored answer unchanged

@REQ-QB-019
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

@REQ-QB-020
@ignore
Scenario: A select answer records the reporter's language and waits for the other
  Given a reporter answering in French chooses a value from a curated list
  When the answer is persisted
  Then the stored value is the French label they saw
  And the answer records that it was given in French
  And the answer is flagged for an Administrator to supply English
  And nothing on the submission path translates it

@REQ-QB-021
@ignore
Scenario: A curated list's other language is not copied onto the answer
  Given a curated choice offers both official languages
  When a reporter picks it in one language
  Then the stored answer holds only the language they saw
  And it is flagged for translation like any other select answer

@REQ-QB-022
@ignore
Scenario: An Administrator supplies the second language of an answer
  Given a stored answer is flagged for translation
  When an Administrator types the other language, or presses Translate and saves
  Then the answer holds both languages
  And it is no longer flagged
  And the value the reporter gave is unchanged

@REQ-QB-023
@ignore
Scenario: Only an Administrator may translate
  Given a member does not have the Administrator role
  When that member requests a translation
  Then the API rejects the request

@REQ-QB-024
@ignore
Scenario: A skipped file-upload question produces an answer with no attachment
  Given a file-upload question revision is shown and left empty
  When the reporter submits the form
  Then an answer entry exists for that revision with no associated attachment parts

@REQ-QB-025
@ignore
Scenario: Only consent is projected onto the report aggregate
  Given a submitted report has answers to several ordinary questions
  When those answers are persisted
  Then only the consent_publish answer is projected onto the report aggregate as a publication invariant
  And every other answer, including dates, times, provinces, injury severities, and aircraft details, remains a stored string read through its question key

@REQ-QB-026
@ignore
Scenario: Privacy is a property of the revision, not the answer
  Given an answer is created against a private question revision
  When the answer is persisted
  Then it stores the exact revision identifier and a privacy snapshot
  And the answer is available only to authorized admin flows and to the Worker as labeled recognition context
  And it never becomes public content

@REQ-QB-027
@ignore
Scenario: Creating a revision preserves the question bank invariants
  Given an Administrator saves a new revision
  Then the stable key is a non-empty, unique, non-localized identifier
  And both English and French labels are present for an answer-producing question
  And an option-requiring type has at least one live choice and every other type has none
  And only consent_publish may be marked system
  And the consent_publish revision is active, yes/no, private, and excluded from summary input despite being stored as an answer

@REQ-QB-028
@ignore
Scenario: A report may answer a known superseded revision
  Given a reporter's browser session began before an Administrator edited the form
  And the browser still references the previously shown, non-deleted revision
  When the reporter submits the form
  Then the API validates the answer against that superseded revision's historical type and privacy and the question's live choices
  And accepts the submission

@REQ-QB-029
@ignore
Scenario: Unknown or deleted revisions are rejected at submission
  Given a submitted answer references a revision ID that is unknown or has been deleted
  When the API validates the submission
  Then the API rejects the submission

@REQ-QB-030
Scenario: A revision can be soft-deleted only when no answer references it
  Given a question revision has never been referenced by any answer, including answers on deleted reports
  When an Administrator deletes it
  Then the deletion succeeds

@REQ-QB-031
Scenario: A referenced revision can never be deleted
  Given a question revision is referenced by at least one answer, including an answer on a deleted report
  When an Administrator attempts to delete it
  Then the deletion is rejected
  And the revision remains available as history indefinitely
  And deactivating it through a new revision is the normal way to remove it from future forms

@REQ-QB-035
Scenario: A reporter adds a choice the type-ahead did not offer
  Given a type-ahead question offers several choices
  When a reporter submits an answer naming a site the question does not offer
  Then the question gains the site as a reporter-added choice
  And it carries the language the reporter typed it in
  And it is marked for an Administrator to supply the other language
  And the next reporter is offered it

@REQ-QB-036
Scenario: Two reporters naming the same new site produce one choice
  Given a reporter has already added a site to a type-ahead question
  When another reporter submits the same site name
  Then the existing choice is reused rather than duplicated
  And an administrator's wording is never replaced by a reporter's

@REQ-QB-037
Scenario: A choice an administrator removed is not revived by a reporter
  Given an Administrator removed a reporter-added choice from a type-ahead question
  When a reporter submits that same value again
  Then the choice stays removed from the question
  And the reporter's answer still records the value they typed

@REQ-QB-097
Scenario Outline: Only a type-ahead grows from reporters' answers
  Given a published <type> question offers several choices
  When a reporter submits a value the question does not offer
  Then <outcome>

Examples:
  | type          | outcome                                                     |
  | autocomplete  | the report is accepted and the question gains the value     |
  | single_select | the submission is rejected and the question is unchanged    |
  | multi_select  | the submission is rejected and the question is unchanged    |

@REQ-QB-044
Scenario Outline: A statement or a group collects no answer
  Given an Administrator authors a <type> question
  Then it cannot be marked required or private
  And it cannot be made conditional on another question
  And it cannot be the condition for another question

Examples:
  | type      |
  | statement |
  | group     |

@REQ-QB-045
@ignore
Scenario Outline: A statement or a group is excluded from a submission's answer-producing revisions
  Given an Administrator authors a <type> question
  When a reporter is shown the form and submits it
  Then it does not appear in the set of answer-producing revisions the submission records

Examples:
  | type      |
  | statement |
  | group     |

@REQ-QB-046
Scenario: A question may be grouped under a group question
  Given a group question exists as a section heading
  When an Administrator makes another question grouped under it
  Then the question's saved revision names that group as its heading

@REQ-QB-047
@ui
Scenario: A form renders a question together with its group heading and siblings
  Given a group question exists as a section heading
  And another question is grouped under it
  When a reporter is shown the form
  Then that question renders together with the group heading and its other children

@REQ-QB-048
Scenario: Only a group question may be a grouping parent
  Given a question that is not a group
  When an Administrator tries to group another question under it
  Then the attempt is rejected

@REQ-QB-049
Scenario: A group cannot itself be grouped under another group
  Given two group questions exist
  When an Administrator tries to group one under the other
  Then the attempt is rejected

@REQ-QB-050
Scenario: A question cannot be grouped under itself
  Given a group question exists
  When an Administrator tries to group it under itself
  Then the attempt is rejected

@REQ-QB-051
Scenario: Grouping is unaffected by conditional dependency and vice versa
  Given a question is both conditional on a yes/no question and grouped under a group question
  When an Administrator reads its saved revision
  Then both facts are recorded independently
  And clearing one leaves the other unchanged

@REQ-QB-052
@ignore
Scenario: Regrouping follows a parent that stops being a group
  Given a question is grouped under a group question
  When an Administrator retypes that parent away from the group type, or deletes it
  Then the child's next revision is ungrouped rather than naming a heading that no longer exists

@REQ-QB-053
Scenario: A question can be made conditional only on a yes/no or single-select question
  Given an active question asks for something other than yes/no or single-select
  When an Administrator tries to make another question conditional on it
  Then the attempt is rejected
  And a yes/no question is accepted as the condition instead
  And a single-select question naming one of its live options is accepted as the condition instead

@REQ-QB-054
Scenario: A single-select parent's dependency records the required option
  Given a single-select question asking whether the pilot flies hang gliders or paragliders
  When an Administrator makes a rating question depend on the "hang glider" option
  And an Administrator makes a different rating question depend on the "paraglider" option
  Then each rating question's saved dependency names its own required option

@REQ-QB-055
Scenario: A single-select dependency must name one of the parent's live choices
  Given a single-select question offering hang glider and paraglider
  When an Administrator tries to make another question depend on a choice the parent does not offer
  Then the attempt is rejected

@REQ-QB-056
Scenario: A yes/no dependency does not name an option
  Given a yes/no question
  When an Administrator makes another question depend on it
  Then the dependency needs no required option, because the condition is always "answered yes"

@REQ-QB-057
Scenario: A question cannot be conditional on itself or form a cycle
  Given a question is already conditional on a yes/no question
  When an Administrator tries to make that yes/no question conditional on it
  Then the attempt is rejected
  And a question offered as its own condition is rejected the same way

@REQ-QB-058
Scenario: Publication consent can never be made conditional
  Given the consent_publish question exists
  When an Administrator tries to make it conditional on another question
  Then the attempt is rejected

@REQ-QB-059
Scenario: Rearranging the form writes a new revision for every question that moved
  Given several active questions sit in a known order
  When an Administrator rearranges them
  Then each question that moved has a new revision recording its new position
  And a question that did not move keeps its current revision
  And no two questions are left claiming the same position

@REQ-QB-060
Scenario Outline: A question type either takes options or does not
  Given an Administrator authors a <type> question
  When they supply bilingual choices with it
  Then the question <outcome>

Examples:
  | type          | outcome              |
  | autocomplete  | stores those choices |
  | single_select | stores those choices |
  | multi_select  | stores those choices |
  | time          | is rejected          |
  | short_text    | is rejected          |
  | yes_no        | is rejected          |

@REQ-QB-061
Scenario: A question key is normalized and cannot be reused
  Given an Administrator authors a question with a loosely typed key
  Then the stored key is lowercase and underscore-separated
  And a key that reduces to nothing at all is rejected

@REQ-QB-062
Scenario: Retiring a question keeps it and its history
  Given an active question nobody has answered
  When an Administrator deletes it
  Then the question is stamped as deleted rather than removed
  And it refuses any further revision

@REQ-QB-063
Scenario: Publication consent can never be deleted or deactivated
  Given the consent_publish question exists
  When an Administrator tries to delete it
  Then the attempt is rejected
  And trying to stop asking it is rejected the same way
  And an ordinary edit that clears its active flag is rejected the same way

@REQ-QB-066
Scenario: Translation is offered for question wording and for a select answer's second language
  Given an Administrator is authoring a question in one official language
  When they ask for the other language to be translated
  Then the request goes to the application's own API rather than to a provider from the browser
  And the translated text is returned as a draft that is not saved anywhere
  And the same action is available for the second language of a select answer awaiting translation
  And no narrative, free-text answer, or summary is ever translated this way
  And nothing is translated unless an Administrator asked for it

@REQ-QB-067
Scenario: A server with no translation credential still authors questions
  Given no translation provider is configured outside development
  When the authoring screen asks whether translation is available
  Then it is told that translation is unavailable
  And the answer carries no credential and no provider detail

@REQ-QB-068
Scenario: A development server translates through a stand-in rather than refusing
  Given a development server has no translation provider configured
  When an Administrator asks for the other language to be translated
  Then the text comes back unchanged through the same interface
  And the screen is told it is a stand-in so nobody mistakes it for a translation
  And a server outside development never substitutes one

@REQ-QB-069
@ui
Scenario: An Administrator drafts the French from the English
  Given a signed-in Administrator is authoring a new question
  When they write the English wording and press Translate
  Then the French field is filled with the translation
  And the French field remains editable

@REQ-QB-070
@ui
Scenario: An Administrator drafts the English from the French
  Given a signed-in Administrator is authoring a new question
  When they write the French wording and press Translate
  Then the English field is filled with the translation

@REQ-QB-071
@ui
Scenario: A question cannot be saved in one language
  Given a signed-in Administrator is authoring a new question
  When only one official language has been written
  Then saving is unavailable
  When the other language is written as well
  Then saving becomes available

@REQ-QB-072
@ui
Scenario: Translation is not offered when the server has no provider
  Given a signed-in Administrator is authoring a question on a server with no translation provider
  Then the Translate action is unavailable and says so

@REQ-QB-073
@ui
Scenario: A development stand-in says what it is
  Given a signed-in Administrator is authoring a question on a development server
  Then the Translate action works and the screen says the text is copied unchanged

@REQ-QB-074
@ui
Scenario: An Administrator sees which choices reporters added
  Given a signed-in Administrator opens the manage-questions page
  Then a type-ahead question with reporter-added choices says how many are waiting to be reviewed
  When they open that question
  Then each reporter-added choice is marked as such

@REQ-QB-075
@ui
Scenario: An Administrator corrects a reporter-added choice
  Given a signed-in Administrator opens a type-ahead question with a reporter-added choice
  When they correct the wording of that choice and save it
  Then the corrected wording is shown on the question

@REQ-QB-076
@ui
Scenario: An Administrator authors a question from the dashboard
  Given a signed-in Administrator opens the manage-questions page
  When they add a paragraph-text question in both official languages
  Then the new question appears in the list with its type and version

@REQ-QB-077
@ui
Scenario: The options editor appears only for a type that takes options
  Given a signed-in Administrator is authoring a new question
  When they choose the type-ahead list type
  Then the page offers an option editor
  When they choose the single-line text type instead
  Then the page offers neither

@REQ-QB-078
@ui
Scenario: Only yes/no and single-select questions are offered as a condition
  Given a signed-in Administrator is authoring a new question
  Then the condition picker offers only the yes/no and single-select questions on the form

@REQ-QB-079
@ui
Scenario: Naming a required option appears only for a single-select condition
  Given a signed-in Administrator is authoring a new question
  When they choose a yes/no question as the condition
  Then no required-option control is offered
  When they choose a single-select question as the condition instead
  Then a required-option control offers that question's live options

@REQ-QB-080
@ui
Scenario: Questions are reordered from the keyboard
  Given a signed-in Administrator opens the manage-questions page
  When they move the second question up using its move-up control
  Then the two questions have swapped places in the list

@REQ-QB-081
@ui
Scenario: Editing an unanswered question from the dashboard shows its new version
  Given a signed-in Administrator opens the manage-questions page
  And the first question has never been answered
  When they edit its English wording and save
  Then the list shows the new wording and a higher version number

@REQ-QB-082
@ui
Scenario: Editing an answered question warns that it will be replaced
  Given a signed-in Administrator opens the manage-questions page
  And the first question has been answered
  When they edit its English wording
  Then the page says that saving retires this question and creates a new one
  When they save
  Then the list shows one question for that key, with the new wording

@REQ-QB-083
@ui
Scenario: An Administrator sees answers awaiting a second language
  Given a signed-in Administrator opens the answers-awaiting-translation page
  Then each answer is listed with its question, its value, and the language it was given in
  And the page says how many are waiting

@REQ-QB-084
@ui
Scenario: An Administrator translates an answer from the queue
  Given a signed-in Administrator opens the answers-awaiting-translation page
  When they press Translate on the first answer and save
  Then that answer leaves the queue
  And the value the reporter gave is unchanged

@REQ-QB-085
@ui
Scenario: Deleting a question removes it from the list
  Given a signed-in Administrator opens the manage-questions page
  When they delete the second question
  Then it is gone from the list

@REQ-QB-086
@ui
Scenario: A rejected save tells the Administrator why
  Given a signed-in Administrator is authoring a new question
  When they save a question whose two choices read alike
  Then the page shows the reason the save was refused
  And the question is not added to the list

@REQ-QB-087
@ui
Scenario: The editor carries an existing question's settings into the form
  Given a signed-in Administrator opens the manage-questions page
  When they open the first question for editing
  Then the form is filled with its current wording, type, and behaviour
  And no question key is shown

@REQ-QB-088
@ui
Scenario: Reviewing an imported Typeform draft prefills the editor
  Given a signed-in Administrator opens the manage-questions page
  When they import a Typeform English and French export pair
  Then the imported drafts are listed
  When they choose to review the first imported draft
  Then the editor is filled with that draft's type and both languages

@REQ-QB-089
@ui
Scenario: An Administrator downloads the question bank as Typeform JSON
  Given a signed-in Administrator opens the manage-questions page
  When they choose to export the question bank
  Then a zip file download begins

@REQ-QB-090
@ui
Scenario: An Administrator writes a question's choice by its wording alone
  Given a signed-in Administrator is authoring a new question
  When they choose the type-ahead list type
  And they add a choice
  Then the choice asks only for its English and French wording
  When they save the question with that choice
  Then the choice is sent without a code

@REQ-QB-092
Scenario: A choice an Administrator writes is recorded under a code derived from its English wording
  Given an Administrator saves a single-select question with the choices "King Eddy" and "Mara"
  Then the choices are recorded under the codes "king_eddy" and "mara"
  When they reword "King Eddy" to "King Edward" and save again
  Then that choice is still recorded under the code "king_eddy"
  When they save choices whose English wording reads "Site A-1" and "Site A 1"
  Then the save is refused naming both wordings

@REQ-QB-094
Scenario: A reporter answering in French adds a choice recorded in French only
  Given a type-ahead question offers several choices
  When a reporter answering in French submits "Élévation Sainte-Anne", which the question does not offer
  Then the question gains a reporter-added choice whose French wording is "Élévation Sainte-Anne"
  And the choice has no English wording until an Administrator supplies it
  And the choice records that it was typed in French
  And its code is "elevation_sainte_anne", derived from the French wording

@REQ-QB-095
Scenario: Submitting a report records a type-ahead value the question did not offer
  Given a published form has a type-ahead question
  When a reporter answering in French submits a report naming "Élévation Sainte-Anne" in it
  Then the report is accepted
  And the answer is stored as "Élévation Sainte-Anne", in French
  And the question now offers "Élévation Sainte-Anne" as a reporter-added choice coded "elevation_sainte_anne"
  And the next reporter is offered "Élévation Sainte-Anne"

@REQ-QB-096
Scenario: A new question's key is derived from its English wording and never reused
  Given an Administrator saves a new question without a key
  Then its key is derived from its English wording
  When they save another question with the same English wording
  Then it receives a different key
  When they delete the first question and save a third with the same wording
  Then the third question does not take the retired question's key

@REQ-QB-093
@ui
Scenario: Editing a question opens the editor in that question's place
  Given a signed-in Administrator opens the manage-questions page
  When they open the second question for editing
  Then the editor takes the second question's place in the list
  And the editor's top edge lines up with that row's move-up control
  And every other question is still shown in its place
  When they cancel the edit
  Then the second question is shown in its place again

@REQ-QB-098
Scenario: Editing an answered question's wording carries every choice to the replacement
  Given a type-ahead question has been answered on at least one report
  And it offers choices an Administrator wrote and a reporter-added choice
  And an Administrator removed one of its choices
  When an Administrator changes its wording
  Then the replacement question offers every choice the retired one offered
  And the reporter-added choice is still marked as reporter-added
  And the removed choice is carried over and stays removed

@REQ-QB-099
Scenario Outline: Editing an answered question's choices keeps the question and its version
  Given a <type> question has been answered on at least one report
  When an Administrator <edits> its choices
  Then the question offers the edited choices
  And the question keeps its identifier and its current revision
  And the answers already given still record the reporter's own words

Examples:
  | type          | edits                                   |
  | single_select | adds a choice to                        |
  | multi_select  | rewords one of                          |
  | autocomplete  | reorders                                |
  | autocomplete  | supplies the missing language of one of |

@REQ-QB-100
Scenario: A removed choice is hidden from the form and kept in history
  Given a question has been answered with one of its choices
  When an Administrator removes that choice
  Then the form stops offering it
  And the choice is retired rather than erased
  And the answer that named it still records the reporter's own words
  And the question keeps its identifier and its current revision

@REQ-QB-101
Scenario: A choice a live question depends on cannot be removed
  Given a question depends on the "paraglider" choice of a single-select question
  When an Administrator saves the single-select question without that choice
  Then the save is refused naming the dependent question
  And the choice is still offered

@REQ-QB-102
Scenario: A choice in only one language is offered in the language it has
  Given a type-ahead question has a reporter-added choice typed only in English
  When a reporter using French opens the form
  Then the question offers that choice in its English wording
  When an Administrator supplies the choice's French wording
  Then a reporter using French is offered the French wording
  And the choice is no longer waiting to be reviewed

@REQ-QB-103
@ui
Scenario: The report form shows a one-language choice in the language it has
  Given a type-ahead question has a reporter-added choice typed only in English
  When a reporter using French opens that question
  Then the type-ahead offers the choice in its English wording
