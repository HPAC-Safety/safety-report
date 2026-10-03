@xunit:collection(QuestionBankRunsAlone)
Feature: Question translation
An Administrator may draft one language of a question's wording or of a
choice from the other by machine translation. A draft is only a draft: a
person saves both languages.

Background:
  Given the question bank stores each question as a stable, non-localized key
  And each revision has a monotonically increasing revision number for its key
  And at most one live question exists for a stable key

@REQ-QB-066
Scenario: A translation draft comes from the API and is saved only by a person
  Given an Administrator is authoring a question in one official language
  When they ask for the other language to be translated
  Then the request goes to the application's own API rather than to a provider from the browser
  And the translated text is returned as a draft that is not saved anywhere
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
  Then the wording's direction switch translates English to French
  When they write the English wording and press Translate
  Then the French field is filled with the translation
  And the French field remains editable

@REQ-QB-070
@ui
Scenario: An Administrator drafts the English from the French
  Given a signed-in Administrator is authoring a new question
  When they flip the wording's direction switch to French to English
  And they write the French wording and press Translate
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

@REQ-QB-172
@ui
Scenario: Editing a bilingual question's wording offers Translate
  Given a signed-in Administrator is editing a question whose wording is in both languages
  Then the wording's Translate action is unavailable
  When they edit its English help text
  Then the wording's Translate action becomes available

@REQ-QB-173
@ui
Scenario: Translate replaces the French wording with drafts
  Given a signed-in Administrator is editing a question whose wording is in both languages
  When they edit its English question and help text and press Translate
  Then the French question and help text are replaced with their translations
  And the drafts are saved only when they press Save

@REQ-QB-174
@ui
Scenario: The wording's Translate is unavailable after it translates, until a source field is edited again
  Given a signed-in Administrator is editing a question whose wording is in both languages
  When they edit its English help text and press Translate
  Then the wording's Translate action is unavailable
  When they edit its English question
  Then the wording's Translate action becomes available

@REQ-QB-175
@ui
Scenario: Translating the wording changes no choice
  Given a signed-in Administrator is editing a type-ahead question with a choice written only in English
  When they edit its English help text and press Translate
  Then only the wording is sent to be translated
  And every choice keeps its wording

@REQ-QB-176
@ui
Scenario: Translate leaves an unedited field written in both languages as it is
  Given a signed-in Administrator is editing a question whose wording is in both languages
  When they edit its English help text and press Translate
  Then only the English help text is sent to be translated
  And the French question keeps its wording

@REQ-QB-177
@ui
Scenario: A translation that arrives after the direction was flipped changes nothing
  Given a signed-in Administrator is editing a question whose wording is in both languages
  And the translation provider is slow to answer
  When they edit its English help text and press Translate
  And they flip the wording's direction switch before the translation arrives
  And the translation arrives
  Then the French help text keeps its wording

@REQ-QB-178
@ui
Scenario: French typed while a translation is on its way is kept
  Given a signed-in Administrator is editing a question whose wording is in both languages
  And the translation provider is slow to answer
  When they edit its English help text and press Translate
  And they type the French help text themselves before the translation arrives
  And the translation arrives
  Then the French help text is what they typed

@REQ-QB-164
@ui
Scenario: A choice written in both languages offers Translate only once it is edited
  Given a signed-in Administrator is editing a single-select question whose choices are written in both languages
  Then no choice's Translate action is available
  When they edit the English wording of one choice
  Then that choice's Translate action becomes available
  And every other choice's Translate action stays unavailable

@REQ-QB-165
@ui
Scenario Outline: A choice's English is translated into its French as a draft
  Given a signed-in Administrator is authoring a new <type> question worded in both languages
  When they add a choice written in English and press its Translate action
  Then that choice's French field is filled with the translation
  And that choice's French field remains editable
  And nothing is saved until they press Save

  Examples:
    | type          |
    | single-select |
    | multi-select  |
    | type-ahead    |

@REQ-QB-166
@ui
Scenario: Flipping the direction translates a choice's French into its English
  Given a signed-in Administrator is authoring a new type-ahead question worded in both languages
  Then the direction switch translates English to French
  When they flip the direction switch
  Then the direction switch translates French to English, and says so
  When they add a choice written in French and press its Translate action
  Then that choice's English field is filled with the translation

@REQ-QB-167
@ui
Scenario: Translating one choice changes no other choice
  Given a signed-in Administrator is editing a single-select question whose choices are written in both languages
  When they edit the English wording of one choice and press its Translate action
  Then only that choice's wording is sent to be translated
  And only that choice's French field changes

@REQ-QB-168
@ui
Scenario: A choice's Translate is unavailable after it translates, until its source is edited again
  Given a signed-in Administrator is authoring a new type-ahead question worded in both languages
  When they add a choice written in English and press its Translate action
  Then that choice's Translate action is unavailable
  When they edit that choice's English wording again
  Then that choice's Translate action becomes available

@REQ-QB-169
@ui
Scenario: No choice's Translate is offered when the server has no provider
  Given a signed-in Administrator is editing a type-ahead question with choices on a server with no translation provider
  When they add a choice written in English
  Then every choice's Translate action is unavailable and says why

@REQ-QB-170
@ui
Scenario: A choice written in one language can be translated without being edited
  Given a signed-in Administrator is editing a type-ahead question with a choice written only in English
  Then that choice's Translate action is available
  And the choice written in both languages offers no Translate action
  When they press that choice's Translate action
  Then that choice's French field is filled with the translation of its English
