Feature: Report submission
A reporter's answers, revision IDs, and locale live only in the browser
until one final multipart request. Before that request, the API, database,
and object storage receive no unfinished report state.

Background:
  Given the only write endpoint for a reporter is POST /api/v1/reports
  And it requires a valid member bearer token
  And it accepts multipart/form-data with one report JSON part and zero or more files parts
  And the bearer token is transport/security metadata, not persisted report content

@REQ-SUB-001
@ui
Scenario: The browser holds report state locally until submission
  Given a reporter is filling out the form
  When the reporter has not yet submitted
  Then the selected locale, shown question-revision IDs, and entered answers exist only in local browser storage with a 15-day expiry
  And image, video, and document attachments are never placed in browser storage
  And no server draft, report ID reservation, upload token, or resumable upload protocol exists

@REQ-SUB-002
@ui
Scenario: A successful submission clears local browser state
  Given a reporter has entered answers in local browser storage
  When the final multipart request succeeds
  Then the browser clears that local state

@REQ-SUB-003
@ui
Scenario: Expired local state is not restored
  Given local browser state is older than 15 days
  When the reporter returns to the form
  Then the browser ignores or removes the expired state

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
  And pressing it sends the one final multipart request

@REQ-SUB-034
@ui
Scenario: A multi-select question is a picker dropdown, not a flat list
  Given the current page shows a multi-select question
  Then its options are hidden behind one closed picker labelled by the question
  When the reporter opens the picker and checks two options
  Then the picker stays open with both options checked
  When the reporter presses Escape
  Then the picker closes, returns focus to itself, and names both chosen options

@REQ-SUB-004
Scenario: One answer entry per shown answer-producing revision
  Given the client says it showed the reporter a set of answer-producing revisions
  When the reporter submits the form
  Then the submission DTO contains exactly one answer entry for each of those revisions
  And every answer of every type uses "value", a single string, alongside the locale it was given in
  And file-upload answers additionally use zero-based indexes into the repeated files parts
  And fields for the other answer shapes are null

@REQ-SUB-005
Scenario: A skipped answer is represented by an empty value, not omission
  Given a reporter skips an answer-producing question
  When the submission DTO is built
  Then a skipped answer of any type has a null value
  And a skipped file upload has an empty attachment_part_indexes list

@REQ-SUB-006
Scenario: A submitted select value must be one the revision offered
  Given a reporter submits a value for a picker or multi-select question
  When the API validates the submission
  Then the value is accepted only if the answered revision offered exactly that label
  And a value the revision never offered is rejected
  And a type-ahead backed by a live shared list also accepts a value the list does not yet offer

@REQ-SUB-007
Scenario: The submission path never calls a translation provider
  Given a submission contains select answers and a value typed into a type-ahead or a multi-select with reporter additions allowed
  When the API commits the submission
  Then no translation provider is called
  And the answers are stored in the language the reporter gave them in, with no translation yet

@REQ-SUB-025
Scenario: Every answer's value and locale are immutable once submitted
  Given a report has been submitted
  Then no endpoint ever changes an answer's value or the locale it was given in
  And this holds for every answer type, not only select-shaped ones

@REQ-SUB-026
Scenario: The Worker mechanically translates every answer into its second language
  Given a submitted report has answers with values in one locale
  When the Worker claims that report's translation outbox message
  Then it calls the mechanical translation port once per locale group, never the summarization model
  And it writes each answer's translated value and marks the translation source "auto"
  And a skipped answer, with no value, is never sent to the translator

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
  | a duplicate or out-of-range file index              |
  | a files part that is never referenced by any answer |
  | a files part referenced by more than one answer     |
  | an unknown question_revision_id                     |
  | a question_revision_id for a deleted revision       |
  | no explicit answer to the consent_publish revision  |

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

@REQ-SUB-012
Scenario: Accepted attachments are streamed into quarantine under a bound
  Given a submission includes one or more files parts
  When the API accepts an attachment
  Then the API mints an opaque server-side filename/key
  And streams at most 50 MB into the quarantine compartment while computing the actual byte count and inspecting its signature
  And never buffers the whole file in memory
  And never persists or logs the client filename

@REQ-SUB-013
Scenario: A valid submission is persisted atomically
  Given a multipart submission passes every validation step
  When the API commits the submission
  Then one database transaction creates the report and consent projection, one answer per shown answer-producing revision including skips, report-file metadata linked to its file-upload answer for successfully quarantined blobs, one summarization outbox item, one answer-translation outbox item, and one independent attachment-processing outbox item per file

@REQ-SUB-014
Scenario: A failed transaction leaves no visible report and no leaked blobs
  Given the persistence transaction for a submission fails
  When the API returns from the failed request
  Then no report is visible
  And any already-written quarantine blobs are unreferenced and expire through the storage lifecycle rule

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
  And the client IP used for rate limiting comes only from explicitly trusted proxy headers and is never stored on the report

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
  Then no stored report, answer, file, consent projection, or outbox message records the submitter's subject
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
