@xunit:collection(QuestionBankRunsAlone)
Feature: Question authoring
An Administrator authors the question bank: each question is stored as
complete, immutable bilingual revisions. An edit to a question nobody has
answered creates a new revision and leaves the existing one unchanged. Once an
answer exists, an edit retires the question and creates a new one in its
place, so an old answer always correlates to the question as it was
actually worded.

Background:
  Given the question bank stores each question as a stable, non-localized key
  And each revision has a monotonically increasing revision number for its key
  And at most one live question exists for a question key

@REQ-QB-001
Scenario: Editing an unanswered question creates a new revision and changes none
  Given an active question revision exists for a question key
  And no answer references that question
  When an Administrator changes its wording, help text, translations, type, order, privacy, active state, or required state
  Then a new complete revision is created with the next revision number
  And the previous revision is left unchanged
  And the question keeps its identifier

@REQ-QB-002
Scenario: Editing an answered question retires it and creates a new one
  Given a question has been answered on at least one report
  When an Administrator changes its wording
  Then the original question is marked deleted
  And a new question is created with a new identifier
  And the new question carries the same question key
  And the new question starts its own revision numbering
  And the answers already given still refer to the retired question and its original wording

@REQ-QB-003
Scenario: An answer on a deleted report still forces a fork
  Given the only answer to a question is on a report that has been deleted
  When an Administrator changes that question's wording
  Then the original question is marked deleted
  And a new question is created with a new identifier

@REQ-QB-004
Scenario: A deleted question can never be brought back
  Given a question has been marked deleted
  When anything attempts to restore, revive, or revise it
  Then the attempt is refused
  And an Administrator who wants it back authors it again as a new question

@REQ-QB-005
Scenario: Only one question per key is live at a time
  Given a question key has a retired question and a live one
  When anything resolves that key
  Then it resolves to the live question
  And a second live question for the same key is refused

@REQ-QB-006
Scenario: Publication consent revises in place even when answered
  Given the publication consent question has been answered on at least one report
  When an Administrator changes its wording
  Then a new revision is created for it
  And the question keeps its identifier
  And it is never marked deleted

@REQ-QB-008
Scenario: Editing a question copies the latest revision into a new one
  Given an Administrator requests to edit a question with an existing revision
  When the edit is prepared
  Then it starts from the latest revision, with every setting copied
  When the Administrator saves the edit
  Then both languages are validated, and a new complete revision is saved, the existing revision unchanged

@REQ-QB-009
Scenario: Only the latest active, non-deleted revision is shown on the form
  Given a question key has multiple revisions
  And only one of them is both active and not deleted
  When the current form is assembled
  Then that revision is the one included for the key
  And an older active revision never reappears after a later revision deactivates or deletes the question

@REQ-QB-010
Scenario: Form questions are ordered deterministically
  Given the current form includes several question revisions
  When they are ordered for display
  Then they are ordered by sort order
  And ties are broken by question key

@REQ-QB-011
Scenario: The current form is public
  Given no bearer token is presented
  When a request asks for the current form
  Then the request is answered, not refused

@REQ-QB-012
Scenario: A group question's entry nests its children and does not repeat them
  Given a live group question exists as a section heading
  And another live question is grouped under it
  When the current form is assembled
  Then the group's entry carries that question as a child, in order
  And the child does not also appear as its own top-level entry

@REQ-QB-013
Scenario: The current form's response includes a question's conditional dependency
  Given a question is conditional on a yes-or-no question
  When the current form is assembled
  Then the conditional question's entry names the question it depends on

@REQ-QB-014
Scenario: Publication consent can never be optional
  Given the form is assembled for a reporter
  When the reporter submits without answering the publication consent question
  Then the submission is refused as invalid
  And an Administrator cannot save an optional revision of the publication consent question

@REQ-QB-015
Scenario: An Administrator chooses whether an ordinary question must be answered
  Given an Administrator authors an ordinary question
  When they mark it as one reporters must answer
  Then the new revision records that it is required
  And marking it optional again records that on a further new revision

@REQ-QB-108
Scenario Outline: Only free text can be marked as needing translation
  Given an Administrator authors a <type> question without saying whether it needs translation
  Then the new revision <records>

Examples:
  | type       | records                                          |
  | paragraph  | records that its answers need translation        |
  | short text | records that its answers do not need translation |
  | email      | records that its answers do not need translation |
  | date       | records that its answers do not need translation |

@REQ-QB-109
Scenario: Marking a non-text question as needing translation is refused
  Given an Administrator authors an email, date, yes/no, or select question
  When they mark it as needing translation
  Then saving that question is refused

@REQ-QB-110
Scenario: Whether a question needs translation is a revision setting
  Given a short-text question that does not need translation
  When an Administrator marks it as needing translation while nobody has answered it
  Then a new revision records that it needs translation
  When an Administrator changes it back after it has been answered
  Then the question is retired and replaced, so each answer keeps the setting it was given under

@REQ-QB-154
Scenario Outline: A date question allows future dates only when an Administrator says so
  Given an Administrator creates a date question <saying>
  Then the saved question allows future dates: <stored>

Examples:
  | saying                                        | stored |
  | without saying whether it allows future dates | false  |
  | allowing future dates                         | true   |

@REQ-QB-155
Scenario: Only a date question can allow future dates
  Given an Administrator creates a short-text, time, or number question
  When they mark it as allowing future dates
  Then saving each one is refused as invalid

@REQ-QB-156
Scenario: Whether a date question allows future dates is a revision setting
  Given a date question that does not allow future dates
  When an Administrator allows future dates while nobody has answered it
  Then a new revision of the same question allows future dates, and the earlier revision still does not
  When a reporter answers it and an Administrator then disallows future dates
  Then the question is retired and replaced under the same key, and the answer keeps the revision that allowed future dates

@REQ-QB-157
Scenario: The migration leaves the occurrence date refusing future dates, with no new revision
  Given the migrations have been applied
  Then the seeded occurrence-date question "Tell us the date of the occurrence." does not allow future dates
  And it is still the revision it was seeded as

@REQ-QB-016
Scenario: Publication consent must resolve to an explicit yes or no
  Given the publication consent revision has no preselected value
  When the submitted value is absent, null, of the wrong type, or does not resolve to an explicit yes or no
  Then the submission is refused as invalid

@REQ-QB-025
Scenario: Only consent is projected onto the report aggregate
  Given a submitted report has answers to several ordinary questions
  When those answers are persisted
  Then only the publication consent and media consent answers are projected onto the report aggregate, with document consent derived from media consent
  And every other answer, including dates, times, provinces, injury severities, and aircraft details, remains a stored string read through its question key

@REQ-QB-026
Scenario: Privacy is a property of the revision, not the answer
  Given an answer is created against a private question revision
  When the answer is persisted
  Then it stores the exact revision identifier and a privacy snapshot
  And the answer is available only to authorized admin flows and to the Worker as labeled recognition context
  And it never becomes public content

@REQ-QB-027
Scenario: Creating a revision preserves the question bank invariants
  Given an Administrator saves a new revision
  Then the question key is a non-empty, unique, non-localized identifier
  And both English and French labels are present for an answer-producing question
  And a single-select or multi-select question has at least one live choice, a type-ahead may start with none, and every other type has none
  And only the publication-consent and media-consent questions may be marked system
  And both consent questions stay active, yes/no, and private, and no edit can make either one otherwise

@REQ-QB-030
Scenario: A revision can be deleted only when no answer references it
  Given a question revision has never been referenced by any answer, including answers on deleted reports
  When an Administrator deletes it
  Then the deletion succeeds

@REQ-QB-031
Scenario: A referenced revision can never be deleted
  Given a question revision is referenced by at least one answer, including an answer on a deleted report
  When an Administrator attempts to delete it
  Then the deletion is refused
  And the revision remains available as history indefinitely
  And deactivating it through a new revision is the normal way to remove it from future forms

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
Scenario Outline: An answer naming a statement or a group is refused
  Given an Administrator authors a <type> question
  When a submission carries an answer naming that question's revision
  Then the report is refused as invalid
  And nothing is stored

Examples:
  | type      |
  | statement |
  | group     |

@REQ-QB-141
@ui
Scenario: An Administrator writes instructional text as a title and a description
  Given an Administrator is authoring a new question
  When they choose instructional text
  Then its wording is asked for as a title and a description in each language
  And each description takes several lines
  When they choose short text
  Then its wording is asked for as a question and help text in each language

@REQ-QB-142
Scenario: Instructional text keeps the line breaks its description was written with
  Given an Administrator saves instructional text whose description spans several lines
  When the current form is assembled
  Then the description is served with its line breaks unchanged

@REQ-QB-143
@ui
Scenario Outline: A reporter reads instructional text with its description's paragraphs
  Given instructional text whose description has two paragraphs is <placement>
  When a reporter reaches it on the form
  Then its title and both paragraphs of its description are shown, one after the other

Examples:
  | placement               |
  | the form's introduction |
  | a page of its own       |
  | grouped under a group   |

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
  Then the attempt is refused

@REQ-QB-049
Scenario: A group cannot itself be grouped under another group
  Given two group questions exist
  When an Administrator tries to group one under the other
  Then the attempt is refused

@REQ-QB-050
Scenario: A question cannot be grouped under itself
  Given a group question exists
  When an Administrator tries to group it under itself
  Then the attempt is refused

@REQ-QB-051
Scenario: Grouping is unaffected by conditional dependency and vice versa
  Given a question is both conditional on a yes/no question and grouped under a group question
  When an Administrator reads its saved revision
  Then both facts are recorded independently
  And clearing one leaves the other unchanged

@REQ-QB-052
Scenario Outline: A grouped question is ungrouped when its group stops being one
  Given a group question has two questions grouped under it, the first with <answers>
  And a later question follows the group on the form
  When an Administrator <change>
  Then the first grouped question <result>
  And the second grouped question gets a new revision that is ungrouped
  And the reporter's form lists both as entries of their own, in their former order, at the group's place
  And the later question comes after them

Examples:
  | change                                       | answers    | result                                                                   |
  | deletes the group                            | no answers | gets a new revision that is ungrouped                                    |
  | deletes the group                            | an answer  | is retired and replaced by a new question with its key that is ungrouped |
  | retypes the group to a type other than group | no answers | gets a new revision that is ungrouped                                    |
  | retypes the group to a type other than group | an answer  | is retired and replaced by a new question with its key that is ungrouped |

@REQ-QB-248
Scenario Outline: Editing a group gives each of its questions a new revision that stays grouped under it
  Given a group question has two questions grouped under it, the first with <answers>
  When an Administrator edits the group's wording
  Then the first grouped question <result>
  And the second grouped question gets a new revision that is still grouped under the group
  And the reporter's form lists both as children of the group, in their former order

Examples:
  | answers    | result                                                                                |
  | no answers | gets a new revision that is still grouped under the group                             |
  | an answer  | is retired and replaced by a new question with its key that is grouped under the group |

@REQ-QB-053
Scenario: A question can be made conditional only on a yes/no or single-select question
  Given an active question asks for something other than yes/no or single-select
  When an Administrator tries to make another question conditional on it
  Then the attempt is refused
  And a yes/no question is accepted as the condition instead
  And a single-select question naming one of its live choices is accepted as the condition instead

@REQ-QB-054
Scenario: A single-select parent's dependency records the required choice
  Given a single-select question asking whether the pilot flies hang gliders or paragliders
  When an Administrator makes a rating question depend on the "hang glider" choice
  And an Administrator makes a different rating question depend on the "paraglider" choice
  Then each rating question's saved dependency names its own required choice

@REQ-QB-055
Scenario: A single-select dependency must name one of the parent's live choices
  Given a single-select question offering hang glider and paraglider
  When an Administrator tries to make another question depend on a choice the parent does not offer
  Then the attempt is refused

@REQ-QB-056
Scenario: A yes/no dependency does not name a choice
  Given a yes/no question
  When an Administrator makes another question depend on it
  Then the dependency needs no required choice: its condition is always "answered yes"

@REQ-QB-057
Scenario: A question cannot be conditional on itself or form a cycle
  Given a question is already conditional on a yes/no question
  When an Administrator tries to make that yes/no question conditional on it
  Then the attempt is refused
  And a question offered as its own condition is refused the same way

@REQ-QB-058
Scenario: Publication consent can never be made conditional
  Given the publication consent question exists
  When an Administrator tries to make it conditional on another question
  Then the attempt is refused

@REQ-QB-059
Scenario: Rearranging the form writes a new revision for every question that moved
  Given several active questions sit in a known order
  When an Administrator rearranges them
  Then each question that moved has a new revision recording its new position
  And a question that did not move keeps its current revision
  And no two questions are left claiming the same position

@REQ-QB-060
Scenario Outline: A question type either takes choices or does not
  Given an Administrator authors a <type> question
  When they supply bilingual choices with it
  Then the question <outcome>

Examples:
  | type          | outcome              |
  | type-ahead    | stores those choices |
  | single-select | stores those choices |
  | multi-select  | stores those choices |
  | time          | is refused           |
  | short text    | is refused           |
  | yes/no        | is refused           |

@REQ-QB-061
Scenario: A question key is normalized and cannot be reused
  Given an Administrator authors a question with a loosely typed key
  Then the stored key is lowercase and underscore-separated
  And a key that reduces to nothing at all is refused

@REQ-QB-062
Scenario: Deleting a question keeps it and its history
  Given an active question nobody has answered
  When an Administrator deletes it
  Then the question is marked deleted, not erased
  And it refuses any further revision

@REQ-QB-063
Scenario: Publication consent can never be deleted or deactivated
  Given the publication consent question exists
  When an Administrator tries to delete it
  Then the attempt is refused
  And trying to stop asking it is refused the same way
  And an ordinary edit that clears its active flag is refused the same way

@REQ-QB-074
@ui
Scenario: An Administrator sees which choices reporters added
  Given an Administrator opens the manage-questions page
  Then a type-ahead question with reporter-added values says how many are waiting to be reviewed
  When they open that question
  Then each reporter-added value is marked as such

@REQ-QB-075
@ui
Scenario: An Administrator corrects a reporter-added value
  Given an Administrator opens a type-ahead question with a reporter-added value
  When they correct the wording of that choice and save it
  Then the corrected wording is shown on the question

@REQ-QB-076
@ui
Scenario: An Administrator authors a question from the Manage questions page
  Given an Administrator opens the manage-questions page
  When they add a paragraph-text question in both official languages
  Then the new question appears in the list with its type and version

@REQ-QB-077
@ui
Scenario: The choices editor appears only for a type that takes choices
  Given an Administrator is authoring a new question
  When they choose the type-ahead list type
  Then the page offers a choice editor
  When they choose the single-line text type instead
  Then the page offers neither

@REQ-QB-078
@ui
Scenario: Only yes/no and single-select questions are offered as a condition
  Given an Administrator is authoring a new question
  Then the condition picker offers only the yes/no and single-select questions on the form

@REQ-QB-079
@ui
Scenario: Naming a required choice appears only for a single-select condition
  Given an Administrator is authoring a new question
  When they choose a yes/no question as the condition
  Then no required-choice control is offered
  When they choose a single-select question as the condition instead
  Then a required-choice control offers that question's live choices

@REQ-QB-080
@ui
Scenario: Questions are reordered from the keyboard
  Given an Administrator opens the manage-questions page
  When they move the second question up using its move-up control
  Then the two questions have swapped places in the list

@REQ-QB-081
@ui
Scenario: Editing an unanswered question from the Manage questions page shows its new version
  Given an Administrator opens the manage-questions page
  And the first question has never been answered
  When they edit its English wording and save
  Then the list shows the new wording and a higher version number

@REQ-QB-082
@ui
Scenario: Editing an answered question warns that it will be replaced
  Given an Administrator opens the manage-questions page
  And the first question has been answered
  When they edit its English wording
  Then the page says that saving retires this question and creates a new one
  When they save
  Then the list shows one question for that key, with the new wording

@REQ-QB-111
@ui
Scenario: The editor offers Auto-translate answer only for free text
  Given an Administrator is authoring a new question
  When they choose paragraph
  Then Auto-translate answer is offered and checked
  When they choose short text
  Then Auto-translate answer is offered and unchecked
  When they choose email
  Then Auto-translate answer is not offered

@REQ-QB-158
@ui
Scenario: The editor offers Allow future dates only for a date question, unchecked
  Given an Administrator is authoring a new question
  When they choose date
  Then Allow future dates is offered and unchecked
  When they choose time
  Then Allow future dates is not offered
  When they choose date, check Allow future dates, write the question in both languages, and save
  Then the saved question is sent allowing future dates

@REQ-QB-085
@ui
Scenario: Deleting a question removes it from the list
  Given an Administrator opens the manage-questions page
  When they delete the second question
  Then it is gone from the list

@REQ-QB-086
@ui
Scenario: A refused save tells the Administrator why
  Given an Administrator is authoring a new question
  When they save a question whose two choices read alike
  Then the page shows the reason the save was refused
  And the question is not added to the list

@REQ-QB-087
@ui
Scenario: The editor carries an existing question's settings into the form
  Given an Administrator opens the manage-questions page
  When they open the first question for editing
  Then the form is filled with its current wording, type, and behaviour
  And no question key is shown

@REQ-QB-088
@ui
Scenario: Reviewing an imported Typeform draft prefills the editor
  Given an Administrator opens the manage-questions page
  When they import a Typeform English and French export pair
  Then the imported drafts are listed
  When they choose to review the first imported draft
  Then the editor is filled with that draft's type and both languages

@REQ-QB-089
@ui
Scenario: An Administrator downloads the question bank as Typeform files
  Given an Administrator opens the manage-questions page
  When they choose to export the question bank
  Then a zip file download begins

@REQ-QB-090
@ui
Scenario: An Administrator writes a question's choice by its wording alone
  Given an Administrator is authoring a new question
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

@REQ-QB-096
Scenario: A new question's key is derived from its English wording and never reused
  Given an Administrator saves a new question without a key
  Then its key is derived from its English wording
  When they save another question with the same English wording
  Then it receives a different key
  When they delete the first question and save a third with the same wording
  Then the third question does not take the deleted question's key

@REQ-QB-093
@ui
Scenario: Editing a question opens the editor in that question's place
  Given an Administrator opens the manage-questions page
  When they open the second question for editing
  Then the editor takes the second question's place in the list
  And the editor's top edge lines up with that row's move-up control
  And every other question is still shown in its place
  When they cancel the edit
  Then the second question is shown in its place again

@REQ-QB-098
Scenario: Editing an answered question's wording carries every choice to the replacement
  Given a type-ahead question has been answered on at least one report
  And it offers choices an Administrator wrote and a reporter-added value
  And an Administrator removed one of its choices
  When an Administrator changes its wording
  Then the replacement question offers every choice the retired one offered
  And the reporter-added value is still marked as reporter-added
  And the removed choice is carried over and stays removed

@REQ-QB-101
Scenario: A choice a live question depends on cannot be removed
  Given a question depends on the "paraglider" choice of a single-select question
  When an Administrator saves the single-select question without that choice
  Then the save is refused naming the dependent question
  And the choice is still offered

@REQ-QB-103
@ui
Scenario: The report form shows a one-language choice in the language it has
  Given a type-ahead question has a reporter-added value typed only in English
  When a reporter using French opens that question
  Then the type-ahead offers the choice in its English wording

@REQ-QB-240
@ui
Scenario Outline: The form adds the colon after an answerable question's label, in the locale's style
  Given the form asks a <kind> question labelled "<label>"
  When a reporter opens the form in <locale>
  Then the question is labelled "<shown>"

Examples:
  | kind          | label        | locale  | shown         |
  | short text    | Date         | English | Date:         |
  | short text    | Date         | French  | Date :        |
  | single-select | Province     | English | Province:     |
  | multi-select  | Injuries     | French  | Injuries :    |
  | yes/no        | Injured?     | English | Injured?      |
  | yes/no        | Blessé ?     | French  | Blessé ?      |
  | statement     | Tell us more | English | Tell us more  |
  | group         | From         | French  | From          |

@REQ-QB-241
@ui
Scenario Outline: The admin report detail adds the colon after an answerable question's label, in the locale's style
  Given a Safety Officer and a report with a short-text answer labelled "Date", a yes/no answer labelled "Injured?" and a paragraph answer labelled "Description"
  When they open that report in <locale>
  Then the answers are labelled <labels>

Examples:
  | locale  | labels                          |
  | English | "Date:", "Injured?", "Description:" |
  | French  | "Date :", "Injured?", "Description :" |

@REQ-QB-242
@ui
Scenario: The question bank list shows each language's label with its own colon style
  Given an Administrator and a question labelled "Date" in English and "Date" in French
  And a statement labelled "Tell us more" in both languages
  When they open the manage-questions page
  Then the question's English label reads "Date:" and its French label reads "Date :"
  And the statement's labels have no colon

@REQ-QB-243
@ui
Scenario: The question editor refuses a label that ends in a colon
  Given an Administrator is authoring a new question
  When they write "Date:" as the English wording and "Date" as the French wording
  Then a message says the form adds the colon itself
  And Save stays disabled
  When they remove the colon
  Then the message goes away and Save is enabled

@REQ-QB-244
Scenario Outline: A question whose label ends in a colon is refused, in either language
  Given an Administrator
  When they <action> a question whose <language> label is "<label>"
  Then it is refused as invalid, with a problem that says, in English and French, that the form adds the colon itself
  And no question or revision is stored

Examples:
  | action | language | label    |
  | create | English  | Date:    |
  | create | French   | Date :   |
  | revise | English  | Date:    |
  | revise | French   | Date :   |

@REQ-QB-245
Scenario: A migration removes a trailing colon from every stored question label, in place
  Given a database holding questions whose labels end in ":" or " :" in English and in French
  And a report answered under one of them
  When the migration that trims label colons runs
  Then every label has no trailing colon, and a label that had none is unchanged
  And no question or revision was created or deleted
  And the answer still names the same revision

@REQ-QB-246
Scenario: The seeded question bank has no label ending in a colon
  Given a clean database
  When the migrations have run
  Then no question revision's English or French label ends in a colon
