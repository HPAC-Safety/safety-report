@xunit:collection(QuestionBankRunsAlone)
Feature: Question translation
An Administrator may draft one language of a question's wording or of a
choice from the other by machine translation. A draft is only a draft: a
person saves both languages.

Background:
  Given the question bank stores each question as a stable, non-localized key
  And each revision has a monotonically increasing revision number for its key
  And at most one live question exists for a question key

@REQ-QB-066
Scenario: A translation draft comes from the server and is saved only by a person
  Given an Administrator is authoring a question in one official language
  When they ask for the other language to be translated
  Then the browser sends the request to the application's own server, never to a provider
  And the translated text is returned as a draft that is not saved anywhere
  And the reviewer-only translate request is the only server code that calls a translator
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
  Given an Administrator is authoring a new question
  When they write the English wording and ask for its translation
  Then the French wording is filled with the translation
  And the French wording remains editable

@REQ-QTR-001
@ui
Scenario: A new question's wording translates English to French at first
  Given an Administrator is authoring a new question
  Then the wording's direction switch translates English to French

@REQ-QB-070
@ui
Scenario: An Administrator drafts the English from the French
  Given an Administrator is authoring a new question
  And they flipped the wording's direction switch to French to English
  When they write the French wording and ask for its translation
  Then the English wording is filled with the translation

@REQ-QB-071
@ui
Scenario: A question cannot be saved in one language
  Given an Administrator is authoring a new question
  When only one official language has been written
  Then saving is unavailable

@REQ-QTR-002
@ui
Scenario: A question written in both languages can be saved
  Given an Administrator is authoring a new question
  And only one official language has been written
  When the other language is written as well
  Then saving becomes available

@REQ-QB-072
@ui
Scenario: Translation is not offered when the server has no provider
  Given an Administrator is authoring a question on a server with no translation provider
  Then the Translate action is unavailable and says so

@REQ-QB-172
@ui
Scenario: Editing a bilingual question's wording offers Translate
  Given an Administrator is editing a question whose wording is in both languages
  When they edit its English help text
  Then the wording's Translate action becomes available

@REQ-QTR-003
@ui
Scenario: An unedited bilingual question's wording offers no Translate
  Given an Administrator is editing a question whose wording is in both languages
  Then the wording's Translate action is unavailable

@REQ-QB-173
@ui
Scenario: Translate replaces the French wording with drafts
  Given an Administrator is editing a question whose wording is in both languages
  When they edit its English question and help text and ask for their translation
  Then the French question and help text are replaced with their translations
  And the drafts are saved only when they save the question

@REQ-QB-174
@ui
Scenario: The wording's Translate is unavailable after it translates
  Given an Administrator is editing a question whose wording is in both languages
  When they edit its English help text and ask for its translation
  Then the wording's Translate action is unavailable

@REQ-QTR-004
@ui
Scenario: The wording's Translate is offered again once its source wording is edited again
  Given an Administrator is editing a question whose wording is in both languages
  And they edited its English help text and asked for its translation
  When they edit its English question
  Then the wording's Translate action becomes available

@REQ-QB-175
@ui
Scenario: Translating the wording changes no choice
  Given an Administrator is editing a type-ahead question with a choice written only in English
  When they edit its English help text and ask for its translation
  Then only the wording is sent to be translated
  And every choice keeps its wording

@REQ-QB-176
@ui
Scenario: Translate leaves unedited wording written in both languages as it is
  Given an Administrator is editing a question whose wording is in both languages
  When they edit its English help text and ask for its translation
  Then only the English help text is sent to be translated
  And the French question keeps its wording

@REQ-QB-177
@ui
Scenario: A translation that arrives after the direction was flipped changes nothing
  Given an Administrator is editing a question whose wording is in both languages
  And the translation provider is slow to answer
  And they edited its English help text and asked for its translation
  And they flipped the wording's direction switch before the translation arrived
  When the translation arrives
  Then the French help text keeps its wording

@REQ-QB-178
@ui
Scenario: French written while a translation is on its way is kept
  Given an Administrator is editing a question whose wording is in both languages
  And the translation provider is slow to answer
  And they edited its English help text and asked for its translation
  And they wrote the French help text themselves before the translation arrived
  When the translation arrives
  Then the French help text is what they wrote

@REQ-QB-164
@ui
Scenario: A choice written in both languages offers Translate once it is edited
  Given an Administrator is editing a single-select question whose choices are written in both languages
  When they edit the English wording of one choice
  Then that choice's Translate action becomes available
  And every other choice's Translate action stays unavailable

@REQ-QTR-005
@ui
Scenario: No unedited choice written in both languages offers Translate
  Given an Administrator is editing a single-select question whose choices are written in both languages
  Then no choice's Translate action is available

@REQ-QB-165
@ui
Scenario Outline: A choice's English is translated into its French as a draft
  Given an Administrator is authoring a new <type> question worded in both languages
  When they add a choice written in English and ask for its translation
  Then that choice's French wording is filled with the translation
  And that choice's French wording remains editable
  And nothing is saved until they save the question

  Examples:
    | type          |
    | single-select |
    | multi-select  |
    | type-ahead    |

@REQ-QB-166
@ui
Scenario: Flipping the direction translates a choice's French into its English
  Given an Administrator is authoring a new type-ahead question worded in both languages
  And they flipped the choices' direction switch
  When they add a choice written in French and ask for its translation
  Then that choice's English wording is filled with the translation

@REQ-QTR-006
@ui
Scenario: The choices' direction switch translates English to French at first
  Given an Administrator is authoring a new type-ahead question worded in both languages
  Then the direction switch translates English to French

@REQ-QTR-007
@ui
Scenario: Flipping the choices' direction switch says the new direction
  Given an Administrator is authoring a new type-ahead question worded in both languages
  When they flip the direction switch
  Then the direction switch translates French to English, and says so

@REQ-QB-167
@ui
Scenario: Translating one choice changes no other choice
  Given an Administrator is editing a single-select question whose choices are written in both languages
  When they edit the English wording of one choice and ask for its translation
  Then only that choice's wording is sent to be translated
  And only that choice's French wording changes

@REQ-QB-168
@ui
Scenario: A choice's Translate is unavailable after it translates
  Given an Administrator is authoring a new type-ahead question worded in both languages
  When they add a choice written in English and ask for its translation
  Then that choice's Translate action is unavailable

@REQ-QTR-008
@ui
Scenario: A translated choice offers Translate again once its source is edited again
  Given an Administrator is authoring a new type-ahead question worded in both languages
  And they added a choice written in English and asked for its translation
  When they edit that choice's English wording again
  Then that choice's Translate action becomes available

@REQ-QB-169
@ui
Scenario: No choice's Translate is offered when the server has no provider
  Given an Administrator is editing a type-ahead question with choices on a server with no translation provider
  When they add a choice written in English
  Then every choice's Translate action is unavailable and says why

@REQ-QB-170
@ui
Scenario: A choice written in one language can be translated without being edited
  Given an Administrator is editing a type-ahead question with a choice written only in English
  When they ask for that choice's translation
  Then that choice's French wording is filled with the translation of its English

@REQ-QTR-009
@ui
Scenario: Before any edit, only a choice written in one language offers Translate
  Given an Administrator is editing a type-ahead question with a choice written only in English
  Then that choice's Translate action is available
  And the choice written in both languages offers no Translate action
