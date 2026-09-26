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
Scenario: Editing an unanswered question creates a new revision instead of mutating one
  Given an active question revision exists for a stable key
  And no answer references that question
  When an Administrator changes its wording, help text, translations, type, order, privacy, active state, or required state
  Then a new complete revision is created with the next revision number
  And the previous revision is left unchanged
  And the question keeps its identifier

@REQ-QB-002
Scenario: Editing an answered question retires it and creates a new one
  Given a question has been answered on at least one report
  When an Administrator changes its wording
  Then the original question is stamped as deleted
  And a new question is created with a new identifier
  And the new question carries the same stable key
  And the new question starts its own revision numbering
  And the answers already given still refer to the retired question and its original wording

@REQ-QB-003
Scenario: An answer on a deleted report still forces a fork
  Given the only answer to a question is on a report that has been deleted
  When an Administrator changes that question's wording
  Then the original question is stamped as deleted
  And a new question is created with a new identifier

@REQ-QB-004
Scenario: A retired question can never be brought back
  Given a question has been stamped as deleted
  When anything attempts to restore, revive, or revise it
  Then the attempt is rejected
  And an Administrator who wants it back authors it again as a new question

@REQ-QB-005
Scenario: Only one question per key is live at a time
  Given a stable key has a retired question and a live one
  When anything resolves that key
  Then it resolves to the live question
  And a second live question for the same key is rejected

@REQ-QB-006
Scenario: Publication consent revises in place even when answered
  Given the consent_publish question has been answered on at least one report
  When an Administrator changes its wording
  Then a new revision is created for it
  And the question keeps its identifier
  And it is never stamped as deleted

@REQ-QB-008
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
Scenario: consent_publish can never be optional
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

@REQ-QB-108
Scenario Outline: Only free text can be marked as needing translation
  Given an Administrator authors a <type> question without saying whether it needs translation
  Then the new revision <records>

Examples:
  | type       | records                                        |
  | long_text  | records that its answers need translation      |
  | short_text | records that its answers do not need translation |
  | email      | records that its answers do not need translation |
  | date       | records that its answers do not need translation |

@REQ-QB-109
Scenario: Marking a non-text question as needing translation is rejected
  Given an Administrator authors an email, date, yes/no, or select question
  When they mark it as needing translation
  Then saving that question is rejected

@REQ-QB-110
Scenario: Whether a question needs translation is a revision field
  Given a short-text question that does not need translation
  When an Administrator marks it as needing translation while nobody has answered it
  Then a new revision records that it needs translation
  When an Administrator changes it back after it has been answered
  Then the question is retired and replaced, so each answer keeps the setting it was given under

@REQ-QB-154
Scenario Outline: A date question allows future dates only when an Administrator says so
  Given an Administrator creates a date question through the API <saying>
  Then the saved question reads allowFutureDates as the JSON boolean <stored>

Examples:
  | saying                                        | stored |
  | without saying whether it allows future dates | false  |
  | allowing future dates                         | true   |

@REQ-QB-155
Scenario: Only a date question can allow future dates
  Given an Administrator creates a short-text, time, or number question through the API
  When they mark it as allowing future dates
  Then the API refuses to save each one

@REQ-QB-156
Scenario: Whether a date question allows future dates is a revision field
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
Scenario: consent_publish must resolve to an explicit yes or no
  Given the consent_publish revision has no preselected value
  When the submitted value is absent, null, of the wrong type, or does not resolve to an explicit yes or no
  Then the API rejects the submission

@REQ-QB-019
Scenario Outline: Every answer is stored in its written form
  Given a reporter writing in <language> submits <submitted> as the answer to a <type> question
  When the answer is persisted
  Then the stored value is <stored>

Examples:
  | language | type       | submitted       | stored                                  |
  | English  | yes_no     | JSON true       | the boolean true                        |
  | English  | yes_no     | JSON false      | the boolean false                       |
  | French   | yes_no     | JSON true       | the boolean true                        |
  | French   | yes_no     | JSON false      | the boolean false                       |
  | English  | checkbox   | JSON true       | the boolean true                        |
  | French   | checkbox   | JSON false      | the boolean false                       |
  | English  | yes_no     | JSON null       | nothing, because the answer was skipped |
  | English  | date       | 2026-09-21      | 2026-09-21                              |
  | French   | date       | 2026-09-21      | 2026-09-21                              |
  | English  | time       | 14:30           | 14:30                                   |
  | English  | date       | an empty string | nothing, because the answer was skipped |
  | English  | short_text | a line of prose | that line, as typed                     |

@REQ-QB-118
Scenario Outline: An answer not in its written form is rejected
  Given a reporter writing in <language> submits <submitted> as the answer to a <type> question
  When the submission is made
  Then the submission is rejected
  And no stored answer carries that value

Examples:
  | language | type     | submitted                  |
  | English  | date     | the 21st of September 2026 |
  | English  | date     | 21/09/2026                 |
  | English  | date     | 2026-9-21                  |
  | English  | date     | 2026-02-30                 |
  | English  | time     | 2:30 PM                    |
  | English  | time     | 25:00                      |
  | English  | time     | 14:30:00                   |
  | English  | checkbox | checked                    |
  | English  | checkbox | yes                        |
  | French   | checkbox | oui                        |
  | English  | yes_no   | yes                        |
  | French   | yes_no   | non                        |
  | English  | yes_no   | true                       |
  | English  | date     | JSON true                  |
  | English  | number   | JSON false                 |

@REQ-QB-119
Scenario Outline: A yes or no answer has no second language
  Given a reporter writing in <language> submits <submitted> as the answer to a <type> question
  When the answer is persisted
  Then it holds no words and no second language in either column, and its translation mode is none
  And no translation provider was called and nothing waits for the Worker to translate it

Examples:
  | language | type     | submitted  |
  | English  | yes_no   | JSON true  |
  | French   | yes_no   | JSON false |
  | French   | checkbox | JSON true  |

@REQ-QB-120
Scenario Outline: Only true enables a conditional question, in either language
  Given a question depends on a yes/no question
  When a reporter writing in <language> answers the yes/no question <answer>
  Then the conditional question is <asked>

Examples:
  | language | answer | asked     |
  | English  | true   | asked     |
  | English  | false  | not asked |
  | French   | true   | asked     |
  | French   | false  | not asked |

@REQ-QB-121
Scenario Outline: Only true is consent, in either language
  Given a reporter writing in <language> answers <consent> <answer>
  When the answer is projected onto the report
  Then the report records <consent> as <recorded>

Examples:
  | language | consent         | answer | recorded |
  | English  | consent_publish | true   | given    |
  | French   | consent_publish | true   | given    |
  | English  | consent_publish | false  | refused  |
  | French   | consent_publish | false  | refused  |
  | French   | consent_media   | true   | given    |
  | English  | consent_media   | false  | refused  |

@REQ-QB-137
Scenario Outline: A yes or no stored as a word is converted to a boolean once
  Given a <type> answer was stored as the word "<word>" before yes/no answers were booleans
  When the database is migrated
  Then that answer's boolean is <boolean>
  And the converted answer carries no words and no second language, and its translation mode is none
  And its locale is unchanged

Examples:
  | type     | word | boolean |
  | yes_no   | yes  | true    |
  | yes_no   | oui  | true    |
  | yes_no   | no   | false   |
  | yes_no   | non  | false   |
  | checkbox | yes  | true    |
  | checkbox | non  | false   |

@REQ-QB-138
Scenario: A yes or no stored as anything but the four words stops the conversion
  Given a yes_no answer was stored as the word "maybe" before yes/no answers were booleans
  When the database is migrated
  Then the migration fails and names no answer's value
  And no answer was converted

@REQ-QB-025
Scenario: Only consent is projected onto the report aggregate
  Given a submitted report has answers to several ordinary questions
  When those answers are persisted
  Then only the consent_publish and consent_media answers are projected onto the report aggregate, with consent_documents derived from consent_media
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
Scenario: Creating a revision preserves the question bank invariants
  Given an Administrator saves a new revision
  Then the stable key is a non-empty, unique, non-localized identifier
  And both English and French labels are present for an answer-producing question
  And a single-select or multi-select question has at least one live choice, a type-ahead may start with none, and every other type has none
  And only the publication-consent and media-consent questions may be marked system
  And both consent questions stay active, yes/no, and private, and no edit can make either one otherwise

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

@REQ-QB-036
Scenario: Two reporters naming the same new site produce one choice
  Given a reporter has already added a site to a type-ahead question
  When another reporter submits the same site name
  Then the existing choice is reused rather than duplicated
  And an administrator's wording is never replaced by a reporter's

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

@REQ-QB-141
@ui
Scenario: An Administrator writes instructional text as a title and a description
  Given a signed-in Administrator is authoring a new question
  When they choose instructional text
  Then its wording is asked for as a title and a description in each language
  And each description takes several lines
  When they choose short text
  Then its wording is asked for as a question and help text in each language

@REQ-QB-142
Scenario: Instructional text keeps the line breaks its description was written with
  Given an Administrator saves instructional text whose description spans several lines
  When the API assembles the current form
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
Scenario: A translation draft comes from the API and is saved only by a person
  Given an Administrator is authoring a question in one official language
  When they ask for the other language to be translated
  Then the request goes to the application's own API rather than to a provider from the browser
  And the translated text is returned as a draft that is not saved anywhere
  And the same action is available for the second language of an answer awaiting translation
  And the reviewer-gated translate endpoint is the only API code that calls a translator
  And no domain code a reporter's submission runs calls a translator

@REQ-QB-067
Scenario: A server with no translation credential still authors questions
  Given no translation provider is configured, in development or anywhere else
  When the authoring screen asks whether translation is available
  Then it is told that translation is unavailable
  And the answer carries no credential and no provider detail
  And no environment substitutes a stand-in that returns the text unchanged

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

@REQ-QB-154
@ui
Scenario Outline: An Administrator drafts the French of choices written only in English
  Given a signed-in Administrator is authoring a new <type> question worded in both languages
  When they add two choices written only in English and press Translate choices
  Then each of those choices' French field is filled with its translation
  And the question's own Translate action stays unavailable
  And the filled French fields remain editable
  And nothing is saved until they press Save

  Examples:
    | type          |
    | single-select |
    | multi-select  |
    | type-ahead    |

@REQ-QB-155
@ui
Scenario: English-only and French-only choices are each translated into the other language in one press
  Given a signed-in Administrator is authoring a new type-ahead question worded in both languages
  When they add a choice written only in English and a choice written only in French and press Translate choices
  Then the English-only choice's French field is filled with its translation
  And the French-only choice's English field is filled with its translation

@REQ-QB-156
@ui
Scenario: Translating choices never overwrites a written choice
  Given a signed-in Administrator is authoring a new type-ahead question worded in both languages
  When they add a choice written in both languages, an empty choice, and a choice written only in English
  And they press Translate choices
  Then only the English-only choice is sent to be translated
  And the choice written in both languages keeps its wording
  And the empty choice stays empty

@REQ-QB-157
@ui
Scenario: Translating choices is offered only while a choice is missing a language
  Given a signed-in Administrator is editing a single-select question whose choices are all in both languages
  Then the Translate choices action is unavailable
  When they add a choice written only in French
  Then the Translate choices action becomes available

@REQ-QB-158
@ui
Scenario: Translating choices is not offered when the server has no provider
  Given a signed-in Administrator is authoring a question on a server with no translation provider
  When they choose the type-ahead list type
  And they add a choice written only in English
  Then the Translate choices action is unavailable and says so

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

@REQ-QB-111
@ui
Scenario: The editor offers Auto-translate answer only for free text
  Given a signed-in Administrator is authoring a new question
  When they choose long text
  Then Auto-translate answer is offered and checked
  When they choose short text
  Then Auto-translate answer is offered and unchecked
  When they choose email
  Then Auto-translate answer is not offered

@REQ-QB-158
@ui
Scenario: The editor offers Allow future dates only for a date question, unchecked
  Given a signed-in Administrator is authoring a new question
  When they choose date
  Then Allow future dates is offered and unchecked
  When they choose time
  Then Allow future dates is not offered
  When they choose date, check Allow future dates, write the question in both languages, and save
  Then the saved question is sent with allowFutureDates true

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

@REQ-QB-101
Scenario: A choice a live question depends on cannot be removed
  Given a question depends on the "paraglider" choice of a single-select question
  When an Administrator saves the single-select question without that choice
  Then the save is refused naming the dependent question
  And the choice is still offered

@REQ-QB-103
@ui
Scenario: The report form shows a one-language choice in the language it has
  Given a type-ahead question has a reporter-added choice typed only in English
  When a reporter using French opens that question
  Then the type-ahead offers the choice in its English wording

@REQ-QB-122
Scenario Outline: An answer names the choice it was given under
  Given a reporter answering in English is shown a <type> question whose choices are written in both official languages
  When the reporter chooses one of its choices and submits
  Then the stored answer references that choice by its identifier
  And it stores no copy of the choice's wording
  And the answer reads as the choice's English label, with its French label as the second language

Examples:
  | type          |
  | single_select |
  | multi_select  |
  | autocomplete  |

@REQ-QB-123
Scenario: Fixing a picker option in place corrects every answer that named it
  Given a single-select question has been answered with its option "Cooprs"
  When an Administrator fixes that option's wording in place to "Coopers"
  Then the option keeps its identifier
  And the earlier answer now reads "Coopers"
  And the question keeps its identifier and its current revision

@REQ-QB-124
Scenario: Replacing a picker option keeps the old option under every earlier answer
  Given a single-select question offering "foo", "bar", and "baz" has been answered with "baz"
  When an Administrator replaces "baz" with "fizz"
  Then the form offers "foo", "bar", and "fizz"
  And "baz" is retired, not erased, and records that "fizz" replaced it
  And "fizz" has an identifier of its own
  And the earlier answer still names "baz" and reads "baz"
  And the question keeps its identifier and its current revision

@REQ-QB-125
Scenario: A condition follows its choice's replacement
  Given a question depends on the "paraglider" choice of a single-select question
  When an Administrator replaces "paraglider" with "paraglider (solo)"
  Then the dependent question is enabled by an answer naming "paraglider (solo)"
  And the dependent question keeps its current revision

@REQ-QB-140
Scenario: A condition follows its parent question when the parent forks
  Given a question depends on the "Paraglider" choice of an answered single-select question
  When an Administrator changes the single-select question's wording
  Then the report form and the editor show the condition on the replacement question and its "Paraglider" choice
  And the dependent question is not revised by its parent's fork
  And saving the dependent question unchanged gives it no new revision

@REQ-QB-139
@ui
Scenario: An Administrator chooses to replace a picker option rather than fix it
  Given a signed-in Administrator opens the manage-questions page
  When they reword the "Paraglider" option of a single-select question and mark it to be replaced
  Then the save sends that option to be replaced, under its old code with its new wording
  And a type-ahead question's values offer no replace choice

@REQ-QB-126
Scenario Outline: A removed choice is no longer offered but still names every answer given under it
  Given a <type> question has been answered with one of its choices
  When that choice is removed
  Then the form stops offering it
  And the choice is retired rather than erased
  And the earlier answer still names it and reads its wording
  And the question keeps its identifier and its current revision

Examples:
  | type          |
  | single_select |
  | multi_select  |
  | autocomplete  |

@REQ-QB-127
Scenario: A fork's choices are new rows, and old answers keep naming the retired question's
  Given a single-select question has been answered with one of its choices
  When an Administrator changes the question's wording
  Then the replacement question offers a copy of every choice, each with its own identifier
  And the earlier answer still names the retired question's choice

@REQ-QB-128
Scenario Outline: A reporter's new type-ahead value is flagged for review and offered at once
  Given a type-ahead question offers several choices
  When a reporter answering in <language> submits "<value>", which the question does not offer
  Then the question gains a reporter-added value whose <language> wording is "<value>"
  And the value is flagged for review
  And the reporter's answer names that value
  And the next reporter is offered it, in <language> until its other language is supplied

Examples:
  | language | value                  |
  | English  | Mount 7                |
  | French   | Élévation Sainte-Anne  |

@REQ-QB-129
Scenario: A type-ahead value is corrected in place for every answer that names it
  Given two reports answered a type-ahead question with the value "coopers"
  When a Safety Officer corrects that value's wording to "Cooper's"
  Then the value keeps its identifier
  And both answers now read "Cooper's"
  And the next reporter is offered "Cooper's"

@REQ-QB-130
Scenario: A reporter typing a removed type-ahead value names it without reviving it
  Given a Safety Officer removed the type-ahead value "Test site"
  When a reporter submits "test site" for that question
  Then the reporter's answer names the removed value
  And the value stays removed and is not offered
  And the value is flagged for review again

@REQ-QB-131
Scenario: Merging one type-ahead value into another leaves every answer untouched
  Given reports answered a type-ahead question with "Coopers" and with "Cooper's", two separate values
  When a Safety Officer merges "Coopers" into "Cooper's"
  Then "Coopers" is removed and records that it was merged into "Cooper's"
  And the answers that named "Coopers" still name it, and read "Cooper's"
  And the form offers "Cooper's" only
  When a reporter later submits "coopers" for that question
  Then the new answer names "Cooper's"

@REQ-QB-132
Scenario: Merges resolve in a chain and never form a cycle
  Given the type-ahead value "A" was merged into "B"
  When a Safety Officer merges "B" into "C"
  Then an answer naming "A" reads "C"
  And merging "C" into "A" is refused

@REQ-QB-133
Scenario Outline: Only a type-ahead value can be merged or edited by a Safety Officer
  Given a <type> question has two choices
  When a Safety Officer tries to <action> one of them
  Then the attempt is <outcome>

Examples:
  | type          | action                     | outcome  |
  | autocomplete  | merge it into the other    | accepted |
  | autocomplete  | correct its wording        | accepted |
  | single_select | merge it into the other    | refused  |
  | single_select | correct its wording        | refused  |
  | multi_select  | merge it into the other    | refused  |

@REQ-QB-134
Scenario: The Worker supplies a reporter-added value's other language
  Given a reporter answering in French added the type-ahead value "Élévation Sainte-Anne"
  When the Worker processes its translation work
  Then the value gains an English wording from the translation provider, marked as machine-translated
  And the value is still flagged for review
  And no translation provider was called while the report was submitted

@REQ-QB-135
Scenario: Reviewing a type-ahead value clears its flag
  Given a type-ahead question has a reporter-added value flagged for review
  When a Safety Officer approves it
  Then the value is no longer flagged for review
  And the review records the Safety Officer's token subject and the time

@REQ-QB-136
Scenario: Existing answers are linked to their choices without being rewritten
  Given reports stored before this change answered a single-select question with one of its current labels and with a label it no longer offers
  When the choice-reference migration runs
  Then the first answer names the choice with that label
  And the second answer names a removed choice carrying the label it stored, in its language
  And neither answer's stored text changes

@REQ-QB-144
Scenario: The API sends each choice's pin, pinned-first choices first and pinned-last choices last
  Given an Administrator saves a single-select question with "Other" pinned last, "United States" and "Canada" pinned first, and "Mexico" and "Brazil" not pinned
  When the report form's questions and the editor's questions are read
  Then each lists that question's pinned-first choices, then its unpinned choices, then its pinned-last choices
  And each choice carries its pin as "first", "none", or "last"

@REQ-QB-145
@ui
Scenario Outline: A question's choices are listed alphabetically in the reader's language
  Given a <type> question offers "Hawk" / "Faucon", "Emu" / "Émeu", "Kestrel" / "Crécerelle", and "Eagle" / "Aigle", none pinned
  When a reporter using <language> opens that question
  Then its choices are listed <order>

Examples:
  | type          | language | order                                        |
  | single_select | English  | "Eagle", "Emu", "Hawk", "Kestrel"            |
  | single_select | French   | "Aigle", "Crécerelle", "Émeu", "Faucon"      |
  | multi_select  | French   | "Aigle", "Crécerelle", "Émeu", "Faucon"      |
  | autocomplete  | French   | "Aigle", "Crécerelle", "Émeu", "Faucon"      |

@REQ-QB-146
@ui
Scenario Outline: Pinned choices come first or last, each group alphabetical
  Given a <type> question offers "United States" and "Canada" pinned first, "Other" pinned last, and "Mexico", "Brazil", and "France" not pinned
  When a reporter using English opens that question
  Then its choices are listed "Canada", "United States", "Brazil", "France", "Mexico", "Other"
  And <separators>

Examples:
  | type          | separators                                                        |
  | single_select | a separator is drawn after "United States" and after "Mexico"     |
  | multi_select  | a separator is drawn after "United States" and after "Mexico"     |
  | autocomplete  | no separator is drawn, because a type-ahead's suggestions cannot show one |

@REQ-QB-147
Scenario: A value a reporter adds to a type-ahead is not pinned
  Given a type-ahead question offers "Woodside" pinned last and "Cooper's" not pinned
  When a reporter answering in English submits "Mount 7" for it, which the question does not offer
  Then the new value is not pinned
  And the question offers it among its unpinned choices, before "Woodside"

@REQ-QB-148
@ui
Scenario: A value a reporter adds to a type-ahead takes its alphabetical place
  Given a type-ahead question offers "Woodside" and "Cooper's", and a reporter has since added "Mount 7"
  When a reporter using English opens that question
  Then its choices are listed "Cooper's", "Mount 7", "Woodside"

@REQ-QB-149
Scenario: Pinning a choice never revises or forks its question
  Given an answered single-select question offers "Canada", "Mexico", and "Other", none pinned
  When an Administrator pins "Canada" first and "Other" last
  Then "Canada" is pinned first, "Other" is pinned last, and "Mexico" is not pinned
  And the pinned question keeps its identifier and its current revision

@REQ-QB-150
@ui
Scenario: An Administrator sets each option's position, and the editor lists options as the form does
  Given a signed-in Administrator opens the manage-questions page
  When they open a single-select question offering "Other" pinned last, and "Paraglider" and "Hang glider" not pinned
  Then its options are listed "Hang glider", "Paraglider", "Other"
  And each option offers the positions "Alphabetical", "Pin to top", and "Pin to bottom"
  When they reword "Hang glider" to "Speed wing" and set "Paraglider" to "Pin to top"
  Then the options stay where they were while the Administrator edits
  And the save sends "Paraglider" pinned first, "Other" pinned last, and "Speed wing" not pinned

@REQ-QB-151
@ui
Scenario: The required-option control lists the parent's choices as the form does
  Given a signed-in Administrator opens the manage-questions page
  When they make a question conditional on a single-select question offering "Other" pinned last, and "Paraglider" and "Hang glider" not pinned
  Then the required-option control lists "Hang glider", "Paraglider", "Other"

@REQ-QB-152
@ui
Scenario: The type-ahead review page offers merge targets as the form lists them
  Given a signed-in Safety Officer reviews a type-ahead value whose question offers "Woodside" pinned last, and "Mount 7" and "Cooper's" not pinned
  Then the value can be merged into "Cooper's", "Mount 7", or "Woodside", in that order

@REQ-QB-153
@ui
Scenario: A multi-select answer on the report page is listed as the form lists its choices
  Given a signed-in Safety Officer opens a report whose multi-select answer names "Turbulent", "Other" pinned last, and "Gusty"
  Then the answer is listed "Gusty", "Turbulent", "Other"

@REQ-QB-104
Scenario: A new installation asks for several attachments
  Given a new, empty database
  When the migrations run
  Then the seeded attachment question is labelled "Photos or videos:" and "Photos ou vidéos:"
  And its help text asks for photos, videos, or documents in both languages

@REQ-QB-105
Scenario: The seeded single-file wording on an unanswered attachment question is revised
  Given a database whose attachment question still carries its original single-file wording
  And no answer references the attachment question
  When the attachment rewording migration runs
  Then the attachment question has a new revision with the several-files wording
  And the attachment question keeps its identifier

@REQ-QB-106
Scenario: The seeded single-file wording on an answered attachment question forks it
  Given a database whose attachment question still carries its original single-file wording
  And a report has answered the attachment question
  When the attachment rewording migration runs
  Then the original attachment question is stamped as deleted and keeps its single-file wording
  And a new live question with the same key carries the several-files wording

@REQ-QB-107
Scenario: An attachment question an Administrator already reworded is left alone
  Given a database whose attachment question an Administrator has already reworded
  When the attachment rewording migration runs
  Then the attachment question and its revisions are unchanged

@REQ-QB-112
Scenario: Media consent is a system question that can never be removed or made conditional
  Given the consent_media question exists
  When an Administrator tries to delete it
  Then the attempt is rejected
  And trying to stop asking it is rejected the same way
  And trying to make it conditional on another question is rejected the same way
  And trying to give it another role is rejected the same way
  And an Administrator may still change its wording in both languages

@REQ-QB-113
@ui
Scenario Outline: The form asks for media consent only when there is a file to share
  Given a reporter is filling in the form
  When they answer yes to publication consent and attach <file>
  Then the form asks the media consent question, and it must be answered to submit
  When they remove the file, or answer no to publication consent
  Then the form no longer asks it, and submits no answer to it

Examples:
  | file       |
  | an image   |
  | a document |

@REQ-QB-114
Scenario Outline: A media consent answer is recorded on the report
  Given a submission answers yes to publication consent and attaches an image
  And it answers the consent_media question with <answer>
  When the API accepts the submission
  Then the report records media consent as <recorded>

Examples:
  | answer    | recorded   |
  | yes       | yes        |
  | no        | no         |
  | no answer | unanswered |

@REQ-QB-115
Scenario: A media consent answer must be an explicit yes or no
  Given a submission answers the consent_media question with a value that is neither yes nor no
  When the reporter submits it
  Then the API rejects the submission

@REQ-QB-116
Scenario Outline: A media consent answer covers documents only under the wording the form showed
  Given a submission answers yes to publication consent and attaches a document
  And it answers yes to the consent_media question's <revision> revision
  When the API accepts the submission
  Then the report records media consent as yes
  And the report records document consent as <documents>

Examples:
  | revision                         | documents  |
  | current                          | yes        |
  | earlier, superseded              | unanswered |

@REQ-QB-117
Scenario: Media consent names documents and says they are published as uploaded
  Given the consent_media question as seeded
  Then its wording in both languages asks about photos, videos, and documents
  And it says that documents are published exactly as they were uploaded and may contain personal details
