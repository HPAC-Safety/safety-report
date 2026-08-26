Feature: Report submission
  A reporter's answers, revision IDs, and locale live only in the browser
  until one final multipart request. Before that request, the API, database,
  and object storage receive no unfinished report state.

  Background:
    Given the only write endpoint for a reporter is POST /api/v1/reports
    And it accepts multipart/form-data with one report JSON part, zero or more files parts, and one Turnstile response token

  Scenario: The browser holds report state locally until submission
    Given a reporter is filling out the form
    When the reporter has not yet submitted
    Then the selected locale, shown question-revision IDs, and entered answers exist only in local browser storage with a 15-day expiry
    And image, video, and document attachments are never placed in browser storage
    And no server draft, report ID reservation, upload token, or resumable upload protocol exists

  Scenario: A successful submission clears local browser state
    Given a reporter has entered answers in local browser storage
    When the final multipart request succeeds
    Then the browser clears that local state

  Scenario: Expired local state is not restored
    Given local browser state is older than 15 days
    When the reporter returns to the form
    Then the browser ignores or removes the expired state

  Scenario: One answer entry per shown answer-producing revision
    Given the client says it showed the reporter a set of answer-producing revisions
    When the reporter submits the form
    Then the submission DTO contains exactly one answer entry for each of those revisions
    And textual/scalar answers use "value", selection answers use "option_codes", and file-upload answers use zero-based indexes into the repeated files parts
    And fields for the other answer shapes are null

  Scenario: A skipped answer is represented by an empty value, not omission
    Given a reporter skips an answer-producing question
    When the submission DTO is built
    Then a skipped scalar has a null value
    And a skipped selection has an empty option_codes list
    And a skipped file upload has an empty attachment_part_indexes list

  Scenario Outline: The API rejects a malformed submission DTO
    Given a submission DTO contains <problem>
    When the API validates it
    Then the API rejects the submission

    Examples:
      | problem                                                        |
      | a duplicate question_revision_id                               |
      | a non-null field from the wrong answer shape                   |
      | a duplicate or out-of-range file index                         |
      | a files part that is never referenced by any answer            |
      | a files part referenced by more than one answer                |
      | an answer entry for a statement or group revision              |
      | an unknown question_revision_id                                |
      | a question_revision_id for a deleted revision                  |
      | no explicit answer to the consent_publish revision             |

  Scenario: A submission may answer a known superseded revision
    Given the browser's session began before an Administrator edited the form
    And an answered revision is a known, non-deleted, superseded revision
    When the API validates the submission
    Then the API validates the answer against that revision's historical type, options, and privacy
    And does not require the submitted set to equal the latest form

  Scenario: Reporter-visible errors never echo submitted content
    Given a submission fails validation
    When the API returns an error to the reporter
    Then the error is localized and safe
    And it never echoes an answer, client filename, Turnstile token, or credential
    And routine invalid requests are not logged with body content

  Scenario: Accepted attachments are streamed into quarantine under a bound
    Given a submission includes one or more files parts
    When the API accepts an attachment
    Then the API mints an opaque server-side filename/key
    And streams at most 50 MB into the quarantine compartment while computing the actual byte count and inspecting its signature
    And never buffers the whole file in memory
    And never persists or logs the client filename

  Scenario: A valid submission is persisted atomically
    Given a multipart submission passes every validation step
    When the API commits the submission
    Then one database transaction creates the report and consent projection, one answer per shown answer-producing revision including skips, report-file metadata for successfully quarantined blobs, one summarization outbox item, and one independent attachment-processing outbox item per file

  Scenario: A failed transaction leaves no visible report and no leaked blobs
    Given the persistence transaction for a submission fails
    When the API returns from the failed request
    Then no report is visible
    And any already-written quarantine blobs are unreferenced and expire through the storage lifecycle rule

  Scenario: A successful submission returns an opaque accepted receipt
    Given a submission passes validation and persists successfully
    When the API responds
    Then the response is 202 Accepted with an opaque report ID and the status "submitted"
    And the response contains no raw answers or attachment URLs

  Scenario: The UI prevents duplicate submission while a request is in flight
    Given a reporter has just submitted the form
    When the request is still in flight
    Then the UI disables repeat submission
    And retains local state if the network result is uncertain

  Scenario: Submission is rejected without valid abuse-control checks
    Given a submission request arrives
    When Turnstile verification fails, or Turnstile is required but unavailable or misconfigured, or the per-IP rate limit is exceeded
    Then the API rejects the request
    And a rate-limited request receives 429 with a safe retry signal
    And the client IP used for rate limiting comes only from explicitly trusted proxy headers and is never stored on the report
