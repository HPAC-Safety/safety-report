@xunit:collection(QuestionBankRunsAlone)
Feature: Choices and type-ahead values
A single-select, multi-select, or type-ahead question owns its choices
outside its revisions. An answer names its choice; a picker option is fixed
or replaced; a type-ahead value a reporter adds is reviewed, corrected,
merged, or removed by a Safety Officer or an Administrator.

Background:
  Given the question bank stores each question as a stable, non-localized key
  And each revision has a monotonically increasing revision number for its key
  And at most one live question exists for a question key

@REQ-QB-036
Scenario: Two reporters naming the same new site produce one choice
  Given a reporter has already added a site to a type-ahead question
  When another reporter submits the same site name
  Then the existing choice is reused rather than duplicated
  And an Administrator's wording is never replaced by a reporter's

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
  Given an Administrator opens the manage-questions page
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

@REQ-QB-233
@ui
Scenario: Typing a merged-away wording offers the survivor, hinting the alias that matched
  Given a type-ahead question offers "Cooper's Hill", one merged from "Coopers"
  When a reporter using English opens that question
  And they type "Coopers" in the field
  Then the list offers "Cooper's Hill", hinting "also: Coopers"

@REQ-QB-234
@ui
Scenario: A merged-away wording matches typing in the other language too
  Given a type-ahead question offers "Cooper's Hill" / "Colline Cooper", one merged from "Colline du Cooper"
  When a reporter using English opens that question
  And they type "Colline du Cooper" in the field
  Then the list offers "Cooper's Hill", hinting "also: Colline du Cooper"

@REQ-QB-235
@ui
Scenario: A chained merge offers the final survivor, hinting the first wording
  Given a type-ahead question offers "Cooper's Hill", merged from "Cooper's", itself merged from "Coopers"
  When a reporter using English opens that question
  And they type "Coopers" in the field
  Then the list offers "Cooper's Hill", hinting "also: Coopers"

@REQ-QB-236
@ui
Scenario: Under a dependent type-ahead, a merged-away wording offers the survivor only under its own parent choices
  Given the type-ahead "Model" question's choices depend on the single-select "Make" question, and its "Mentor 7" under "Niviuk" was merged from "Mentr 7"
  When they answer "Make" with "Ozone"
  And they type "Mentr 7" in "Model"
  Then "Model"'s list offers no choice
  When they change "Make" to "Niviuk"
  And they type "Mentr 7" in "Model"
  Then "Model"'s list offers "Mentor 7", hinting "also: Mentr 7"

@REQ-QB-237
@ui
Scenario: The type-ahead review page lists a value's aliases, chains included
  Given a Safety Officer reviews a flagged value that two earlier wordings, one itself merged from a third, were merged into
  When they open the review-type-ahead-values page
  Then the value shows its aliases "Coopers" and "Cooper's"

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
  Given a <type> question offers "Green Ridge" / "Crête Verte", "Stone Ridge" / "Crête de Pierre", "Silver Ridge" / "Crête d'Argent", and "Blue Ridge" / "Crête Bleue", none pinned
  When a reporter using <language> opens that question
  Then its choices are listed <order>

Examples:
  | type          | language | order                                                             |
  | single_select | English  | "Blue Ridge", "Green Ridge", "Silver Ridge", "Stone Ridge"        |
  | single_select | French   | "Crête Bleue", "Crête d'Argent", "Crête de Pierre", "Crête Verte" |
  | multi_select  | French   | "Crête Bleue", "Crête d'Argent", "Crête de Pierre", "Crête Verte" |
  | autocomplete  | French   | "Crête Bleue", "Crête d'Argent", "Crête de Pierre", "Crête Verte" |

@REQ-QB-146
@ui
Scenario Outline: Pinned choices come first or last, each group alphabetical
  Given a <type> question offers "Southland" and "Northland" pinned first, "Otherland" pinned last, and "Westland", "Eastland", and "Midland" not pinned
  When a reporter using English opens that question
  Then its choices are listed "Northland", "Southland", "Eastland", "Midland", "Westland", "Otherland"
  And <separators>

Examples:
  | type          | separators                                                       |
  | single_select | a separator is drawn after "Southland" and after "Westland"     |
  | multi_select  | a separator is drawn after "Southland" and after "Westland"     |
  | autocomplete  | a separator is drawn after "Southland" and after "Westland"     |

@REQ-QB-147
Scenario: A value a reporter adds to a type-ahead is not pinned
  Given a type-ahead question offers "Woodside" pinned last and "Cooper's" not pinned
  When a reporter answering in English submits "Mount 7" for it, which the question does not offer
  Then the new value is not pinned
  And the question offers it among its unpinned choices, before "Woodside"

@REQ-QB-148
@ui
Scenario: A value a reporter adds to a type-ahead takes its alphabetical place
  Given a type-ahead question offers "Wood Ridge" and "Cooper Ridge", and a reporter has since added "Mount Ridge"
  When a reporter using English opens that question
  Then its choices are listed "Cooper Ridge", "Mount Ridge", "Wood Ridge"

@REQ-QB-149
Scenario: Pinning a choice never revises or forks its question
  Given an answered single-select question offers "Canada", "Mexico", and "Other", none pinned
  When an Administrator pins "Canada" first and "Other" last
  Then "Canada" is pinned first, "Other" is pinned last, and "Mexico" is not pinned
  And the pinned question keeps its identifier and its current revision

@REQ-QB-150
@ui
Scenario: An Administrator sets each option's position, and the editor lists options as the form does
  Given an Administrator opens the manage-questions page
  When they open a single-select question offering "Other" pinned last, and "Paraglider" and "Hang glider" not pinned
  Then its options are listed "Hang glider", "Paraglider", "Other"
  And each option offers the positions "Alphabetical", "Pin to top", and "Pin to bottom"
  When they reword "Hang glider" to "Speed wing" and set "Paraglider" to "Pin to top"
  Then the options stay where they were while the Administrator edits
  And the save sends "Paraglider" pinned first, "Other" pinned last, and "Speed wing" not pinned

@REQ-QB-151
@ui
Scenario: The required-option control lists the parent's choices as the form does
  Given an Administrator opens the manage-questions page
  When they make a question conditional on a single-select question offering "Other" pinned last, and "Paraglider" and "Hang glider" not pinned
  Then the required-option control lists "Hang glider", "Paraglider", "Other"

@REQ-QB-152
@ui
Scenario: The type-ahead review page offers merge targets as the form lists them
  Given a Safety Officer reviews a type-ahead value whose question offers "Woodside" pinned last, and "Mount 7" and "Cooper's" not pinned
  Then the value can be merged into "Cooper's", "Mount 7", or "Woodside", in that order

@REQ-QB-153
@ui
Scenario: A multi-select answer on the report page is listed as the form lists its choices
  Given a Safety Officer opens a report whose multi-select answer names "Turbulent", "Other" pinned last, and "Gusty"
  Then the answer is listed "Gusty", "Turbulent", "Other"

@REQ-MOD-094
Scenario Outline: A Safety Officer or an Administrator reviews type-ahead values
  Given a member has the <role> role
  When that member approves, corrects, merges, relinks, or removes a reporter-added type-ahead value
  Then the API <outcome> the attempt

Examples:
  | role          | outcome  |
  | User          | forbids  |
  | SafetyOfficer | allows   |
  | Administrator | allows   |

@REQ-MOD-097
@ui
Scenario: A Safety Officer approves, corrects, and removes type-ahead values on the review page
  Given a Safety Officer and three type-ahead values flagged for review
  When they open the review-type-ahead-values page
  Then each value is listed under its question's heading, with the language it was typed in and how many answers name it
  When they approve "Mount 7", correct "coopers" to "Cooper's", and remove "Test site"
  Then the API is asked to approve, correct, and remove exactly those values
  And the page lists no value left to review

@REQ-MOD-095
@ui
Scenario: A Safety Officer reviews flagged type-ahead values on one page
  Given a Safety Officer and two type-ahead questions with values flagged for review
  When they open the review-type-ahead-values page
  Then every flagged value is listed under its question's heading, with its language and how many answers name it
  When they merge "Coopers" into "Cooper's"
  Then "Coopers" leaves the list
  And "Cooper's" is no longer flagged

@REQ-MOD-160
@ui
Scenario: The review queue groups flagged values under their question, questions ordered alphabetically
  Given a Safety Officer and flagged values under two type-ahead questions, returned by the API with the later question first
  When they open the review-type-ahead-values page
  Then the question headings read, top to bottom, "Where did this happen?" then "Where did you launch?"

@REQ-MOD-161
@ui
Scenario Outline: Values within a question's group are sorted alphabetically in the viewer's language, ignoring case and accents
  Given a Safety Officer who reads <language> and one type-ahead question whose flagged values are "<second>", "<first>", and "<third>", in that order
  When they open the review-type-ahead-values page
  Then the values under that question's heading read, top to bottom, "<first>", "<second>", and "<third>"

Examples:
  | language | first  | second   | third |
  | English  | cooper | Cooper's | zulu  |
  | French   | Étang  | Etna     | zone  |

@REQ-MOD-162
@ui
Scenario Outline: Approving, correcting, removing, merging, and relinking a value keeps the reviewer's scroll position, with no loading state
  Given a Safety Officer and twenty flagged values under one type-ahead question
  When they open the review-type-ahead-values page
  And they scroll to "Site 15"
  And they <action> "Site 15"
  Then the page never shows the loading text
  And the scroll position is unchanged

Examples:
  | action  |
  | approve |
  | remove  |
  | correct |
  | merge   |
  | relink  |

@REQ-MOD-163
@ui
Scenario: A merged value leaves the queue in place, and a merge target still awaiting review shows its updated answer count
  Given a Safety Officer and two flagged values of the same question, one also awaiting review in its own right
  When they open the review-type-ahead-values page
  And they merge "Coopers" into "Cooper's"
  Then "Coopers" leaves the list, and the rows around it stay where they are
  And "Cooper's" is still listed, showing 5 answers naming it

# Re-translating a type-ahead value's wording while correcting it (ADR-0141,
# ADR-0144): the correction view reuses the question editor's choice
# affordance, scoped to the one value being corrected. No new API.

@REQ-MOD-166
@ui
Scenario: A value written in both languages offers Translate only once its wording differs from what correction opened with
  Given a Safety Officer and three type-ahead values flagged for review
  When they open the review-type-ahead-values page
  And they begin correcting "coopers"
  And they write its French wording as "Coopers (fr)"
  Then that value's Translate action is unavailable
  When they edit its English wording to "Cooper's"
  Then that value's Translate action becomes available

@REQ-MOD-167
@ui
Scenario: A value's Translate is unavailable after it translates, until its source is edited again
  Given a Safety Officer and three type-ahead values flagged for review
  When they open the review-type-ahead-values page
  And they begin correcting "coopers", edit its English wording to "Cooper's", and press Translate
  Then that value's Translate action is unavailable
  When they edit that value's English wording again
  Then that value's Translate action becomes available

@REQ-MOD-168
@ui
Scenario: Pressing Translate drafts the other language, still editable, and saves nothing by itself
  Given a Safety Officer and three type-ahead values flagged for review
  When they open the review-type-ahead-values page
  And they begin correcting "coopers", edit its English wording to "Cooper's", and press Translate
  Then that value's French field is filled with the translation and remains editable
  And nothing is saved until they press Save correction

@REQ-MOD-169
@ui
Scenario: The direction switch changes which language Translate reads from
  Given a Safety Officer and three type-ahead values flagged for review
  When they open the review-type-ahead-values page
  And they begin correcting "Test site"
  Then that value's direction switch translates English to French
  When they flip that value's direction switch to French to English
  And they write its French wording as "Site d'essai" and press Translate
  Then that value's English field is filled with the translation

@REQ-MOD-170
@ui
Scenario: Translate is unavailable when the server has no translation provider
  Given a Safety Officer and three type-ahead values flagged for review, on a server with no translation provider
  When they open the review-type-ahead-values page
  And they begin correcting "coopers"
  Then that value's Translate action is unavailable and says why

@REQ-MOD-171
@ui
Scenario: A failed translation says so on the value's row and drafts nothing
  Given a Safety Officer and three type-ahead values flagged for review, on a server whose translation fails
  When they open the review-type-ahead-values page
  And they begin correcting "coopers", edit its English wording to "Cooper's", and press Translate
  Then that value's row says the translation failed
  And that value's French field still reads ""
  And that value's Translate action becomes available

@REQ-MOD-172
@ui
Scenario: A translation overtaken by a direction flip is dropped, and Translate stops showing as working
  Given a Safety Officer and three type-ahead values flagged for review, on a server whose translation answers only when released
  When they open the review-type-ahead-values page
  And they begin correcting "coopers", edit its English wording to "Cooper's", and press Translate
  And they flip that value's direction switch while the translation is still out
  And the translation then answers
  Then its answer is dropped and Translate is no longer shown as working
