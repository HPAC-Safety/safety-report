Feature: Report submission
A reporter's answers, revision IDs, and locale live only in the browser
until one final submission request. Before that request, the API and database
receive no unfinished report state. The one thing that reaches the server
earlier is an attachment, sent straight into private quarantine through a
pre-signed PUT the API mints as soon as it is attached, and validated and
claimed by that request (ADR-0096, ADR-0126). The browser's saved report
names its finished uploads, so they are kept exactly as long as it is
(ADR-0100).

Background:
  Given a reporter writes a report through POST /api/v1/reports and sends each attachment through a pre-signed PUT that POST /api/v1/uploads mints
  And both require a valid member bearer token
  And the report request is JSON that names each attachment by the upload ID the upload returned
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
  When the reporter returns to the form
  And the reporter chooses to continue
  Then the form opens on the page the reporter was last on
  And the saved answers are restored

@REQ-SUB-037
@ui
Scenario: Declining a saved report starts a fresh form
  Given this browser holds an unexpired saved report
  When the reporter returns to the form
  And the reporter declines to continue
  Then the browser removes the saved report
  And the form opens at its introduction with no answers

@REQ-SUB-038
@ui
Scenario: A reporter with no saved report is not asked
  Given this browser holds no saved report
  When the reporter returns to the form
  Then no dialog asks whether to continue

@REQ-SUB-053
@ui
Scenario: Each page of the form has its own address
  Given a reporter is on the form's introduction at /report
  When the reporter presses Next
  Then the address names the page now shown, as /report/<question-key>
  When the reporter presses Back
  Then the address is /report

@REQ-SUB-054
@ui
Scenario: The browser's Back and Forward buttons move between pages under the form's rules
  Given a reporter has answered a required question and pressed Next
  When the reporter presses the browser's Back button
  Then the required question's page shows and the address names it
  When the reporter clears the answer and presses the browser's Forward button
  Then the required question's page still shows
  And an inline, localized message explains that an answer is required

@REQ-SUB-055
@ui
Scenario: Continuing a saved report puts its page in the address
  Given this browser holds an unexpired saved report
  When the reporter returns to the form
  And the reporter chooses to continue
  Then the address names the page the reporter was last on

@REQ-SUB-056
@ui
Scenario: A page address never answers the continue question for the reporter
  Given this browser holds an unexpired saved report
  When the reporter opens the address of a page other than the one saved
  Then a dialog asks whether to continue where they left off, with No and Yes buttons
  When the reporter declines to continue
  Then the address is /report
  And the form opens at its introduction with no answers

@REQ-SUB-057
@ui
Scenario: A page address without a saved report opens the introduction
  Given this browser holds no saved report
  When the reporter opens the address of a later page of the form
  Then the form opens at its introduction at /report
  When the reporter opens the address of a page the form does not have
  Then the form opens at its introduction at /report

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
  When a reporter presses Next
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
  When a reporter presses Next
  Then the page does not advance
  And an inline, localized message explains that an answer is required

@REQ-SUB-032
@ui
Scenario: A conditional question is absent from paging until its parent condition is met
  Given a question depends on a yes/no or single-select question
  When the parent's current answer does not meet the condition
  Then the dependent question's page is skipped entirely
  When the reporter then answers the parent so the condition is met
  Then the dependent question's page appears in the sequence

@REQ-SUB-033
@ui
Scenario: The Next button becomes Submit on the final page
  Given a reporter has reached the last page of the form
  Then the control that was Next now reads Submit
  And pressing it sends the one final submission request

@REQ-SUB-034
@ui
Scenario: A multi-select question is a picker dropdown, not a flat list
  Given the current page shows a multi-select question
  Then its options are hidden behind one closed picker labelled by the question
  When the reporter opens the picker and checks two options
  Then the picker stays open with both options checked
  When the reporter presses Escape
  Then the picker closes, returns focus to itself, and names both chosen options

@REQ-SUB-085
@ui
Scenario Outline: An email or phone question opens the matching keyboard
  Given the current page shows an optional <type> question
  Then its field has type "<input type>", input mode "<input mode>", and autocomplete "<autocomplete>"

Examples:
  | type  | input type | input mode | autocomplete |
  | email | email      | email      | email        |
  | phone | tel        | tel        | tel          |

@REQ-SUB-086
@ui
Scenario Outline: An optional email or phone question may be left blank
  Given the current page shows an optional <type> question
  When the reporter leaves it blank and presses Next
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
  When the reporter types "chase.florell@example" into it and presses Next
  Then the reporter stays on that page
  And an inline, localized message asks for an email address like name@example.com

@REQ-SUB-088
@ui
Scenario: A phone number that is not valid for its country holds the reporter on its page
  Given the current page shows an optional phone question
  When the reporter types "5551234" into it and presses Next
  Then the reporter stays on that page
  And an inline, localized message asks for a phone number valid for the chosen country

@REQ-SUB-089
@ui
Scenario: The phone country picker starts on Canada
  Given the current page shows an optional phone question
  Then its country picker shows "🇨🇦 +1"
  And the phone field's placeholder is "(555) 555-5555"

@REQ-SUB-090
@ui
Scenario Outline: A phone number takes its chosen country's mask as it is typed
  Given the current page shows an optional phone question
  When the reporter chooses <country> in its country picker
  Then its country picker shows "<shown>"
  And the phone field's placeholder is "<placeholder>"
  When the reporter types "<digits>" into it
  Then the phone field reads "<masked>"

Examples:
  | country        | shown   | placeholder    | digits     | masked         |
  | Canada         | 🇨🇦 +1  | (555) 555-5555 | 6045551234 | (604) 555-1234 |
  | United Kingdom | 🇬🇧 +44 | 5555 555555    | 2079460018 | 20 7946 0018   |
  | France         | 🇫🇷 +33 | 5 55 55 55 55  | 612345678  | 6 12 34 56 78  |

@REQ-SUB-091
@ui
Scenario Outline: A phone answer is sent in E.164
  Given the current page shows an optional phone question
  When the reporter chooses <country> in its country picker
  And the reporter types "<digits>" into it
  And the reporter goes on to submit the report
  Then the phone answer is sent as "<sent>"

Examples:
  | country        | digits     | sent          |
  | Canada         | 6045551234 | +16045551234  |
  | United Kingdom | 2079460018 | +442079460018 |

@REQ-SUB-092
@ui
Scenario: Before "@" is typed, every suggested domain is offered for what has been typed
  Given the current page shows an optional email question
  When the reporter types "chas" into it
  Then the field is a combobox whose suggestion list is labelled "Suggested email addresses"
  And the suggestions below it are, in order:
    | chas@gmail.com   |
    | chas@yahoo.com   |
    | chas@hotmail.com |
    | chas@outlook.com |
    | chas@icloud.com  |
    | chas@mail.com    |
  When the reporter types "e" into it
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
  When the reporter types "chase.florell@g" into it
  Then the suggestions below it are, in order:
    | chase.florell@gmail.com |

@REQ-SUB-094
@ui
Scenario Outline: Choosing a suggestion fills the field
  Given the current page shows an optional email question
  When the reporter types "chase.florell@h" into it
  And the reporter chooses "chase.florell@hotmail.com" <how>
  Then the email field reads "chase.florell@hotmail.com"
  And no suggestions are shown

Examples:
  | how               |
  | with the keyboard |
  | with the pointer  |

@REQ-SUB-095
@ui
Scenario: An address at a domain outside the suggestions is accepted
  Given the current page shows an optional email question
  When the reporter types "pilot@example.ca" into it and presses Next
  Then the next page shows

@REQ-SUB-098
@ui
Scenario Outline: On a desktop, clicking or focusing a date field opens a one-month calendar under it
  Given the current page shows a date question that does not allow future dates, on a desktop
  When the reporter <opens> the date field
  Then a calendar labelled "Choose a date" opens under the field, showing today's month
  And today is marked in it
  And it has "Previous month" and "Next month" buttons

Examples:
  | opens     |
  | clicks    |
  | tabs into |

@REQ-SUB-099
@ui
Scenario: Choosing a day fills the field as yyyy-mm-dd and closes the calendar
  Given the current page shows a date question that does not allow future dates, on a desktop
  When the reporter clicks the date field and chooses the 1st of today's month
  Then the date field reads the 1st of today's month as yyyy-mm-dd
  And the calendar closes
  And the chosen day is announced in words

@REQ-SUB-100
@ui
Scenario Outline: The calendar disables the days after today unless the question allows future dates
  Given the current page shows a date question that <allows> future dates, on a desktop
  When the reporter clicks the date field
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
  When the reporter types "<typed>" into the date field and presses Next
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
  When the reporter types "2024-02-29" into the date field and presses Next
  And the reporter submits the report from the next page
  Then the date answer is sent as "2024-02-29"

@REQ-SUB-103
@ui
Scenario Outline: The calendar is in the reader's language
  Given the current page shows a date question in <language>, on a desktop
  When the reporter clicks the date field
  Then the calendar names today's month in <language>
  And its weekday headings start on <first day>
  And its buttons and pickers are labelled from the <language> catalogue

Examples:
  | language | first day |
  | English  | Sunday    |
  | French   | Monday    |

@REQ-SUB-104
@ui
Scenario: The calendar works from the keyboard
  Given the current page shows a date question that allows future dates, on a desktop
  When the reporter tabs into the date field and presses ArrowDown
  Then today has focus in the calendar
  When the reporter presses ArrowLeft
  Then the day 1 day before today has focus
  When the reporter presses ArrowUp
  Then the day 8 days before today has focus
  When the reporter presses ArrowDown
  Then the day 1 day before today has focus
  When the reporter presses ArrowRight
  Then today has focus in the calendar
  When the reporter presses PageUp
  Then the same day of the previous month has focus
  When the reporter presses PageDown
  Then today has focus in the calendar
  When the reporter presses Enter
  Then the date field reads today as yyyy-mm-dd
  And the calendar closes
  And focus is on the date field
  When the reporter presses ArrowDown and then Escape
  Then the calendar closes
  And focus is on the date field

@REQ-SUB-111
@ui
Scenario: Tabbing past a date field skips its calendar
  Given the current page shows a date question that does not allow future dates, on a desktop
  When the reporter tabs into the date field
  Then a calendar labelled "Choose a date" opens under the field, showing today's month
  When the reporter presses Tab
  Then focus skips the calendar to the Next button, and the calendar closes

@REQ-SUB-105
@ui
Scenario: The reporter jumps to a month and year a few years back
  Given the current page shows a date question that does not allow future dates, on a desktop
  When the reporter clicks the date field
  And the reporter chooses March in the calendar's month picker and 2023 in its year picker
  Then the calendar shows March 2023
  When the reporter chooses the 14th
  Then the date field reads "2023-03-14"

@REQ-SUB-106
@ui
Scenario Outline: On a touch device, a date question uses the device's own date picker
  Given the current page shows a date question that <allows> future dates, on a touch device
  Then the date field is a native date input <limit>
  And tapping it opens no calendar of the form's own

Examples:
  | allows         | limit                      |
  | does not allow | whose latest date is today |
  | allows         | with no latest date        |

@REQ-SUB-107
@ui
Scenario: On a touch device, a future date the device's picker lets through still holds the reporter on its page
  Given the current page shows a date question that does not allow future dates, on a touch device
  When the device's picker sets the date field to "9999-12-31" and the reporter presses Next
  Then the reporter stays on the date page
  And an inline message says "Choose a date that is not in the future."

@REQ-SUB-078
Scenario: One answer entry per shown answer-producing revision
  Given the client says it showed the reporter a set of answer-producing revisions
  When the reporter submits the form
  Then the submission DTO contains exactly one answer entry for each of those revisions
  And a single-select, multi-select, or type-ahead answer carries the identifiers of the chosen choices in "choices"
  And a type-ahead answer naming a value the question does not offer carries the typed text in "value" instead
  And every other answer uses "value", a single string, alongside the locale it was given in
  And file-upload answers additionally carry one attachment entry per file attached to that question, each an upload ID and the file's name
  And fields for the other answer shapes are null

@REQ-SUB-077
@ui
Scenario Outline: A yes or no is sent as a JSON boolean whatever language the report is submitted in
  Given a signed-in reporter answers a yes/no question and publication consent in <answered in>
  And the reporter switches the form to <submitted in> before submitting
  When the reporter submits the report
  Then the yes/no answer is sent as the JSON boolean false and the consent answer as the JSON boolean true

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
  When the submission DTO is built
  Then a skipped answer of any type has a null value
  And a skipped file upload has an empty attachments list

@REQ-SUB-079
Scenario: A submitted choice must be one the question offers
  Given a reporter submits a single-select, multi-select, or type-ahead answer naming choices by identifier
  When the API validates the submission
  Then the answer is accepted only if every named choice is a live choice of that question
  And a removed choice, or another question's choice, is rejected
  And only a type-ahead also accepts typed text naming a value it does not yet offer

@REQ-SUB-080
Scenario: The submission path never calls a translation provider
  Given a submission contains choice answers and a value typed into a type-ahead
  When the API commits the submission
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
  Then no endpoint ever changes an answer's value or the locale it was given in
  And this holds for every answer type, not only select-shaped ones

@REQ-SUB-026
Scenario: The Worker mechanically translates every answer that needs it
  Given a submitted report has answers needing machine translation, in one locale
  When the Worker claims that report's translation outbox message
  Then it calls the mechanical translation port once per locale group, never the summarization model
  And it writes each answer's translated value and marks the translation source "auto"
  And a skipped answer, with no value, is never sent to the translator

@REQ-SUB-071
Scenario: Only free text marked for translation is machine-translated
  Given a submitted report answers a long-text question marked for translation
  And it answers a short-text question not marked for translation
  And it answers an email, a phone number, a date, a time, a number, and a yes/no question
  When the Worker translates that report's answers
  Then only the long-text answer is sent to the translator
  And the yes/no answer, stored as a boolean, is never sent to the translator
  And every other answer, the yes/no answer included, keeps no second language

@REQ-SUB-027
Scenario: An administrator's correction always wins over the Worker's translation
  Given an answer already has a translation the Worker supplied automatically
  When an administrator supplies or corrects that answer's translated value
  Then the stored translated value is the administrator's
  And the translation source is marked "human"

@REQ-SUB-008
Scenario Outline: The API rejects a malformed submission DTO
  Given a submission DTO contains <problem>
  When the API validates it
  Then the API rejects the submission

Examples:
  | problem                                             |
  | a duplicate question_revision_id                    |
  | a non-null field from the wrong answer shape        |
  | a malformed upload ID                               |
  | the same upload ID named more than once             |
  | more upload IDs than the attachment limit           |
  | an unknown question_revision_id                     |
  | a question_revision_id for a deleted revision       |
  | no explicit answer to the consent_publish revision  |

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
  | English  | email | an empty string        | nothing, because the answer was skipped |
  | English  | phone | an empty string        | nothing, because the answer was skipped |

@REQ-SUB-097
Scenario Outline: A malformed email or phone answer is refused by its question key
  Given a reporter writing in English submits <submitted> as the answer to an <type> question
  When the submission is made
  Then the submission is rejected
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
  Then the submission is rejected
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
  Then each carries allowFutureDates as the JSON boolean matching its setting

@REQ-SUB-009
Scenario: A submission may answer a known superseded revision
  Given the browser's session began before an Administrator edited the form
  And an answered revision is a known, non-deleted, superseded revision
  When the API validates the submission
  Then the API validates the answer against that revision's historical type, options, and privacy
  And does not require the submitted set to equal the latest form

@REQ-SUB-010
@ignore
Scenario: A revision that was never shown as answer-producing is rejectable
  Given a submitted answer references a revision that the client was never shown as answer-producing, or the submitted revisions form an internally inconsistent combination for the same stable key
  When the API validates the submission
  Then the API may reject the submission

@REQ-SUB-011
Scenario: Reporter-visible errors never echo submitted content
  Given a submission fails validation
  When the API returns an error to the reporter
  Then the error is localized and safe
  And it never echoes an answer, client filename, bearer token, credential, or storage key
  And routine invalid requests are not logged with body content

@REQ-SUB-013
Scenario: A valid submission is persisted atomically
  Given a submission passes every validation step
  When the API commits the submission
  Then one database transaction creates the report and consent projection, one answer per shown answer-producing revision including skips, report-file metadata linked to its file-upload answer for each claimed upload, one summarization outbox item, one answer-translation outbox item, and one independent attachment-processing outbox item per file

@REQ-SUB-014
Scenario: A failed transaction leaves no visible report and no leaked blobs
  Given the persistence transaction for a submission fails
  When the API returns from the failed request
  Then no report is visible
  And the uploads it named stay unclaimed in quarantine and expire through the storage lifecycle rule

@REQ-SUB-015
Scenario: A successful submission returns an opaque accepted receipt
  Given a submission passes validation and persists successfully
  When the API responds
  Then the response is 202 Accepted with an opaque report ID and the status "submitted"
  And the response contains no raw answers or attachment URLs

@REQ-SUB-016
@ui
Scenario: The UI prevents duplicate submission while a request is in flight
  Given a reporter has just submitted the form
  When the request is still in flight
  Then the UI shows bounded progress and disables repeat submission
  And retains local state if the network result is uncertain
  And clears saved local state only after a definite 202 response

@REQ-SUB-017
Scenario: A rate-limited submission is rejected
  Given a submission request arrives
  When the per-IP rate limit is exceeded
  Then the API rejects the request with 429 and a safe retry signal
  And the client IP used for rate limiting comes from X-Forwarded-For, trusted because only the load balancer can reach the API, and is never stored on the report

@REQ-SUB-018
Scenario: An unauthenticated submission is rejected
  Given a submission request carries no bearer token
  When the API processes the submission
  Then the API rejects it before any report state is created

@REQ-SUB-019
Scenario Outline: A member of any role may submit a report
  Given a reporter holds a valid member token with the <role> role
  When a valid submission is made
  Then the API accepts it

Examples:
  | role          |
  | User          |
  | SafetyOfficer |
  | Administrator |

@REQ-SUB-020
Scenario: A stored report carries no submitter subject, user id, or link
  Given a reporter submits a valid report while signed in
  When the submission is committed
  Then no stored report, answer, file, upload, consent projection, or outbox message records the submitter's subject
  And no column, join table, or hash anywhere links the report to the member who filed it

@REQ-SUB-021
@ignore
Scenario: No audit entry or log line records who submitted a report
  Given a reporter submits a valid report while signed in
  When the submission completes
  Then no audit entry attributes the submission to a subject
  And no log line records the submitting subject at any level

@REQ-SUB-022
@ui
Scenario: A signed-out visitor is asked to sign in before the report page is offered
  Given a signed-out visitor opens the report page
  Then the report page content is not shown
  And the page explains that filing a report requires an HPAC member sign-in
  And it offers a sign-in action

@REQ-SUB-023
@ui
Scenario: The report page tells the reporter that signing in does not attach them to the report
  Given a signed-in member opens the report page
  Then the report page content is shown
  And a notice states that signing in only confirms HPAC membership
  And the notice states that the report is not linked to their account

@REQ-SUB-024
@ui
Scenario: The not-tracked notice is shown in the reporter's chosen language
  Given a signed-in member opens the report page in French
  Then the notice is shown in French

@REQ-SUB-072
Scenario: Minting an upload returns a pre-signed PUT for one quarantine key and nothing else
  Given a member asks to upload an allowlisted file within its kind's size limit
  When the API mints the upload
  Then the response is 201 Created with an opaque upload ID, the attachment's kind, a pre-signed PUT URL, and when that URL expires
  And the URL writes only the quarantine key named by that upload ID, and lives at most 15 minutes
  And the URL is signed for the declared content type and the exact declared size
  And the request carries no filename, and none is persisted or logged for it
  And the response never echoes a client filename or a report ID
  And nothing is written to object storage or the database

@REQ-SUB-073
Scenario Outline: A declared file the API will not accept gets no upload URL
  Given a member asks to upload <file>
  When the API checks the declared type and size
  Then the API rejects it with a safe rejection reason of "<reason>"
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
  Given the API minted an upload URL
  When the browser sends <request>
  Then storage refuses it
  And nothing is stored under that upload's quarantine key

Examples:
  | request                                              |
  | a body larger or smaller than the declared size      |
  | a content type other than the declared one           |
  | the PUT after the URL has expired                    |
  | the PUT to any key other than the one it was minted for |

@REQ-SUB-075
Scenario Outline: A submission validates every upload it claims
  Given a submission claims an upload whose stored file is <file>
  When the API validates the submission
  Then the API rejects the submission with 400
  And the response names that upload ID with a safe rejection reason of "<reason>"
  And no report, answer, file, or outbox row is created
  And the API read only the upload's size and the bytes sniffing needs, never the whole file into memory

Examples:
  | file                                                            | reason                 |
  | bytes that match no known format                                | unrecognised_content   |
  | declared as one allowlisted type but containing another         | declared_type_mismatch |
  | declared as a video but detected as a 30 MB image               | too_large              |

@REQ-SUB-041
Scenario: A submission naming an expired or unknown upload is refused by name
  Given a submission names an upload ID that no longer exists in quarantine
  When the API validates the submission
  Then the API rejects the submission with 400
  And the response lists exactly the upload IDs it could not find
  And no report, answer, file, or outbox row is created

@REQ-SUB-042
Scenario: A claimed upload leaves quarantine once the report commits
  Given a submission claims an upload
  When the report's transaction commits
  Then the claimed bytes live in the report's own compartments
  And the upload is removed from quarantine, with the lifecycle rule as the backstop if that removal fails

@REQ-SUB-043
Scenario: An unauthenticated upload is rejected
  Given an upload request carries no bearer token
  When the API receives it
  Then the API rejects it before anything is written to object storage

@REQ-SUB-044
Scenario: A rate-limited upload is rejected
  Given an upload request arrives
  When the per-IP upload rate limit is exceeded
  Then the API rejects the request with 429 and a safe retry signal

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
  When the upload finishes
  Then the Next or Submit control is enabled again

@REQ-SUB-047
@ui
Scenario: A reporter may cancel an upload in progress
  Given a file on the current page is still uploading
  When the reporter presses that file's Cancel control
  Then the upload request is aborted
  And the file is removed from the list

@REQ-SUB-048
@ui
Scenario: A reporter may remove an uploaded file
  Given a file on the current page has finished uploading
  When the reporter presses that file's Remove control
  Then the browser asks the API to delete that upload
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
  Given the API refuses an uploaded file
  Then that file's row shows a localized reason matching the refusal
  And the file is not named by the submission

@REQ-SUB-084
@ui
Scenario: A file larger than its kind allows is refused on its row before it is sent
  Given the current page shows a file-upload question
  When the reporter attaches a video larger than 250 MB
  Then that file's row shows a localized message stating the limit for each kind
  And nothing is sent to the API or to storage for it

@REQ-SUB-076
@ui
Scenario: A file refused at submission is marked on its row and nothing else is lost
  Given the API refuses a submission because some of its uploads failed validation
  Then each refused file's row shows a localized reason matching its refusal
  And every other answer and upload is kept

@REQ-SUB-051
@ui
Scenario: An expired upload is marked for re-attachment and nothing else is lost
  Given the API refuses a submission because some of its uploads expired
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
  When the reporter returns to the form
  And the reporter declines to continue
  Then the browser asks the API to delete each of those uploads
  And the browser removes the saved report

@REQ-SUB-065
@ui
Scenario: A reporter may discard the report in progress
  Given the reporter has uploaded files and the browser holds a saved report
  When the reporter discards the report and confirms
  Then the browser asks the API to delete each of those uploads
  And the browser removes the saved report
  And the form opens at its introduction with no answers

@REQ-SUB-066
@ui
Scenario: Discarding a report asks for confirmation first
  Given the reporter has uploaded files and the browser holds a saved report
  When the reporter presses Discard report and then keeps the report
  Then no upload is deleted
  And the saved report and its answers are kept

@REQ-SUB-067
@ui
Scenario: An expired saved report's uploads are erased
  Given this browser holds a saved report started more than 15 days ago that names uploaded files
  When the reporter returns to the form
  Then the browser asks the API to delete each of those uploads
  And the browser ignores or removes the expired state

@REQ-SUB-058
@ui
Scenario: The attachment field is a drop zone with a large choose-files control
  Given the current page shows a file-upload question
  Then the field shows a drop zone with a large upload icon and a localized "drag files here, or choose files" prompt
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
