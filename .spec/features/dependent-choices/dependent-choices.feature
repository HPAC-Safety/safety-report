@xunit:collection(QuestionBankRunsAlone)
Feature: Dependent choices
A single-select or type-ahead question's choices may depend on an earlier
single-select or type-ahead, one level deep: each choice names the parent
choices it is offered under.

Background:
  Given the question bank stores each question as a stable, non-localized key
  And each revision has a monotonically increasing revision number for its key
  And at most one live question exists for a question key

@REQ-QB-179
Scenario Outline: A picker or type-ahead's choices may depend on another picker or type-ahead
  Given a <parent> question, offering "Niviuk" and "Ozone" when its type has choices
  When an Administrator makes a <child> question's choices depend on it, linking each choice to one of its choices
  Then the dependency is <outcome>

Examples:
  | parent        | child         | outcome  |
  | single-select | type-ahead    | accepted |
  | type-ahead    | type-ahead    | accepted |
  | type-ahead    | single-select | accepted |
  | single-select | single-select | accepted |
  | multi-select  | type-ahead    | refused  |
  | single-select | multi-select  | refused  |
  | yes/no        | type-ahead    | refused  |

@REQ-QB-180
Scenario: A dependency is one level deep
  Given the "Model" question's choices depend on the "Make" question
  When an Administrator makes a third question's choices depend on "Model"
  Then the dependency is refused, saying "Model" already depends on another question
  When an Administrator makes the "Make" question's choices depend on a third question
  Then the dependency is refused, saying other questions' choices already depend on "Make"

@REQ-QB-181
Scenario: The parent comes before the child on the form
  Given the "Make" question comes after the "Model" question on the form
  When an Administrator makes the "Model" question's choices depend on "Make"
  Then the dependency is refused, naming both questions
  Given the "Model" question's choices depend on the "Make" question, which comes before it
  When an Administrator moves "Model" before "Make"
  Then the new order is refused, naming both questions

@REQ-QB-184
Scenario: A dependency and its links sit outside revisions
  Given an answered "Make" question and an answered "Model" question
  When an Administrator makes "Model"'s choices depend on "Make" and links each choice
  And later links one choice to a different parent choice
  Then neither question gains a revision, and neither is replaced
  And every earlier answer still names the choice it named

@REQ-QB-185
Scenario: Removing a question's parent keeps the links and stops filtering
  Given the "Model" question's choices depend on the "Make" question
  When an Administrator clears the "Model" question's parent
  Then each "Model" choice keeps its link
  And the report form offers every "Model" choice, whatever "Make" is answered with
  And a choice added to "Model" needs no parent choice

@REQ-QB-187
Scenario: A replaced picker parent choice passes its child links to the replacement
  Given the "Model" question's choices depend on a single-select "Make" question
  And "Mentor 7" is linked to the "Niviuk" choice
  When an Administrator replaces the parent choice "Niviuk" with "Niviuk Gliders"
  Then "Mentor 7" is linked to "Niviuk Gliders"
  And neither question gains a revision

@REQ-QB-188
Scenario: A merged type-ahead parent value passes its child links to the value it was merged into
  Given the "Model" question's choices depend on a type-ahead "Make" question
  And "Mentor 7" is linked to the "Nivuik" value
  When a Safety Officer merges the parent value "Nivuik" into "Niviuk"
  Then "Mentor 7" is linked to "Niviuk"

@REQ-QB-189
Scenario: A dependency follows its parent when the parent forks
  Given the "Model" question's choices depend on an answered single-select "Make" question
  And "Mentor 7" is linked to the "Niviuk" choice
  When an Administrator changes the "Make" question's wording
  Then "Model"'s choices depend on the question that replaced "Make"
  And "Mentor 7" is linked to that question's copy of "Niviuk"
  And "Model" gains no revision

@REQ-QB-190
Scenario: A forked dependent question copies every choice with its links
  Given the answered "Model" question's choices depend on the "Make" question
  And "Mentor 7" is linked to the "Niviuk" choice
  When an Administrator changes the "Model" question's wording
  Then the question that replaced "Model" depends on "Make"
  And its copy of "Mentor 7" is linked to "Niviuk"

@REQ-QB-191
Scenario: The report form's questions name each dependency and each link
  Given the "Model" question's choices depend on the "Make" question
  And "Other" is offered under "Niviuk" and "Ozone"
  When the report form loads today's questions
  Then the "Model" question names "Make" as the question its choices depend on
  And each "Model" choice names every "Make" choice it is offered under
  And a question whose choices depend on nothing names no parent

@REQ-QB-192
Scenario Outline: A reporter's new value in a dependent type-ahead is linked to the parent's answer
  Given the type-ahead "Model" question's choices depend on the type-ahead "Make" question
  When a reporter answers "Make" with <make> and types "Zeno 2" for "Model", which "Model" does not offer
  Then "Model" gains a reporter-added value "Zeno 2", linked to <linked>
  And the "Model" answer names that value and nothing about "Make"

Examples:
  | make                                          | linked                                    |
  | its "Ozone" choice                            | "Ozone"                                   |
  | "Gin", a value "Make" does not offer          | the reporter-added "Make" value "Gin"     |

@REQ-QB-195
@ui
Scenario: An Administrator picks the question a question's choices depend on, and clears it
  Given an Administrator opens the manage-questions page
  When they edit a type-ahead question placed after a single-select "Make", a type-ahead "Site", a multi-select "Conditions", and a type-ahead "Model" whose choices depend on "Make"
  Then its "Choices depend on" control offers "Make" and "Site" only
  When they pick "Make", link every choice, and save
  Then the save names "Make" as the question its choices depend on
  When they clear "Choices depend on" and save
  Then the save names no parent question and keeps every choice's link

@REQ-QB-197
@ui
Scenario Outline: A dependent question offers only the choices linked to the parent's answer
  Given a <child> "Model" question's choices depend on a single-select "Make" question offering "Niviuk" and "Ozone"
  And "Model" offers "Mentor 7" and "Ikuma" linked to "Niviuk", and "Rush 6" linked to "Ozone"
  When a reporter using <language> opens the page asking both
  Then "Model" is disabled, and says to answer "Make" first
  When they answer "Make" with "Niviuk"
  Then "Model" is enabled and offers only "Ikuma" and "Mentor 7"

Examples:
  | child         | language |
  | single-select | English  |
  | type-ahead    | English  |
  | type-ahead    | French   |

@REQ-QB-198
@ui
Scenario: Changing the parent's answer clears a child answer it no longer offers
  Given the type-ahead "Model" question's choices depend on the single-select "Make" question
  And a reporter answered "Make" with "Niviuk" and picked "Mentor 7" for "Model"
  When they change "Make" to "Ozone"
  Then "Model" is empty and offers only "Rush 6"
  When they type "Zeno 2", a value "Model" does not offer, and change "Make" to "Niviuk"
  Then "Model" still holds "Zeno 2"

@REQ-QB-199
@ui
Scenario: A parent answered with a new value leaves the child nothing to pick, but a value to type
  Given the type-ahead "Model" question's choices depend on the type-ahead "Make" question
  When a reporter types "Gin", a value "Make" does not offer, for "Make"
  Then "Model" is enabled and its list offers no choice
  When they type "Zeno 2" for "Model" and send the report
  Then "Make" is sent as "Gin" and "Model" as "Zeno 2", both as the words typed

@REQ-QB-200
@ui
Scenario: A saved report restores the parent and child answers together
  Given a reporter answered "Make" with "Niviuk" and "Model" with "Mentor 7", and the browser saved the report
  When they come back and continue the saved report
  Then "Make" holds "Niviuk", and "Model" holds "Mentor 7" and offers only the "Niviuk" models
  And a saved "Model" answer no longer linked to the saved "Make" answer is restored empty

@REQ-QB-201
@ui
Scenario: A dependent child that cannot be answered yet does not hold the reporter back
  Given a required "Model" question's choices depend on an optional "Make" question
  When a reporter leaves "Make" unanswered and presses Next
  Then the form moves on past "Model", which cannot be answered until "Make" is
  When they consent and send the report
  Then the report is sent with no answer to "Model"

@REQ-QB-204
@ui
Scenario: A picker child with nothing under the parent's answer says so and does not hold the reporter back
  Given a required single-select "Model" question's choices depend on the single-select "Make" question, and nothing is offered under "Gin"
  When a reporter answers "Make" with "Gin"
  Then "Model" is disabled, and says no choice is listed for that answer
  And pressing Next moves on

@REQ-QB-205
@ui
Scenario: The manage-questions page shows why a question cannot move above its parent
  Given an Administrator opens the manage-questions page
  When they move a question whose choices depend on the question above it up, and the new order is refused
  Then the page shows the refusal, naming both questions
  And the list keeps its order

@REQ-QB-206
Scenario: The parent comes before the child wherever grouping places them
  Given a group question comes before the "Make" question on the form
  When an Administrator makes a question grouped under that group depend on "Make"
  Then the dependency is refused, naming both questions
  Given the "Model" question's choices depend on the "Make" question, which comes before it
  When an Administrator groups "Make" under a group question placed after "Model"
  Then the change is refused, naming both questions

@REQ-QB-203
Scenario Outline: A parent the form does not ask filters nothing
  Given the "Model" question's choices depend on the "Make" question
  When an Administrator <removes> "Make"
  Then the report form names no parent for "Model" and offers every "Model" choice
  And a report answering "Model" with any of its choices, and not answering "Make", is accepted

Examples:
  | removes     |
  | deactivates |
  | deletes     |

@REQ-QB-212
Scenario: Every choice of a dependent question is offered under at least one parent choice
  Given a type-ahead question "Model" offers "Mentor 7" and "Rush 6"
  When an Administrator makes its choices depend on the "Make" question, offering only "Mentor 7" under "Niviuk"
  Then the save is refused, naming "Rush 6"
  And nothing is saved
  When they offer "Mentor 7" under "Niviuk" and "Ozone", and "Rush 6" under "Ozone", in the same save
  Then the dependency is saved, with "Mentor 7" under both and "Rush 6" under "Ozone"
  And adding a choice to "Model" under no parent choice is refused
  And offering a choice under a choice of any question other than "Make" is refused

@REQ-QB-213
Scenario Outline: One choice is offered under several parent choices, and its wording is unique on the question
  Given the "Model" question's choices depend on the "Make" question
  When an Administrator adds "Other", in French "Autre", offered under "Niviuk" and "Ozone"
  Then "Model" offers one "Other" choice, offered under both
  And adding <wording> under any parent choice is refused, naming it

Examples:
  | wording                              |
  | a second "Other"                     |
  | " other " in English                 |
  | a choice whose French reads "Autre"  |

@REQ-QB-214
Scenario Outline: A parent choice is removed only while every child choice under it keeps another parent
  Given the "Model" question's choices depend on a <parent> "Make" question
  And "Other" is offered under "Niviuk" and "Ozone", and "Mentor 7" under "Niviuk" only
  When <who> removes "Niviuk"
  Then the removal is refused, naming "Mentor 7" and not "Other"
  When "Mentor 7" is also offered under "Ozone" and <who> removes "Niviuk" again
  Then "Niviuk" is removed
  And "Other" and "Mentor 7" keep their "Niviuk" links, which filter nothing
  And saving "Model" again, as the editor sends it, succeeds and keeps the "Niviuk" links
  And a reviewer offering "Other" under "Ozone" only succeeds and keeps its "Niviuk" link
  And a reviewer offering "Other" under the removed "Niviuk" alone is refused, and "Other" stays under "Ozone"

Examples:
  | parent        | who                                            |
  | single-select | an Administrator saving the question           |
  | type-ahead    | a Safety Officer on the type-ahead review page |

@REQ-QB-215
Scenario: Merging a parent value into one the child choice already names leaves one link
  Given the "Model" question's choices depend on a type-ahead "Make" question
  And "Other" is offered under "Nivuik" and "Niviuk"
  When a Safety Officer merges the parent value "Nivuik" into "Niviuk"
  Then "Other" is offered under "Niviuk" once
  And its link to "Nivuik" is marked removed, not erased

@REQ-QB-216
Scenario: A reporter's typed value in a dependent type-ahead names a value already offered under the parent's answer
  Given the "Model" question offers "Other" under "Niviuk" and "Ozone"
  When a reporter answers "Make" with "Ozone" and types " other " for "Model"
  Then the "Model" answer names that "Other"
  And "Model" gains no new value, and "Other" is not flagged for review

@REQ-QB-217
Scenario: A reporter's typed value matching a value under another parent answer links it and flags it
  Given the "Model" question offers "Mentor 7" under "Niviuk" only
  When a reporter answers "Make" with "Ozone" and types "mentor 7" for "Model"
  Then the "Model" answer names "Mentor 7"
  And "Model" gains no new value
  And "Mentor 7" is offered under "Niviuk" and "Ozone", and is flagged for review

@REQ-QB-218
Scenario: A reporter's typed value matching a merged value names the value it was merged into
  Given the "Model" value "Mentr 7" was merged into "Mentor 7", which is offered under "Niviuk" only
  When a reporter answers "Make" with "Ozone" and types "Mentr 7" for "Model"
  Then the "Model" answer names "Mentor 7"
  And "Mentor 7" is offered under "Niviuk" and "Ozone", and is flagged for review

@REQ-QB-219
Scenario: A reporter's typed value matching a removed value brings it back flagged, not revived
  Given the "Model" value "Zeno 1" was removed
  When a reporter answers "Make" with "Ozone" and types "Zeno 1" for "Model"
  Then the "Model" answer names "Zeno 1"
  And "Zeno 1" is flagged for review and still removed

@REQ-QB-220
Scenario: A reviewer adds and removes a dependent type-ahead value's parents, never down to none
  Given a reporter added the "Model" value "Zeno 2", offered under "Ozone"
  When a Safety Officer offers "Zeno 2" under "Ozone" and "Niviuk"
  Then "Zeno 2" is offered under both, and every answer naming it still names it
  When they offer it under "Niviuk" only
  Then its "Ozone" link is marked removed, not erased
  And offering it under no parent choice is refused
  And offering it under a choice of any question other than "Make" is refused
  And changing the parents of a "Model" value that was merged into another is refused

@REQ-QB-221
Scenario: Merging dependent type-ahead values offers the survivor under every parent either was under
  Given the "Model" values "Zeno 2" under "Ozone" and "Zeno two" under "Niviuk"
  When a Safety Officer merges the value "Zeno two" into "Zeno 2"
  Then "Zeno 2" is offered under "Ozone" and "Niviuk"
  And every answer naming "Zeno two" reads "Zeno 2", and none is rewritten

@REQ-QB-222
@ui
Scenario Outline: Each choice of a dependent question picks the parent choices it is offered under
  Given an Administrator using <language> opens the manage-questions page
  When they make a type-ahead question's choices depend on a single-select question offering "Other" pinned last, and "Ozone" and "Niviuk" not pinned
  Then every choice row, a new one included, has an "Offered under" multi-select listing "Niviuk", "Ozone", "Other"
  And Save is refused while a choice is offered under nothing, naming that choice in <language>, with its multi-select marked invalid
  When they tick "Niviuk" and "Ozone" for one choice and "Ozone" for every other, and save
  Then the save sends each choice with every parent choice ticked for it

Examples:
  | language |
  | English  |
  | French   |

@REQ-QB-223
@ui
Scenario: The form offers one choice under each of its parent answers and keeps it across them
  Given the type-ahead "Model" question's choices depend on the single-select "Make" question
  And "Model" offers "Other" under "Niviuk" and "Ozone", "Mentor 7" under "Niviuk", and "Rush 6" under "Ozone"
  When a reporter answers "Make" with "Niviuk"
  Then "Model" offers "Mentor 7" and "Other"
  When they pick "Other" and change "Make" to "Ozone"
  Then "Model" still holds "Other" and offers "Other" and "Rush 6"
  When the browser saved the report and they come back and continue it
  Then "Make" holds "Ozone" and "Model" holds "Other"

@REQ-QB-227
@ui
Scenario: Words typed into a dependent type-ahead are kept and sent as typed, even when they read as a choice under another answer
  Given the type-ahead "Model" question's choices depend on the single-select "Make" question
  When a reporter answers "Make" with "Ozone" and types "Mentor 7", which is offered only under "Niviuk"
  Then "Model" still holds "Mentor 7"
  When they press Next, consent, and send the report
  Then "Model" is sent as the words "Mentor 7"

@REQ-QB-224
@ui
Scenario: The type-ahead review page shows every parent of a dependent value and edits them
  Given a Safety Officer reviews the reporter-added "Model" value "Zeno 2", offered under "Ozone"
  And "Zeno 2" is also linked to "Gin", a "Make" value since removed
  Then the value shows that it is offered under "Ozone"
  And its "Offered under" control lists "Make"'s choices
  When they also tick "Niviuk"
  Then the page sends "Ozone" and "Niviuk"
  And the page does not let them untick the last parent choice, and says why

@REQ-QB-225
Scenario: The migration keeps each link as one of the choice's parents and merges identical duplicates
  Given a database one migration short, whose dependent "Certification:" question offers "EN-A" to "EN-D" twice each, one copy under "Paraglider" and one under "Hang Glider", and "EN-CCC" once, under "Paraglider"
  And reports answered "Certification:" with both copies of "EN-A"
  And a question is conditional on the "Hang Glider" copy of "EN-B"
  When the migration runs
  Then "Certification:" offers one "EN-A" to "EN-D" each, the oldest copy, offered under "Paraglider" and "Hang Glider"
  And each other copy is retired, replaced by the one that survived
  And "EN-CCC" is offered under "Paraglider" only
  And every answer still names the choice it named and reads the same wording
  And the conditional question's condition follows the surviving "EN-B"
  And no choice keeps its old single parent link

@REQ-QB-228
Scenario: The migration merges a dependent type-ahead's identical duplicates into the oldest
  Given a database one migration short, whose dependent type-ahead "Certification:" question offers "EN-A" twice, one copy under "Paraglider" and one under "Hang Glider"
  And the value "EN A" was merged into the "Hang Glider" copy
  And a report answered "Certification:" with the "Hang Glider" copy
  When the migration runs
  Then "EN-A" is offered once, the oldest copy, under "Paraglider" and "Hang Glider"
  And the other copy is merged into it, and so is "EN A"
  And the answer still names the copy it named, which reads as "EN-A"

@REQ-QB-226
Scenario: The migration merges no pair whose wording matches in one language only
  Given a database one migration short, whose dependent question offers "EN-A" / "EN-A" under "Paraglider" and "EN-A" / "EN-A (FR)" under "Hang Glider"
  When the migration runs
  Then both choices stay live, each under its own parent choice
  And the question's next save is refused, naming "EN-A"
