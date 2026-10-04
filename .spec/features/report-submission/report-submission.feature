Feature: Report submission
A reporter's answers, revision IDs, and locale live only in the browser
until one final submission request. Before that request, the server and database
receive no unfinished report state. The one thing that reaches the server
earlier is an attachment, sent straight into private quarantine through a
pre-signed upload link minted as soon as it is attached, and validated and
claimed by that request (ADR-0096, ADR-0126). The browser's saved report
names its finished uploads, so they are kept exactly as long as it is
(ADR-0100).

Background:
  Given a reporter sends a report in one final request, each attachment having gone ahead of it straight to storage through a pre-signed upload link
  And both require a valid member bearer token
  And the report names each attachment by the upload ID its upload returned
  And the bearer token is transport/security metadata, not persisted report content

@REQ-SUB-001
@ui
Scenario: The browser holds report state locally until submission
  Given a reporter is filling out the form
  When the reporter has not yet submitted
  Then the selected locale, shown question-revision IDs, and entered answers exist only in local browser storage with a 15-day expiry
  And no image, video, or document file is placed in browser storage, only each finished upload's ID, name, and size
  And no server draft, report ID reservation, or resumable upload protocol exists

@REQ-SUB-002
@ui
Scenario: A successful submission clears local browser state
  Given a reporter has entered answers in local browser storage
  When the final submission request succeeds
  Then the browser clears that local state

@REQ-SUB-003
@ui
Scenario: Expired local state is not restored
  Given local browser state was started more than 15 days ago and saved again since
  When the reporter returns to the form
  Then the browser ignores or removes the expired state

@REQ-SUB-035
@ui
Scenario: A returning reporter is asked whether to continue their saved report
  Given this browser holds an unexpired saved report
  When the reporter returns to the form
  Then a dialog asks whether to continue where they left off, with No and Yes buttons
  And a table below the buttons lists each saved question with its saved answer
  And each saved attached file is listed by name under its question

@REQ-SUB-068
@ui
Scenario Outline: The continue dialog shows a saved date or time in the reporter's language
  Given this browser holds an unexpired saved report with a date answer "2026-09-13" and a time answer "14:30"
  And the interface language is <language>
  When the reporter returns to the form
  Then the continue dialog lists the date as "<date>" and the time as "<time>"

Examples:
  | language | date               | time      |
  | English  | September 13, 2026 | 2:30 p.m. |
  | French   | 13 septembre 2026  | 14 h 30   |

@REQ-SUB-036
@ui
Scenario: Continuing a saved report restores it where the reporter left off
  Given this browser holds an unexpired saved report
  When the reporter returns to the form and continues the saved report
  Then the form opens on the page the reporter was last on
  And the saved answers are restored

@REQ-SUB-037
@ui
Scenario: Declining a saved report starts a fresh form
  Given this browser holds an unexpired saved report
  When the reporter returns to the form and declines to continue
  Then the browser removes the saved report
  And the form opens at its introduction with no answers

@REQ-SUB-038
@ui
Scenario: A reporter with no saved report is not asked
  Given this browser holds no saved report
  When the reporter returns to the form
  Then no dialog asks whether to continue

@REQ-SUB-124
@ui
Scenario: Continuing a saved report leaves out an answer whose question revision is no longer current
  Given this browser holds an unexpired saved report, and an Administrator has since revised one of its answered questions
  When the reporter returns to the form and continues the saved report
  Then the saved answers to the other questions are restored
  And the revised question is empty

@REQ-SUB-125
@ui
Scenario: The reporter is told once that saved answers were cleared
  Given this browser holds an unexpired saved report, and an Administrator has since revised one of its answered questions
  When the reporter returns to the form and continues the saved report
  Then one notice says the form changed since the report was saved, so some answers were cleared
  And no question is marked individually

@REQ-SUB-126
@ui
Scenario: No notice appears when every saved answer is still current
  Given this browser holds an unexpired saved report whose every answer is still current
  When the reporter returns to the form and continues the saved report
  Then no notice says answers were cleared

@REQ-SUB-127
@ui
Scenario: A saved report with no answer still current is replaced by a fresh form and the notice
  Given this browser holds an unexpired saved report whose every answer names a revision that is no longer current
  When the reporter returns to the form
  Then no dialog asks whether to continue
  And the browser removes the saved report
  And one notice says the form changed since the report was saved, so some answers were cleared

@REQ-SUB-053
@ui
Scenario: Each page of the form has its own address
  Given a reporter is on the form's introduction, at the form's own address
  When the reporter goes on to the next page
  Then the address names the page now shown by its question key

@REQ-SUB-137
@ui
Scenario: Going back to the introduction returns to the form's own address
  Given a reporter has gone on from the form's introduction to the next page
  When the reporter goes back a page
  Then the address is the form's own address

@REQ-SUB-054
@ui
Scenario: The browser's Back button returns to the previous page of the form
  Given a reporter has answered a required question and gone on to the next page
  When the reporter goes back with the browser's Back button
  Then the required question's page shows and the address names it

@REQ-SUB-138
@ui
Scenario: The browser's Forward button cannot skip a required question left unanswered
  Given a reporter has answered a required question, gone on, and come back with the browser's Back button
  When the reporter clears the answer and goes forward with the browser's Forward button
  Then the required question's page still shows
  And an inline, localized message explains that an answer is required

@REQ-SUB-055
@ui
Scenario: Continuing a saved report puts its page in the address
  Given this browser holds an unexpired saved report
  When the reporter returns to the form and continues the saved report
  Then the address names the page the reporter was last on

@REQ-SUB-056
@ui
Scenario: A page address never answers the continue question for the reporter
  Given this browser holds an unexpired saved report
  When the reporter opens the address of a page other than the one saved
  Then a dialog asks whether to continue where they left off, with No and Yes buttons

@REQ-SUB-139
@ui
Scenario: Declining to continue from a page address starts a fresh form at the form's own address
  Given this browser holds an unexpired saved report, and the reporter has opened the address of a page other than the one saved
  When the reporter declines to continue
  Then the address is the form's own address
  And the form opens at its introduction with no answers

@REQ-SUB-057
@ui
Scenario: A page address without a saved report opens the introduction
  Given this browser holds no saved report
  When the reporter opens the address of a later page of the form
  Then the form opens at its introduction, at the form's own address

@REQ-SUB-140
@ui
Scenario: The address of a page the form does not have opens the introduction
  Given this browser holds no saved report
  When the reporter opens the address of a page the form does not have
  Then the form opens at its introduction, at the form's own address

@REQ-SUB-028
@ui
Scenario: The leading statement question renders as an introduction
  Given the current form's first question is a live statement
  When a reporter opens the report page
  Then the statement renders with a Next control and no Back control
  And no answer is collected for it

@REQ-SUB-029
@ui
Scenario: A reporter pages through questions one at a time
  Given the current form has more than one answer-producing question
  When a reporter goes on to the next page
  Then exactly one question, or one group and its children, is shown per page
  And a Back control returns to the previous page without losing its answer

@REQ-SUB-030
@ui
Scenario: A group question and its children page together
  Given a group question has children grouped under it
  When a reporter reaches that group's page
  Then the group heading and every child render together on one page
  And advancing counts that page as a single step

@REQ-SUB-031
@ui
Scenario: A required question blocks Next until answered
  Given the current page shows a required, unanswered question
  When a reporter goes on to the next page
  Then the page does not advance
  And an inline, localized message explains that an answer is required

@REQ-SUB-032
@ui
Scenario: A conditional question is absent from paging until its parent condition is met
  Given a question depends on a yes/no or single-select question
  When the parent's current answer does not meet the condition
  Then the dependent question's page is skipped entirely

@REQ-SUB-141
@ui
Scenario: A conditional question joins the paging once its parent condition is met
  Given a question depends on a yes/no or single-select question
  And the parent's current answer does not meet the condition
  When the reporter answers the parent so the condition is met
  Then the dependent question's page appears in the sequence

@REQ-SUB-033
@ui
Scenario: The Next button becomes Submit on the final page
  Given a reporter has reached the last page of the form
  Then the control that was Next now reads Submit
  And choosing it sends the one final submission request

@REQ-SUB-034
@ui
Scenario: A multi-select question is one closed picker, not a flat list
  Given the current page shows a multi-select question
  Then its choices are hidden behind one closed picker labelled by the question

@REQ-SUB-142
@ui
Scenario: A multi-select picker stays open while the reporter checks choices
  Given the current page shows a multi-select question
  When the reporter opens the picker and checks two choices
  Then the picker stays open with both choices checked

@REQ-SUB-143
@ui
Scenario Outline: Closing a multi-select picker from the keyboard returns focus to it and names the checked choices
  Given the reporter has checked two choices in an open multi-select picker
  When the reporter uses the <key> key
  Then the picker closes, returns focus to itself, and names both choices

Examples:
  | key    |
  | Escape |

@REQ-SUB-132
@ui
Scenario: A closed multi-select picker tells assistive technology it opens a dialog
  Given the current page shows a multi-select question
  Then assistive technology hears its closed picker as collapsed, labelled by the question, with a dialog as its popup

@REQ-SUB-144
@ui
Scenario: An open multi-select picker controls a dialog of checkboxes
  Given the current page shows a multi-select question
  When the reporter opens the picker
  Then the picker is expanded and controls a dialog labelled by the question, holding one checkbox for each choice

@REQ-SUB-085
@ui
Scenario Outline: An email or phone question opens the matching keyboard
  Given the current page shows an optional <type> question
  Then its input has type "<input type>", input mode "<input mode>", and autocomplete "<autocomplete>"

Examples:
  | type  | input type | input mode | autocomplete |
  | email | email      | email      | email        |
  | phone | tel        | tel        | tel          |

@REQ-SUB-086
@ui
Scenario Outline: An optional email or phone question may be left blank
  Given the current page shows an optional <type> question
  When the reporter leaves it blank and goes on
  Then the next page shows
  And the <type> answer is sent as null when the report is submitted

Examples:
  | type  |
  | email |
  | phone |

@REQ-SUB-087
@ui
Scenario: A malformed email address holds the reporter on its page
  Given the current page shows an optional email question
  When the reporter enters "chase.florell@example" and goes on
  Then the reporter stays on that page
  And an inline, localized message asks for an email address like name@example.com

@REQ-SUB-088
@ui
Scenario: A phone number that is not valid for its country holds the reporter on its page
  Given the current page shows an optional phone question
  When the reporter enters "5551234" and goes on
  Then the reporter stays on that page
  And an inline, localized message asks for a phone number valid for the chosen country

@REQ-SUB-089
@ui
Scenario: The phone country picker starts on Canada
  Given the current page shows an optional phone question
  Then its country picker shows "🇨🇦 +1"
  And the phone question's placeholder is "(555) 555-5555"

@REQ-SUB-090
@ui
Scenario Outline: Choosing a phone country shows its calling code and number format
  Given the current page shows an optional phone question
  When the reporter chooses <country> in its country picker
  Then its country picker shows "<shown>"
  And the phone question's placeholder is "<placeholder>"

Examples:
  | country        | shown   | placeholder    |
  | Canada         | 🇨🇦 +1  | (555) 555-5555 |
  | United Kingdom | 🇬🇧 +44 | 5555 555555    |
  | France         | 🇫🇷 +33 | 5 55 55 55 55  |

@REQ-SUB-145
@ui
Scenario Outline: A phone number takes its chosen country's mask as it is entered
  Given the current page shows an optional phone question with <country> chosen in its country picker
  When the reporter enters "<digits>"
  Then the phone question reads "<masked>"

Examples:
  | country        | digits     | masked         |
  | Canada         | 6045551234 | (604) 555-1234 |
  | United Kingdom | 2079460018 | 20 7946 0018   |
  | France         | 612345678  | 6 12 34 56 78  |

@REQ-SUB-091
@ui
Scenario Outline: A phone answer is sent in E.164
  Given the current page shows an optional phone question with <country> chosen in its country picker
  And the reporter has entered "<digits>"
  When the reporter goes on to submit the report
  Then the phone answer is sent as "<sent>"

Examples:
  | country        | digits     | sent          |
  | Canada         | 6045551234 | +16045551234  |
  | United Kingdom | 2079460018 | +442079460018 |

@REQ-SUB-092
@ui
Scenario: Before "@" is typed, every suggested domain is offered for what has been typed
  Given the current page shows an optional email question
  When the reporter enters "chas"
  Then the question offers a suggestion list labelled "Suggested email addresses"
  And the suggestions below it are, in order:
    | chas@gmail.com   |
    | chas@yahoo.com   |
    | chas@hotmail.com |
    | chas@outlook.com |
    | chas@icloud.com  |
    | chas@mail.com    |

@REQ-SUB-146
@ui
Scenario: The email suggestions follow each further letter typed
  Given the current page shows an optional email question
  And the reporter has entered "chas"
  When the reporter enters "e"
  Then the suggestions below it are, in order:
    | chase@gmail.com   |
    | chase@yahoo.com   |
    | chase@hotmail.com |
    | chase@outlook.com |
    | chase@icloud.com  |
    | chase@mail.com    |

@REQ-SUB-093
@ui
Scenario: After "@", the suggestions narrow to the domains beginning with what follows it
  Given the current page shows an optional email question
  When the reporter enters "chase.florell@g"
  Then the suggestions below it are, in order:
    | chase.florell@gmail.com |

@REQ-SUB-094
@ui
Scenario Outline: Choosing a suggestion fills the question
  Given the current page shows an optional email question
  And the reporter has entered "chase.florell@h"
  When the reporter chooses "chase.florell@hotmail.com" <how>
  Then the email question reads "chase.florell@hotmail.com"
  And no suggestions are shown

Examples:
  | how               |
  | with the keyboard |
  | with the pointer  |

@REQ-SUB-095
@ui
Scenario: An address at a domain outside the suggestions is accepted
  Given the current page shows an optional email question
  When the reporter enters "pilot@example.ca" and goes on
  Then the next page shows

@REQ-SUB-098
@ui
Scenario Outline: On a desktop, reaching a date question opens a one-month calendar under it
  Given the current page shows a date question that does not allow future dates, on a desktop
  When the reporter reaches the date question <how>
  Then a calendar labelled "Choose a date" opens under the question, showing today's month
  And today is marked in it
  And it has "Previous month" and "Next month" buttons

Examples:
  | how               |
  | with the pointer  |
  | from the keyboard |

@REQ-SUB-099
@ui
Scenario: Choosing a day fills the question as yyyy-mm-dd and closes the calendar
  Given the current page shows a date question that does not allow future dates, on a desktop
  When the reporter chooses the 1st of today's month in the date question's calendar
  Then the date question reads the 1st of today's month as yyyy-mm-dd
  And the calendar closes
  And the chosen day is announced in words

@REQ-SUB-147
@ui
Scenario: Choosing the same day again announces it again
  Given the current page shows a date question that does not allow future dates, on a desktop
  And the reporter has chosen the 1st of today's month in the date question's calendar
  When the reporter chooses the 1st of today's month in the date question's calendar again
  Then the announcement is cleared and the chosen day is announced again

@REQ-SUB-112
@ui
Scenario: A date question with a placeholder of its own still names the yyyy-mm-dd format
  Given the current page shows a date question whose placeholder is "When did it happen?", on a desktop
  Then the date question's placeholder is "When did it happen?"
  And the date question is described by the format "yyyy-mm-dd"

@REQ-SUB-100
@ui
Scenario Outline: The calendar disables the days after today unless the question allows future dates
  Given the current page shows a date question that <allows> future dates, on a desktop
  When the reporter opens the date question's calendar
  Then the calendar shows today's month
  And every day after today is <state>

Examples:
  | allows         | state    |
  | does not allow | disabled |
  | allows         | offered  |

@REQ-SUB-101
@ui
Scenario Outline: A typed date that is malformed, or in the future where not allowed, holds the reporter on its page
  Given the current page shows a date question that does not allow future dates, on a desktop
  When the reporter enters "<typed>" in the date question and goes on
  Then the reporter stays on the date page
  And an inline message says "<message>"

Examples:
  | typed       | message                                        |
  | 2026/9/21   | Enter a date as yyyy-mm-dd, such as 2026-09-21. |
  | Sep 21 2026 | Enter a date as yyyy-mm-dd, such as 2026-09-21. |
  | 2026-9-21   | Enter a date as yyyy-mm-dd, such as 2026-09-21. |
  | 2026-02-30  | Enter a date as yyyy-mm-dd, such as 2026-09-21. |
  | 9999-12-31  | Choose a date that is not in the future.        |

@REQ-SUB-102
@ui
Scenario: A date typed as yyyy-mm-dd is sent as typed
  Given the current page shows a date question that does not allow future dates, on a desktop
  When the reporter enters "2024-02-29" in the date question and submits the report from the next page
  Then the date answer is sent as "2024-02-29"

@REQ-SUB-103
@ui
Scenario Outline: The calendar is in the reader's language
  Given the current page shows a date question in <language>, on a desktop
  When the reporter opens the date question's calendar
  Then the calendar names today's month in <language>
  And its weekday headings start on <first day>
  And its buttons and pickers are labelled from the <language> catalogue

Examples:
  | language | first day |
  | English  | Sunday    |
  | French   | Monday    |

@REQ-SUB-104
@ui
Scenario Outline: The calendar moves its focused day from the keyboard
  Given the current page shows a date question that allows future dates, on a desktop
  And the keyboard focus is on <from>
  When the reporter uses the <key> key
  Then <to> has focus in the calendar

Examples:
  | from                               | key        | to                                 |
  | the date question                  | ArrowDown  | today                              |
  | today                              | ArrowLeft  | the day 1 day before today         |
  | the day 1 day before today         | ArrowUp    | the day 8 days before today        |
  | the day 8 days before today        | ArrowDown  | the day 1 day before today         |
  | the day 1 day before today         | ArrowRight | today                              |
  | today                              | PageUp     | the same day of the previous month |
  | the same day of the previous month | PageDown   | today                              |

@REQ-SUB-148
@ui
Scenario Outline: Choosing the focused day from the keyboard fills the question and closes the calendar
  Given the current page shows a date question that allows future dates, on a desktop
  And the keyboard focus is on today
  When the reporter uses the <key> key
  Then the date question reads today as yyyy-mm-dd
  And the calendar closes
  And focus is on the date question

Examples:
  | key   |
  | Enter |

@REQ-SUB-149
@ui
Scenario Outline: Closing the calendar from the keyboard returns focus to the date question
  Given the current page shows a date question that <allows> future dates, on a desktop
  And the date question's calendar has been opened <how>, and the keyboard focus is on today
  When the reporter uses the <key> key
  Then the calendar closes
  And focus is on the date question

Examples:
  | allows         | how                                                                        | key    |
  | allows         | from the keyboard after a day was chosen from it                           | Escape |
  | does not allow | from the keyboard after the pointer opened it and activated its background | Escape |

@REQ-SUB-111
@ui
Scenario Outline: Leaving a date question from the keyboard skips its calendar
  Given the current page shows a date question that does not allow future dates, on a desktop
  And the reporter has reached the date question from the keyboard
  When the reporter uses the <key> key
  Then focus skips the calendar to the Next button, and the calendar closes

Examples:
  | key |
  | Tab |

@REQ-SUB-131
@ui
Scenario: A closed desktop date question tells assistive technology it opens a calendar dialog
  Given the current page shows a date question that does not allow future dates, on a desktop
  Then assistive technology hears the date question as collapsed, with a dialog as its popup

@REQ-SUB-150
@ui
Scenario: An open desktop date question controls its calendar dialog
  Given the current page shows a date question that does not allow future dates, on a desktop
  When the reporter opens the date question's calendar
  Then the date question is expanded and controls the dialog labelled "Choose a date"

@REQ-SUB-151
@ui
Scenario: Activating the calendar's background keeps it open and keeps focus on the date question
  Given the current page shows a date question that does not allow future dates, on a desktop
  And the reporter has opened the date question's calendar
  When the reporter activates the calendar's background with the pointer
  Then the calendar stays open and focus is on the date question

@REQ-SUB-105
@ui
Scenario: The reporter jumps to a month and year a few years back
  Given the current page shows a date question that does not allow future dates, on a desktop
  And the reporter has opened the date question's calendar
  When the reporter chooses March in the calendar's month picker and 2023 in its year picker
  Then the calendar shows March 2023

@REQ-SUB-152
@ui
Scenario: Choosing a day in a month jumped to fills the question
  Given the current page shows a date question that does not allow future dates, on a desktop
  And the reporter has opened the date question's calendar at March 2023
  When the reporter chooses the 14th
  Then the date question reads "2023-03-14"

@REQ-SUB-106
@ui
Scenario Outline: On a touch device, a date question uses the device's own date picker
  Given the current page shows a date question that <allows> future dates, on a touch device
  Then the date question is a native date input <limit>
  And opening it on the touch device shows no calendar of the form's own

Examples:
  | allows         | limit                      |
  | does not allow | whose latest date is today |
  | allows         | with no latest date        |

@REQ-SUB-107
@ui
Scenario: On a touch device, a future date the device's picker lets through still holds the reporter on its page
  Given the current page shows a date question that does not allow future dates, on a touch device
  When the device's picker sets the date question to "9999-12-31" and the reporter goes on
  Then the reporter stays on the date page
  And an inline message says "Choose a date that is not in the future."

@REQ-SUB-078
Scenario: One answer entry per shown answer-producing revision
  Given the client says it showed the reporter a set of answer-producing revisions
  When the reporter submits the form
  Then the submission contains exactly one answer entry for each of those revisions
  And a single-select, multi-select, or type-ahead answer carries the identifiers of the chosen choices in "choices"
  And a type-ahead answer naming a value the question does not offer carries the typed text in "value" instead
  And every other answer uses "value", a single string, alongside the locale it was given in
  And file-upload answers additionally carry one attachment entry per file attached to that question, each an upload ID and the file's name
  And the other answer shapes are null

@REQ-SUB-077
@ui
Scenario Outline: A yes or no is sent as true or false whatever language the report is submitted in
  Given a signed-in reporter answers a yes/no question and publication consent in <answered in>
  And the reporter switches the form to <submitted in> before submitting
  When the reporter submits the report
  Then the yes/no answer is sent as false and the consent answer as true

Examples:
  | answered in | submitted in |
  | English     | English      |
  | French      | French       |
  | English     | French       |
  | French      | English      |

@REQ-SUB-083
@ui
Scenario: The form names each chosen choice by its identifier
  Given a signed-in reporter picks a wing type, checks two conditions, and types a launch site the form does not offer
  When the reporter sends the report
  Then the wing type and both conditions are sent as their choices' identifiers
  And the launch site is sent as the words typed

@REQ-SUB-005
Scenario: A skipped answer is represented by an empty value, not omission
  Given a reporter skips an answer-producing question
  When the submission is built
  Then a skipped answer of any type has a null value
  And a skipped file upload has an empty attachments list

@REQ-SUB-079
Scenario: A submitted choice must be one the question offers
  Given a reporter submits a single-select, multi-select, or type-ahead answer naming choices by identifier
  When the submission is validated
  Then the answer is accepted only if every named choice is a live choice of that question
  And a removed choice, or another question's choice, is refused
  And only a type-ahead also accepts typed text naming a value it does not yet offer

@REQ-SUB-113
Scenario Outline: A choice of a dependent question must be offered under the parent's answer
  Given the "Model" question's choices depend on the "Make" question, and "Mentor 7" is offered under "Niviuk"
  When a reporter submits "Model" answered with "Mentor 7" and "Make" <make>
  Then the report is refused as invalid, naming "model" and "make" by key
  And no report, answer, or choice is written

Examples:
  | make                   |
  | answered with "Ozone"  |
  | left unanswered        |

@REQ-SUB-114
Scenario Outline: A required dependent question that cannot be answered yet does not block a submission
  Given a required single-select "Model" question's choices depend on the "Make" question, and nothing is offered under "Gin"
  When a reporter submits a report <make>, with "Model" sent with no choice
  Then the report is accepted with no answer to "Model"

Examples:
  | make                          |
  | leaving "Make" unanswered     |
  | answering "Make" with "Gin"   |
  | answering "Make" with "Ozone", after every "Model" choice under "Ozone" was removed |

@REQ-SUB-115
Scenario Outline: A choice offered under several parent answers is accepted under each, and refused under any other
  Given the "Model" question's choices depend on the "Make" question, and "Other" is offered under "Niviuk" and "Ozone"
  When a reporter submits "Model" answered with "Other" and "Make" answered with <make>
  Then <outcome>

Examples:
  | make     | outcome                                                                                     |
  | "Niviuk" | the report is accepted                                                                                            |
  | "Ozone"  | the report is accepted                                                                                            |
  | "Gin"    | the report is refused as invalid, naming "model" and "make" by key, and no report, answer, or choice is written |

@REQ-SUB-080
Scenario: The submission path never calls a translation provider
  Given a submission contains choice answers and a value typed into a type-ahead
  When the submission is stored
  Then no translation provider is called
  And no choice answer stores a copy of either of its choice's labels
  And a new type-ahead value is queued for the Worker to translate, on the value itself

@REQ-SUB-081
Scenario: A choice answer reads both languages from its choice
  Given a single-select and a multi-select question offer choices written in both official languages
  When a reporter answering in English picks one choice from each and submits
  Then each answer reads as its choice's English label, with its French label as the second language
  And the translation source is marked "choice"
  And nothing is sent to the Worker's translator

@REQ-SUB-082
Scenario Outline: A type-ahead answer names a value, and the Worker translates only a new one
  Given a type-ahead question offers a value written in both official languages
  When a reporter answering in English submits <answer>
  Then the answer names <named>
  And its second language comes from <source>

Examples:
  | answer                            | named                          | source                                                 |
  | that value, picked from the list  | that value                     | the value's French label                               |
  | words the question does not offer | a new reporter-added value     | the value's French label, once the Worker supplies it  |

@REQ-SUB-025
Scenario: Every answer's value and locale are immutable once submitted
  Given a report has been submitted
  Then no request ever changes an answer's value or the locale it was given in
  And this holds for every answer type, not only select-shaped ones

@REQ-SUB-026
Scenario: The Worker mechanically translates every answer that needs it
  Given a submitted report has answers needing machine translation, in one locale
  When the Worker claims that report's translation job
  Then it calls the mechanical translation port once per locale group, never the summarization model
  And it writes each answer's translated value and marks the translation source "auto"
  And a skipped answer, with no value, is never sent to the translator

@REQ-SUB-071
Scenario: Only free text marked for translation is machine-translated
  Given a submitted report answers a paragraph question marked for translation
  And it answers a short-text question not marked for translation
  And it answers an email, a phone number, a date, a time, a number, and a yes/no question
  When the Worker translates that report's answers
  Then only the paragraph answer is sent to the translator
  And the yes/no answer, stored as true or false, is never sent to the translator
  And every other answer, the yes/no answer included, keeps no second language

@REQ-SUB-119
Scenario: A second automatic translation is refused
  Given an answer already has a translation the Worker supplied automatically
  When the Worker's translator attempts to supply that answer's translation again
  Then the domain refuses it
  And the stored translated value is unchanged

@REQ-SUB-120
Scenario: Nothing is left that supplies or corrects an answer's translation by hand
  Given an answer already has a translation the Worker supplied automatically
  Then no request can supply a human translation for it
  And no request lists answers waiting for one

@REQ-SUB-008
Scenario Outline: A malformed submission is refused
  Given a malformed submission has <problem>
  When it is validated
  Then the report is refused as invalid

Examples:
  | problem                                             |
  | a duplicate question revision                       |
  | a non-null value from the wrong answer shape        |
  | a malformed upload ID                               |
  | the same upload ID named more than once             |
  | more upload IDs than the attachment limit           |
  | an unknown question revision                        |
  | a deleted question revision                         |
  | no explicit answer to the publication consent revision |

@REQ-SUB-096
Scenario Outline: A well-formed email or phone answer is stored as written
  Given a reporter writing in <language> submits <submitted> as the answer to an <type> question
  When the answer is persisted
  Then the stored value is <stored>

Examples:
  | language | type  | submitted              | stored                                  |
  | English  | email | pilot@example.com      | pilot@example.com                       |
  | French   | email | pilote@exemple.qc.ca   | pilote@exemple.qc.ca                    |
  | English  | phone | +16045551234           | +16045551234                            |
  | French   | phone | +33612345678           | +33612345678                            |
  | English  | email | an empty string        | nothing, as the answer was skipped      |
  | English  | phone | an empty string        | nothing, as the answer was skipped      |

@REQ-SUB-097
Scenario Outline: A malformed email or phone answer is refused by its question key
  Given a reporter writing in English submits <submitted> as the answer to an <type> question
  When the submission is made
  Then the submission is refused
  And the refusal names the question by its key
  And no stored answer carries that value

Examples:
  | type  | submitted          |
  | email | pilot.example.com  |
  | email | pilot@             |
  | email | @example.com       |
  | email | pilot@example      |
  | email | pilot @example.com |
  | phone | 604-555-1234       |
  | phone | 6045551234         |
  | phone | +1 604 555 1234    |
  | phone | +1604555123        |
  | phone | +15555551234       |

@REQ-SUB-108
Scenario Outline: A future date is refused by its question key unless the question allows future dates
  Given a reporter writing in English submits <date> as the answer to a date question that does not allow future dates
  When the submission is made
  Then the submission is refused
  And the refusal names the question by its key
  And the refusal says the question does not allow a date after today
  And no stored answer carries that value

Examples:
  | date                                        |
  | the day after today in the latest time zone |
  | 9999-12-31                                  |

@REQ-SUB-109
Scenario Outline: A date that is today somewhere is accepted, and a question that allows future dates accepts any date
  Given a reporter writing in English submits <date> as the answer to a date question that <allows> future dates
  When the answer is persisted
  Then the date is stored as sent

Examples:
  | date                                        | allows         |
  | today in the latest time zone               | does not allow |
  | 2020-02-29                                  | does not allow |
  | the day after today in the latest time zone | allows         |
  | 9999-12-31                                  | allows         |

@REQ-SUB-110
Scenario: The form's question list says whether each date question allows future dates
  Given a live date question that allows future dates and one that does not
  When the reporter's form reads the current questions
  Then each says whether it allows future dates, true or false, matching its setting

@REQ-SUB-010
Scenario Outline: A submission naming a revision that is not current, or naming revisions inconsistently, is refused
  Given a submission carries <answers>
  When the submission is validated
  Then the report is refused as invalid
  And nothing is stored

Examples:
  | answers                                                   |
  | an answer naming an unknown revision                      |
  | an answer naming a deleted revision                       |
  | an answer naming a superseded revision of a live question |
  | two answers naming the same revision                      |

@REQ-SUB-011
Scenario: Reporter-visible errors never echo submitted content
  Given a submission fails validation
  When the error is returned to the reporter
  Then the error is localized and safe
  And it never echoes an answer, client filename, bearer token, credential, or storage key
  And routine invalid requests are not logged with body content

@REQ-SUB-013
Scenario: A valid submission is persisted atomically
  Given a submission passes every validation step
  When the submission is stored
  Then one database transaction creates the report and consent projection, one answer per shown answer-producing revision including skips, report-file metadata linked to its file-upload answer for each claimed upload, one summarization job, one answer-translation job, and one independent attachment-processing job per file

@REQ-SUB-014
Scenario: A failed transaction leaves no visible report and no leaked blobs
  Given the persistence transaction for a submission fails
  When the failed request returns
  Then no report is visible
  And the uploads it named stay unclaimed in quarantine and expire through the storage lifecycle rule

@REQ-SUB-015
Scenario: A successful submission returns an opaque accepted receipt
  Given a submission passes validation and persists successfully
  When the submission is answered
  Then the report is accepted, with an opaque report ID and the status "submitted"
  And the response contains no raw answers or attachment URLs, and carries only the report ID, the status, and the browser receipt

@REQ-SUB-016
@ui
Scenario: The UI prevents duplicate submission while a request is in flight
  Given a reporter has just submitted the form
  When the request is still in flight
  Then the UI shows bounded progress and disables repeat submission
  And retains local state if the network result is uncertain
  And clears saved local state only after a definite acceptance

@REQ-SUB-017
Scenario: A rate-limited submission is refused
  Given a submission request arrives
  When the per-IP rate limit is exceeded
  Then the request is refused as too frequent, with a safe retry signal
  And the client address used for rate limiting is the viewer address CloudFront always sets, which a member cannot forge, and is never stored on the report

@REQ-SUB-018
Scenario: An unauthenticated submission is refused
  Given a submission request carries no bearer token
  When the submission is processed
  Then it is refused as unauthenticated before any report state is created

@REQ-SUB-019
Scenario Outline: A member of any role may submit a report
  Given a reporter holds a valid member token with the <role> role
  When a valid submission is made
  Then the submission is accepted

Examples:
  | role           |
  | User           |
  | Safety Officer |
  | Administrator  |

@REQ-SUB-020
Scenario: A stored report carries no reporter token subject, user id, or link
  Given a reporter submits a valid report while signed in
  When the submission is committed
  Then no stored report, answer, file, upload, consent projection, or Worker job records the reporter's token subject
  And no stored value, link, or hash anywhere ties the report to the member who filed it

@REQ-SUB-021
Scenario: No audit entry or log line records who submitted a report
  Given a reporter submits a valid report while signed in
  When the submission completes
  Then no audit entry attributes the submission to a token subject
  And no log line records the reporter's token subject at any level

@REQ-SUB-022
@ui
Scenario: An anonymous visitor is asked to sign in before the report page is offered
  Given an anonymous visitor opens the report page
  Then the report page content is not shown
  And the page explains that filing a report requires an HPAC member sign-in
  And it offers a sign-in action

@REQ-SUB-023
@ui
Scenario: The report page tells the reporter that signing in does not attach them to the report
  Given a member opens the report page
  Then the report page content is shown
  And a notice states that signing in only confirms HPAC membership
  And the notice states that the report is not linked to their account

@REQ-SUB-024
@ui
Scenario: The not-tracked notice is shown in the reporter's chosen language
  Given a member opens the report page in French
  Then the notice is shown in French

@REQ-SUB-072
Scenario: Minting an upload returns a pre-signed upload link for one quarantine key and nothing else
  Given a member asks to upload an allowlisted file within its kind's size limit
  When the upload is minted
  Then it is created with an opaque upload ID, the attachment's kind, a pre-signed upload link, and when that link expires
  And the URL writes only the quarantine key named by that upload ID, and lives at most 15 minutes
  And the URL is signed for the declared content type and the exact declared size
  And the request carries no filename, and none is persisted or logged for it
  And the response never echoes a client filename or a report ID
  And nothing is written to object storage or the database

@REQ-SUB-073
Scenario Outline: A declared file that will not be accepted gets no upload URL
  Given a member asks to upload <file>
  When the declared type and size are checked
  Then it is refused as invalid with a safe refusal reason of "<reason>"
  And no upload URL is minted

Examples:
  | file                                                  | reason                |
  | a file of zero bytes                                  | empty                 |
  | a video one byte larger than 250 MB                   | too_large             |
  | an image one byte larger than 25 MB                   | too_large             |
  | a document one byte larger than 25 MB                 | too_large             |
  | a file whose declared type is not on the allowlist    | unaccepted_media_type |

@REQ-SUB-074
Scenario Outline: Storage accepts only the upload the URL was signed for
  Given an upload URL has been minted
  When the browser sends <request>
  Then storage refuses it
  And nothing is stored under that upload's quarantine key

Examples:
  | request                                              |
  | a body larger or smaller than the declared size      |
  | a content type other than the declared one           |
  | an upload after the URL has expired                  |
  | an upload to any key other than the one it was minted for |

@REQ-SUB-075
Scenario Outline: A submission validates every upload it claims
  Given a submission claims an upload whose stored file is <file>
  When the submission is validated
  Then the report is refused as invalid
  And the response names that upload ID with a safe refusal reason of "<reason>"
  And no report, answer, file, or Worker job is created
  And only the upload's size and the bytes sniffing needs were read, never the whole file into memory

Examples:
  | file                                                            | reason                 |
  | bytes that match no known format                                | unrecognised_content   |
  | declared as one allowlisted type but containing another         | declared_type_mismatch |
  | declared as a video but detected as a 30 MB image               | too_large              |

@REQ-SUB-041
Scenario: A submission naming an expired or unknown upload is refused by name
  Given a submission names an upload ID that no longer exists in quarantine
  When the submission is validated
  Then the report is refused as invalid
  And the response lists exactly the upload IDs it could not find
  And no report, answer, file, or Worker job is created

@REQ-SUB-042
Scenario: A claimed upload leaves quarantine once the report commits
  Given a submission claims an upload
  When the report's transaction commits
  Then the claimed bytes live in the report's own compartments
  And the upload is removed from quarantine, with the lifecycle rule as the backstop if that removal fails

@REQ-SUB-043
Scenario: An unauthenticated upload is refused
  Given an upload request carries no bearer token
  When it is received
  Then it is refused as unauthenticated before anything is written to object storage

@REQ-SUB-044
Scenario: A rate-limited upload is refused
  Given an upload request arrives
  When the per-IP upload rate limit is exceeded
  Then the request is refused as too frequent, with a safe retry signal

@REQ-SUB-045
@ui
Scenario: Attaching a file uploads it at once with an activity indicator
  Given the current page shows a file-upload question
  When the reporter attaches a file
  Then the file appears in a list of attached files under its own name
  And an indeterminate activity indicator shows on that file's row while it uploads
  And once the upload finishes the indicator is replaced by a Remove control

@REQ-SUB-046
@ui
Scenario: Next and Submit wait for every upload to finish
  Given a file on the current page is still uploading
  Then the Next or Submit control is disabled

@REQ-SUB-153
@ui
Scenario: Next and Submit are offered again once the upload finishes
  Given a file on the current page is still uploading
  When the upload finishes
  Then the Next or Submit control is enabled again

@REQ-SUB-047
@ui
Scenario: A reporter may cancel an upload in progress
  Given a file on the current page is still uploading
  When the reporter cancels that file's upload
  Then the upload request is aborted
  And the file is removed from the list

@REQ-SUB-048
@ui
Scenario: A reporter may remove an uploaded file
  Given a file on the current page has finished uploading
  When the reporter removes that file
  Then the browser asks to delete that upload
  And the file is removed from the list and is not named by the submission

@REQ-SUB-049
@ui
Scenario: The form refuses a file past the attachment limit
  Given the reporter has already attached as many files as the attachment limit allows
  When the reporter attaches one more
  Then that file is not uploaded
  And an inline, localized message states the limit

@REQ-SUB-050
@ui
Scenario: A refused upload is explained on that file's row
  Given an uploaded file is refused
  Then that file's row shows a localized reason matching the refusal
  And the file is not named by the submission

@REQ-SUB-084
@ui
Scenario: A file larger than its kind allows is refused on its row before it is sent
  Given the current page shows a file-upload question
  When the reporter attaches a video larger than 250 MB
  Then that file's row shows a localized message stating the limit for each kind
  And nothing is sent to the server or to storage for it

@REQ-SUB-076
@ui
Scenario: A file refused at submission is marked on its row and nothing else is lost
  Given a submission is refused for some uploads that failed validation
  Then each refused file's row shows a localized reason matching its refusal
  And every other answer and upload is kept

@REQ-SUB-051
@ui
Scenario: An expired upload is marked for re-attachment and nothing else is lost
  Given a submission is refused for some uploads that expired
  Then each of those files is marked expired with a prompt to attach it again
  And every other answer and upload is kept
  And the reporter can submit again once the files are re-attached

@REQ-SUB-063
@ui
Scenario: Continuing a saved report restores its uploaded files
  Given the reporter has uploaded files and the browser holds a saved report
  When the reporter reloads the form and continues the saved report
  Then each uploaded file is listed as attached under its own name, with a Remove control
  And the submission names each restored file by its upload ID

@REQ-SUB-064
@ui
Scenario: Starting over erases the saved report's uploads
  Given this browser holds a saved report naming uploaded files
  When the reporter returns to the form and declines to continue
  Then the browser asks to delete each of those uploads
  And the browser removes the saved report

@REQ-SUB-065
@ui
Scenario: A reporter may discard the report in progress
  Given the reporter has uploaded files and the browser holds a saved report
  When the reporter discards the report and confirms
  Then the browser asks to delete each of those uploads
  And the browser removes the saved report
  And the form opens at its introduction with no answers

@REQ-SUB-066
@ui
Scenario: Discarding a report asks for confirmation first
  Given the reporter has uploaded files and the browser holds a saved report
  When the reporter starts to discard the report and then keeps it
  Then no upload is deleted
  And the saved report and its answers are kept

@REQ-SUB-067
@ui
Scenario: An expired saved report's uploads are erased
  Given this browser holds a saved report started more than 15 days ago that names uploaded files
  When the reporter returns to the form
  Then the browser asks to delete each of those uploads
  And the browser ignores or removes the expired state

@REQ-SUB-058
@ui
Scenario: The attachment question is a drop zone with a large choose-files control
  Given the current page shows a file-upload question
  Then the question shows a drop zone with a large upload icon and a localized "drag files here, or choose files" prompt
  And the type, count, and size guidance sits inside the drop zone

@REQ-SUB-059
@ui
Scenario: The drop zone's control opens the file chooser from a pointer or the keyboard
  Given the current page shows a file-upload question
  When the reporter activates the drop zone's choose-files control by pointer or keyboard
  Then the browser's file chooser opens for that question

@REQ-SUB-060
@ui
Scenario: Files dropped on the drop zone upload exactly as chosen files do
  Given the current page shows a file-upload question
  When the reporter drops two files on the drop zone
  Then both files appear in the list of attached files under their own names
  And each shows its own activity indicator while it uploads

@REQ-SUB-061
@ui
Scenario: Dropped files past the attachment limit are refused
  Given the reporter has attached one file fewer than the attachment limit allows
  When the reporter drops two more files on the drop zone
  Then only one of them is uploaded
  And an inline, localized message states the limit

@REQ-SUB-062
@ui
Scenario: A file dropped outside the drop zone does nothing
  Given the current page shows a file-upload question
  When the reporter drops a file on the page outside the drop zone
  Then the browser stays on the form
  And no file is attached or uploaded

@REQ-SUB-116
Scenario: A request that arrives without CloudFront's origin secret is refused
  Given origin verification is required
  When a submission request arrives without CloudFront's origin secret
  Then it is refused as forbidden, before authentication or anything else runs

@REQ-SUB-117
Scenario: The rate limiter partitions by the CloudFront viewer address, not the shared connection
  Given the per-IP submission rate limit is exhausted for one CloudFront viewer address
  When a submission request arrives from a different CloudFront viewer address
  Then it is not refused as too frequent

@REQ-SUB-118
Scenario: A successful submission nudges the Worker
  Given each nudge to the Worker is recorded, and a submission is ready to persist
  When the submission is answered
  Then the Worker is nudged once

@REQ-SUB-122
@ui
Scenario: Leaving the report form for another page while it holds unsubmitted answers says the report is saved
  Given a reporter is filling out the form
  When the reporter activates a header navigation link away from the form
  Then a bilingual dialog says the report is saved in this browser until the day its 15 days end, offering to keep working

@REQ-SUB-154
@ui
Scenario: Confirming the leave dialog goes on to the page the reporter chose
  Given a reporter filling out the form has activated a header navigation link away from it
  When the reporter confirms leaving
  Then the browser navigates to that page

@REQ-SUB-128
@ui
Scenario: Closing or reloading the tab while the report form holds only saved answers shows no prompt
  Given a reporter is filling out the form
  When the reporter reloads the tab
  Then no unload prompt appears
  And a dialog asks whether to continue where they left off, with No and Yes buttons

@REQ-SUB-129
@ui
Scenario: Leaving the report form for another page while a file is still uploading says that file will not be kept
  Given a file on the current page is still uploading
  When the reporter activates a header navigation link away from the form
  Then the dialog also says a file still uploading will not be kept if they leave

@REQ-SUB-130
@ui
Scenario: Closing or reloading the tab while a file is still uploading triggers the browser's own prompt
  Given a file on the current page is still uploading
  When the reporter tries to close or reload the tab
  Then the browser's own unload prompt appears, with no custom text

@REQ-SUB-121
@ui
Scenario: Leaving the untouched report form never shows a confirmation
  Given a reporter has not answered anything on the report form
  When the reporter activates a header navigation link away from the form
  Then the browser navigates to that page with no dialog shown

@REQ-SUB-133
Scenario: A successful submission returns a random browser receipt
  Given a submission passes validation and persists successfully
  When the submission is answered
  Then the body carries a receipt of at least 256 random bits, base64url
  And two submissions by the same member return unrelated receipts

@REQ-SUB-134
Scenario: The report stores only the receipt's SHA-256 hash
  Given a reporter submits a valid report while signed in
  When the submission is committed
  Then the stored report holds the SHA-256 hash of the receipt it returned and nowhere else the receipt itself
  And no log line records the receipt

@REQ-SUB-135
Scenario: The receipt links a report to a browser, never to a member
  Given a reporter submits a valid report while signed in
  When the submission is committed
  Then no stored value of that report, its answers, or its Worker jobs is the reporter's token subject or a hash of it
  And the stored receipt hash is not derived from the token subject

@REQ-SUB-136
@ui
Scenario: The browser keeps the receipt once the report is accepted and never puts it in an address
  Given a member submits a valid report
  When the report is accepted with a receipt
  Then the browser keeps the report ID and the receipt in its own storage
  And no request address or navigation carries the receipt
