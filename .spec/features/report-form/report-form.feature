@xunit:collection(QuestionBankRunsAlone)
Feature: Report form
The form a reporter fills in: how each answer is stored, how type-ahead and
picker questions behave, the attachment and media-consent questions, the
Country pick list, and the seeded groups each shown as one page.

Background:
  Given the question bank stores each question as a stable, non-localized key
  And each revision has a monotonically increasing revision number for its key
  And at most one live question exists for a question key

@REQ-QB-019
Scenario Outline: Every answer is stored in its written form
  Given a reporter writing in <language> submits <submitted> as the answer to a <type> question
  When the answer is persisted
  Then the stored value is <stored>

Examples:
  | language | type       | submitted       | stored                             |
  | English  | yes/no     | the value true  | the value true                     |
  | English  | yes/no     | the value false | the value false                    |
  | French   | yes/no     | the value true  | the value true                     |
  | French   | yes/no     | the value false | the value false                    |
  | English  | checkbox   | the value true  | the value true                     |
  | French   | checkbox   | the value false | the value false                    |
  | English  | yes/no     | the value null  | nothing, as the answer was skipped |
  | English  | date       | 2026-09-21      | 2026-09-21                         |
  | French   | date       | 2026-09-21      | 2026-09-21                         |
  | English  | time       | 14:30           | 14:30                              |
  | English  | date       | an empty string | nothing, as the answer was skipped |
  | English  | short text | a line of prose | that line, as typed                |

@REQ-QB-118
Scenario Outline: An answer not in its written form is refused
  Given a reporter writing in <language> submits <submitted> as the answer to a <type> question
  When the submission is made
  Then the submission is refused
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
  | English  | yes/no   | yes                        |
  | French   | yes/no   | non                        |
  | English  | yes/no   | the word "true"            |
  | English  | date     | the value true             |
  | English  | number   | the value false            |

@REQ-QB-119
Scenario Outline: A yes or no answer has no second language
  Given a reporter writing in <language> submits <submitted> as the answer to a <type> question
  When the answer is persisted
  Then it holds no words and no second language, and its translation mode is none
  And no translation provider was called and nothing waits for the Worker to translate it

Examples:
  | language | type     | submitted       |
  | English  | yes/no   | the value true  |
  | French   | yes/no   | the value false |
  | French   | checkbox | the value true  |

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
  Then <consent> is <recorded> on the report

Examples:
  | language | consent             | answer | recorded |
  | English  | publication consent | true   | given    |
  | French   | publication consent | true   | given    |
  | English  | publication consent | false  | refused  |
  | French   | publication consent | false  | refused  |
  | French   | media consent       | true   | given    |
  | English  | media consent       | false  | refused  |

@REQ-QB-137
Scenario Outline: A yes or no stored as a word is converted to true or false once
  Given a <type> answer was stored as the word "<word>" before yes/no answers were stored as true or false
  When the database is migrated
  Then that answer reads <value>
  And the converted answer carries no words and no second language, and its translation mode is none
  And its locale is unchanged

Examples:
  | type     | word | value   |
  | yes/no   | yes  | true    |
  | yes/no   | oui  | true    |
  | yes/no   | no   | false   |
  | yes/no   | non  | false   |
  | checkbox | yes  | true    |
  | checkbox | non  | false   |

@REQ-QB-138
Scenario: A yes or no stored as anything but the four words stops the conversion
  Given a yes/no answer was stored as the word "maybe" before yes/no answers were stored as true or false
  When the database is migrated
  Then the migration fails and names no answer's value
  And no answer was converted

@REQ-QB-159
@ui
Scenario: A type-ahead question is a control the form draws, with no caret
  Given a type-ahead question with the help text "Pick the nearest site" offers "Woodside", "Mount 7", and "Cooper's", none pinned
  When a reporter using English opens that question
  Then the question is drawn by the form, closed, with no caret, described by its help text, and no browser suggestion list

@REQ-RFM-001
@ui
Scenario Outline: A type-ahead's list opens with a hint before 3 characters, however it is opened
  Given a type-ahead question with the help text "Pick the nearest site" offers "Woodside", "Mount 7", and "Cooper's", none pinned
  And a reporter using English opens that question
  When they open the question's list with <opening>
  Then the list opens directly beneath the question, as wide as it, offering only the hint to type 3 or more letters

Examples:
  | opening                         |
  | the pointer on the question     |
  | the Alt and down arrow keys     |
  | the letter "o"                  |

@REQ-QB-229
@ui
Scenario: A third character in a type-ahead reveals its matching choices
  Given a type-ahead question with the help text "Pick the nearest site" offers "Woodside", "Mount 7", and "Cooper's", none pinned
  And a reporter using English opens that question
  And they enter "Mo" in the question
  When they enter "u" in the question
  Then a list as wide as the question opens directly beneath it, offering "Mount 7"

@REQ-RFM-002
@ui
Scenario: Fewer than 3 characters in a type-ahead offer only the hint
  Given a type-ahead question with the help text "Pick the nearest site" offers "Woodside", "Mount 7", and "Cooper's", none pinned
  And a reporter using English opens that question
  When they enter "Mo" in the question
  Then the list offers only the hint to type 3 or more letters

@REQ-RFM-003
@ui
Scenario Outline: Taking a type-ahead back below 3 characters brings its hint back
  Given a type-ahead question with the help text "Pick the nearest site" offers "Woodside", "Mount 7", and "Cooper's", none pinned
  And a reporter using English opens that question
  And they enter "Mou" in the question
  When they use <keys> in the question
  Then the list offers only the hint to type 3 or more letters

Examples:
  | keys              |
  | the Backspace key |

@REQ-QB-230
@ui
Scenario Outline: Below 3 characters, no key picks a type-ahead choice
  Given a type-ahead question with the help text "Pick the nearest site" offers "Woodside", "Mount 7", and "Cooper's", none pinned
  And a reporter using English opens that question
  And they enter "Mo" in the question
  When they use <keys> in the question
  Then the list offers only the hint to type 3 or more letters
  And the question holds "Mo"

Examples:
  | keys               |
  | the down arrow key |
  | the up arrow key   |
  | the Enter key      |

@REQ-QB-232
@ui
Scenario Outline: Reopening a type-ahead filters by what it already holds, however it is reopened
  Given a type-ahead question with the help text "Pick the nearest site" offers "Woodside", "Mount 7", and "Mount Fromme", none pinned
  And a reporter using English opens that question
  And they enter "<typed>" in the question
  And they dismiss the question's list
  When they open the question's list with <opening>
  Then <outcome>

Examples:
  | typed | opening                         | outcome                                                                                      |
  | Mou   | the pointer on the question     | a list as wide as the question opens directly beneath it, offering "Mount 7", "Mount Fromme" |
  | Mou   | the Alt and down arrow keys     | a list as wide as the question opens directly beneath it, offering "Mount 7", "Mount Fromme" |
  | Mou   | the down arrow key              | a list as wide as the question opens directly beneath it, offering "Mount 7", "Mount Fromme" |
  | Mo    | the pointer on the question     | the list offers only the hint to type 3 or more letters                                      |
  | Mo    | the Alt and down arrow keys     | the list offers only the hint to type 3 or more letters                                      |
  | Mo    | the down arrow key              | the list offers only the hint to type 3 or more letters                                      |

@REQ-QB-160
@ui
Scenario Outline: Typing into a type-ahead filters its list, ignoring case and accents
  Given a type-ahead question offers "Hawk" / "Faucon", "Emu" / "Émeu", "Kestrel" / "Crécerelle", and "Eagle" / "Aigle", none pinned
  And a reporter using French opens that question
  When they enter "<typed>" in the question
  Then its list offers only <offered>

Examples:
  | typed | offered                           |
  | emeu  | "Émeu"                            |
  | CRÉ   | "Crécerelle"                      |
  | aig   | "Aigle"                           |
  | rel   | "Crécerelle"                      |

@REQ-QB-161
@ui
Scenario Outline: A reporter picks a type-ahead choice from the keyboard
  Given a type-ahead question with the help text "Pick the nearest site" offers "Woodside", "Mount 7", and "Mount Fromme", none pinned
  And a reporter using English opens that question
  And they enter "Mou" in the question
  And the question's active choice is "Mount Fromme"
  When they use <keys> in the question
  Then the list is closed and the question holds "Mount Fromme"

Examples:
  | keys          |
  | the Enter key |

@REQ-RFM-004
@ui
Scenario Outline: The arrow keys move a type-ahead's active choice
  Given a type-ahead question with the help text "Pick the nearest site" offers "Woodside", "Mount 7", and "Mount Fromme", none pinned
  And a reporter using English opens that question
  And they enter "Mou" in the question
  And the question's active choice is <start>
  When they use <keys> in the question
  Then <active> is the question's active choice

Examples:
  | start          | keys                     | active         |
  | none           | the down arrow key twice | "Mount Fromme" |
  | "Mount Fromme" | the up arrow key         | "Mount 7"      |
  | "Mount 7"      | the down arrow key       | "Mount Fromme" |

@REQ-RFM-005
@ui
Scenario Outline: A type-ahead holding a choice reopens its list with a key
  Given a type-ahead question with the help text "Pick the nearest site" offers "Woodside", "Mount 7", and "Mount Fromme", none pinned
  And a reporter using English opens that question
  And they have picked "Mount Fromme" from the question's list with the keyboard
  When they use <keys> in the question
  Then its list is open

Examples:
  | keys                        |
  | the Alt and down arrow keys |

@REQ-RFM-006
@ui
Scenario Outline: Closing a type-ahead's list keeps the choice it holds
  Given a type-ahead question with the help text "Pick the nearest site" offers "Woodside", "Mount 7", and "Mount Fromme", none pinned
  And a reporter using English opens that question
  And they have picked "Mount Fromme" from the question's list with the keyboard
  And they open the question's list with <opening>
  When they close the question's list with <closing>
  Then the list is closed and the question holds "Mount Fromme"

Examples:
  | opening                         | closing                          |
  | the Alt and down arrow keys     | the Escape key                   |
  | the pointer on the question     | the pointer outside the question |

@REQ-QB-162
@ui
Scenario: A reporter enters a type-ahead value its list does not offer
  Given a type-ahead question with the help text "Pick the nearest site" offers "Woodside", "Mount 7", and "Cooper's", none pinned
  And a reporter using English opens that question
  When they enter "A ridge nobody listed" in the question
  Then the list says no choice matches

@REQ-RFM-007
@ui
Scenario Outline: Moving on from a type-ahead keeps a value its list does not offer
  Given a type-ahead question with the help text "Pick the nearest site" offers "Woodside", "Mount 7", and "Cooper's", none pinned
  And a reporter using English opens that question
  And they enter "A ridge nobody listed" in the question
  When they use <keys> in the question
  Then the list is closed and the question holds "A ridge nobody listed"

Examples:
  | keys        |
  | the Tab key |

@REQ-QB-163
@ui
Scenario Outline: A type-ahead's or single-select's long list fits a phone-width screen
  Given a <type> question offers 30 choices
  And a reporter using English opens that question on a phone-width screen
  When they open the question's list with <opening>
  Then the list fits within the screen's width, and the page grows no wider than the screen
  And the list reaches its last choice within itself

Examples:
  | type          | opening                    |
  | type-ahead    | the letters "Launch site"  |
  | single-select | the pointer on the caret   |

@REQ-QB-231
@ui
Scenario: A dependent type-ahead's list shows a hint below 3 characters, exactly as an independent one's does
  Given the type-ahead "Model" question's choices depend on the single-select "Make" question
  And they answer "Make" with "Niviuk"
  When they open "Model"'s list
  Then "Model"'s list offers only the hint to type 3 or more letters

@REQ-RFM-008
@ui
Scenario: A dependent type-ahead's choices filter at 3 characters, exactly as an independent one's do
  Given the type-ahead "Model" question's choices depend on the single-select "Make" question
  And they answer "Make" with "Niviuk"
  When they enter "Iku" in "Model"
  Then "Model"'s list offers only "Ikuma"

@REQ-RFM-009
@ui
Scenario: A dependent type-ahead offers no choice when 3 characters match none under the parent's answer
  Given the type-ahead "Model" question's choices depend on the single-select "Make" question
  And they answer "Make" with "Niviuk"
  And they enter "Iku" in "Model"
  When they enter "Rus" in "Model"
  Then "Model"'s list offers no choice

@REQ-QB-171
@ui
Scenario: A type-ahead choice picked from the list is sent as that choice, not matched by its wording
  Given a signed-in reporter answers a type-ahead question offering two choices both worded "Other"
  And they pick the second "Other" from the list
  When they consent on the next page and send the report
  Then the answer names the second "Other" choice's identifier and carries no typed text

@REQ-RFM-010
@ui
Scenario: A type-ahead choice picked from the list shows its wording in the closed question
  Given a signed-in reporter answers a type-ahead question offering two choices both worded "Other"
  When they pick the second "Other" from the list
  Then the list is closed and the question holds "Other"

@REQ-QB-208
@ui
Scenario: A single-select question is a picker the form draws, not the browser's select
  Given a single-select question with the help text "Pick the nearest site" offers "Woodside", "Mount 7", and "Cooper's", none pinned
  When a reporter using English opens that question
  Then the question is drawn by the form, closed, with a caret showing "Choose one", described by its help text, and no browser select

@REQ-RFM-011
@ui
Scenario Outline: A single-select's list opens beneath it, drawn like a type-ahead's, however it is opened
  Given a single-select question with the help text "Pick the nearest site" offers "Woodside", "Mount 7", and "Cooper's", none pinned
  And a reporter using English opens that question
  When they open the question's list with <opening>
  Then a list as wide as the question opens directly beneath it, offering "Choose one", "Cooper's", "Mount 7", "Woodside"
  And the list is drawn like a type-ahead's list

Examples:
  | opening                         |
  | the pointer on the question     |
  | the Enter key                   |
  | the Space key                   |
  | the Alt and down arrow keys     |
  | the down arrow key              |

@REQ-QB-267
@ui
Scenario: A reporter picks a type-ahead choice with the pointer, and no choice ever takes focus
  Given a type-ahead question with the help text "Pick the nearest site" offers "Woodside", "Mount 7", and "Cooper's", none pinned
  And a reporter using English opens that question
  And they enter "Coo" in the question
  When they pick "Cooper's" from the list with the pointer
  Then the list is closed and the question holds "Cooper's"
  And the question has focus

@REQ-QB-268
@ui
Scenario: A reporter picks a single-select choice with the pointer, and no choice ever takes focus
  Given a single-select question with the help text "Pick the nearest site" offers "Woodside", "Mount 7", and "Cooper's", none pinned
  And a reporter using English opens that question
  And they open the question's list
  When they pick "Cooper's" from the list with the pointer
  Then the list is closed and the question holds "Cooper's"
  And the question has focus

@REQ-QB-209
@ui
Scenario Outline: A reporter picks a single-select choice from the keyboard
  Given a single-select question with the help text "Pick the nearest site" offers "Woodside", "Mount 7", and "Cooper's", none pinned
  And a reporter using English opens that question
  And they open the question's list
  And the question's active choice is "<active>"
  When they use <keys> in the question
  Then the list is closed and the question holds "<active>"

Examples:
  | active   | keys          |
  | Mount 7  | the Enter key |
  | Woodside | the Space key |

@REQ-RFM-012
@ui
Scenario Outline: Opening a single-select's list with a key makes "Choose one" its active choice
  Given a single-select question with the help text "Pick the nearest site" offers "Woodside", "Mount 7", and "Cooper's", none pinned
  And a reporter using English opens that question
  When they open the question's list with <opening>
  Then "Choose one" is the question's active choice

Examples:
  | opening            |
  | the down arrow key |

@REQ-RFM-013
@ui
Scenario Outline: Keys move a single-select's active choice
  Given a single-select question with the help text "Pick the nearest site" offers "Woodside", "Mount 7", and "Cooper's", none pinned
  And a reporter using English opens that question
  And they open the question's list
  And the question's active choice is "<start>"
  When they use <keys> in the question
  Then "<active>" is the question's active choice

Examples:
  | start      | keys               | active     |
  | Choose one | the down arrow key | Cooper's   |
  | Cooper's   | the End key        | Woodside   |
  | Woodside   | the Home key       | Choose one |
  | Choose one | the m key          | Mount 7    |

@REQ-RFM-014
@ui
Scenario Outline: A single-select holding a choice reopens its list with that choice chosen and active
  Given a single-select question with the help text "Pick the nearest site" offers "Woodside", "Mount 7", and "Cooper's", none pinned
  And a reporter using English opens that question
  And they have picked "Mount 7" from the question's list with the keyboard
  When they use <keys> in the question
  Then its list is open, with "Mount 7" chosen and active

Examples:
  | keys                        |
  | the Space key               |
  | the Alt and down arrow keys |

@REQ-RFM-015
@ui
Scenario Outline: Closing a single-select's list without picking keeps the choice it holds
  Given a single-select question with the help text "Pick the nearest site" offers "Woodside", "Mount 7", and "Cooper's", none pinned
  And a reporter using English opens that question
  And they have picked "<held>" from the question's list with <picking>
  And they open the question's list with <opening>
  And they make "<active>" the question's active choice with <reaching>
  When they close the question's list with <closing>
  Then the list is closed and the question holds "<held>"

Examples:
  | held     | picking      | opening                     | active   | reaching    | closing                          |
  | Mount 7  | the keyboard | the Space key               | Cooper's | the keys    | the Escape key                   |
  | Woodside | the pointer  | the pointer on the question | Cooper's | the pointer | the pointer outside the question |
  | Woodside | the pointer  | the Enter key               | Woodside | the keys    | the Tab key                      |

@REQ-RFM-016
@ui
Scenario: Pointing at a single-select's choice makes it the active choice
  Given a single-select question with the help text "Pick the nearest site" offers "Woodside", "Mount 7", and "Cooper's", none pinned
  And a reporter using English opens that question
  And they pick "Woodside" from the question's list
  And they open the question's list
  When they point at "Cooper's"
  Then "Cooper's" is the question's active choice

@REQ-QB-210
@ui
Scenario: A single-select answer can be cleared back to unanswered
  Given a single-select question with the help text "Pick the nearest site" offers "Woodside", "Mount 7", and "Cooper's", none pinned
  And a reporter using English opens that question
  And they pick "Mount 7" from the question's list
  When they pick "Choose one" from the question's list
  Then the list is closed and the question holds "Choose one"
  And the browser's saved report holds no answer to that question

@REQ-RFM-017
@ui
Scenario: A picked single-select choice shows in the closed question
  Given a single-select question with the help text "Pick the nearest site" offers "Woodside", "Mount 7", and "Cooper's", none pinned
  And a reporter using English opens that question
  When they pick "Mount 7" from the question's list
  Then the list is closed and the question holds "Mount 7"

@REQ-QB-211
@ui
Scenario: A multi-select's list is drawn like a type-ahead's list, with a checkbox on each row
  Given a multi-select question offers "United States" and "Canada" pinned first, "Other" pinned last, and "Mexico", "Brazil", and "France" not pinned
  And a reporter using English opens that question
  When they open the multi-select's list
  Then the list is drawn like a type-ahead's list
  And each choice is a row large enough to touch, holding a checkbox

@REQ-RFM-018
@ui
Scenario: Pointing at a multi-select's row highlights it as a type-ahead's active choice is
  Given a multi-select question offers "United States" and "Canada" pinned first, "Other" pinned last, and "Mexico", "Brazil", and "France" not pinned
  And a reporter using English opens that question
  And they open the multi-select's list
  When they point at "Brazil"
  Then the "Brazil" row is highlighted as a type-ahead's active choice is

@REQ-RFM-019
@ui
Scenario Outline: Checking a multi-select's choice with a key highlights its row and keeps the list open
  Given a multi-select question offers "United States" and "Canada" pinned first, "Other" pinned last, and "Mexico", "Brazil", and "France" not pinned
  And a reporter using English opens that question
  And they open the multi-select's list
  When they move to the "France" checkbox and check it with <keys>
  Then the "France" row is highlighted as a type-ahead's active choice is
  And "France" is checked, and the list stays open

Examples:
  | keys          |
  | the Space key |

@REQ-QB-104
Scenario: A new installation asks for several attachments
  Given a new, empty database
  When the migrations run
  Then the seeded attachment question is labelled "Photos or videos" and "Photos ou vidéos"
  And its help text asks for images, videos, or documents in both languages

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
  Then the original attachment question is marked deleted and keeps its single-file wording
  And a new live question with the same key carries the several-files wording

@REQ-QB-107
Scenario: An attachment question an Administrator already reworded is left alone
  Given a database whose attachment question an Administrator has already reworded
  When the attachment rewording migration runs
  Then the attachment question and its revisions are unchanged

@REQ-QB-112
Scenario: Media consent is a system question that can never be removed or made conditional
  Given the media consent question exists
  When an Administrator tries to delete it
  Then the attempt is refused
  And trying to stop asking it is refused the same way
  And trying to make it conditional on another question is refused the same way
  And trying to give it another role is refused the same way
  And an Administrator may still change its wording in both languages

@REQ-QB-113
@ui
Scenario Outline: The form asks for media consent only when there is a file to share
  Given a reporter is filling in the form
  When they answer yes to publication consent and attach <file>
  Then the form asks the media consent question, and it must be answered to submit

Examples:
  | file       |
  | an image   |
  | a document |

@REQ-RFM-020
@ui
Scenario Outline: The form stops asking for media consent once the file is removed or publication consent is withdrawn
  Given a reporter is filling in the form
  And they answer yes to publication consent and attach <file>
  And they have reached the media consent question and tried to submit without answering it
  When they remove the file, or answer no to publication consent
  Then the form no longer asks it, and submits no answer to it

Examples:
  | file       |
  | an image   |
  | a document |

@REQ-QB-114
Scenario Outline: A media consent answer is recorded on the report
  Given a submission answers yes to publication consent and attaches an image
  And it answers the media consent question with <answer>
  When the report is accepted
  Then the report records media consent as <recorded>

Examples:
  | answer    | recorded   |
  | yes       | yes        |
  | no        | no         |
  | no answer | unanswered |

@REQ-QB-115
Scenario: A media consent answer must be an explicit yes or no
  Given a submission answers the media consent question with a value that is neither yes nor no
  When the reporter submits it
  Then the submission is refused as invalid

@REQ-QB-116
Scenario: A media consent answer covers documents when it answers the wording the form showed
  Given a submission answers yes to publication consent and attaches a document
  And it answers yes to the media consent question's current revision
  When the report is accepted
  Then the report records media consent as yes
  And the report records document consent as yes

@REQ-QB-247
Scenario: A media consent answer naming an earlier wording is refused, so no document is published on it
  Given a submission answers yes to publication consent and attaches a document
  And it answers yes to the media consent question's earlier, superseded revision
  When the reporter submits it
  Then the submission is refused as invalid

@REQ-QB-117
Scenario: Media consent names documents and says they are published as uploaded
  Given the media consent question as seeded
  Then its wording in both languages asks about images, videos, and documents
  And it says that documents are published exactly as they were uploaded and may contain personal details

@REQ-QB-249
Scenario: The form offers Country as an optional single-select of every country, with Canada and the United States pinned first
  When the report form's questions are read
  Then the Country question is an optional single-select labelled "Country" and "Pays"
  And its help text is "Country where the occurrence happened." and "Pays où l'évènement a eu lieu."
  And it offers 249 countries, each coded by its lowercase ISO 3166-1 alpha-2 code
  And "Canada" and "United States" are pinned first and every other country is not pinned
  And the French wording of "us" is "États-Unis"
  And the Province question depends on the Canada choice of the Country question

@REQ-QB-250
Scenario: The seeded yes/no Country question is revised into the pick list when no answer references it
  Given a database whose Country question is still the seeded yes/no question
  And no answer references the Country question
  When the Country pick list migration runs
  Then the Country question has a new revision as a single-select with the pick-list wording
  And the Country question keeps its identifier and offers 249 countries
  And the Province question has a new revision that depends on the Canada choice and keeps its identifier

@REQ-QB-251
Scenario: The seeded yes/no Country question is forked into the pick list when a report has answered it
  Given a database whose Country question is still the seeded yes/no question
  And a report has answered the Country question yes
  When the Country pick list migration runs
  Then the original Country question is marked deleted and keeps its yes/no wording and its answer
  And a new live question with the same key is a single-select offering 249 countries
  And no answer is created, changed, or deleted

@REQ-QB-252
Scenario: An answered Province is forked with its choices when it begins to follow Country
  Given a database whose Country question is still the seeded yes/no question
  And a report has answered the Province question "Ontario"
  When the Country pick list migration runs
  Then the original Province question is marked deleted and keeps its answer
  And a new live Province question with the same key offers the same 13 provinces and depends on the Canada choice
  And the Province answer still names the choice it named

@REQ-QB-253
Scenario: The Country pick list migration run a second time changes nothing
  Given a database whose Country question is still the seeded yes/no question
  And the Country pick list migration has run
  When the Country pick list script is run again
  Then the questions, revisions, and choices are unchanged

@REQ-QB-254
Scenario: A Country question an Administrator already changed is left alone
  Given a database whose Country question an Administrator has already reworded
  When the Country pick list migration runs
  Then the Country question and its revisions are unchanged
  And the Province question is still unconditional

@REQ-QB-255
Scenario: A question that waited for the old yes/no Country answer now waits for Canada
  Given a database whose Country question is still the seeded yes/no question
  And another question is shown only when the old Country question is answered yes
  When the Country pick list migration runs
  Then that question is shown only when Country is the Canada choice

@REQ-QB-256
@ui
Scenario Outline: The Country list reads Canada, United States, a separator, then every other country alphabetically
  Given the form asks the Country question as the migrations seed it
  When a reporter using <language> opens the Country question
  Then its open list reads <list>

Examples:
  | language | list                                                                                         |
  | English  | "Canada", "United States", a separator, "Australia", "Brazil", "Mexico", "Zambia"            |
  | French   | "Canada", "États-Unis", a separator, "Australie", "Brésil", "Mexique", "Zambie"              |

@REQ-QB-257
@ui
Scenario: Country is optional
  Given the form asks the Country question as the migrations seed it
  And a reporter using English opens the Country question
  When they go on without choosing a country
  Then the form moves on without asking Province
  And no message says an answer is required

@REQ-QB-258
@ui
Scenario: Province is shown only when Country is Canada
  Given the form asks the Country question as the migrations seed it
  When a reporter using English chooses "Canada" for Country and goes on
  Then the Province question is shown

@REQ-RFM-021
@ui
Scenario: Changing Country away from Canada no longer asks Province
  Given the form asks the Country question as the migrations seed it
  And a reporter using English chooses "Canada" for Country and goes on
  When the reporter goes back and chooses "Mexico" for Country and goes on
  Then the form moves on without asking Province

@REQ-QB-259
Scenario Outline: A freshly migrated database sends each seeded group with its questions
  When the report form's questions are read from a freshly migrated database
  Then the group <group> holds <questions>, in that order
  And none of those questions is sent at the top level

Examples:
  | group      | questions                                                    |
  | "From"     | "First name", "Last name", "Phone number", "Email"           |
  | "Pilot"    | "First name", "Last name"                                    |
  | "Aircraft" | "Type of aircraft", "Manufacturer", "Model", "Certification" |

@REQ-QB-260
Scenario: A seeded question that lost its group gets a new revision grouped under it when no answer references it
  Given a database created from scratch before the seeded-group repair
  And no answer references the "From" group's questions
  When the seeded-group repair migration runs
  Then each of the "From" group's questions has a new revision grouped under "From" and keeps its identifier

@REQ-QB-261
Scenario: A seeded question that lost its group is forked under it when a report has answered it
  Given a database created from scratch before the seeded-group repair
  And a report has answered the reporter's "First name"
  When the seeded-group repair migration runs
  Then the original "First name" question is marked deleted and keeps its answer
  And a new live question with the same key is grouped under "From"
  And the one answer still names the original "First name" question, unchanged

@REQ-QB-262
Scenario: The seeded-group repair run a second time changes nothing
  Given a database created from scratch before the seeded-group repair
  And the seeded-group repair migration has run
  When the seeded-group repair script is run again
  Then the questions, revisions, and choices are unchanged

@REQ-QB-263
Scenario: A seeded question an Administrator has grouped is left alone
  Given a database created from scratch before the seeded-group repair
  And an Administrator has grouped the reporter's "First name" under a group of their own
  When the seeded-group repair migration runs
  Then the reporter's "First name" is still grouped under that group, with no new revision

@REQ-QB-264
Scenario: A seeded question whose group is no longer live is left alone
  Given a database created from scratch before the seeded-group repair
  And the "Pilot" group has been deleted
  When the seeded-group repair migration runs
  Then the "Pilot" group's questions are still ungrouped, with no new revision

@REQ-QB-265
Scenario: The browser suite's seeded form is what a freshly migrated database sends
  When the report form's questions are read from a freshly migrated database
  Then they are exactly the browser suite's seeded-form fixture

@REQ-QB-266
@ui
Scenario Outline: Each seeded group is one page with its heading and exactly its questions
  Given the form asks the questions the migrations seed
  When a reporter using <language> moves through the form to the <group> group
  Then the page is headed <group> and asks exactly <questions>

Examples:
  | language | group      | questions                                                                 |
  | English  | "From"     | "First name", "Last name", "Phone number", "Email"                        |
  | English  | "Pilot"    | "First name", "Last name"                                                 |
  | English  | "Aircraft" | "Type of aircraft", "Manufacturer", "Model", "Certification"              |
  | French   | "Qui"      | "Prénom", "Nom de famille", "Numéro de téléphone", "Courriel"             |
