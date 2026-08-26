Feature: AI anonymization
  The Worker makes exactly one versioned model call per summary attempt. That
  call both summarizes and anonymizes the report and returns one
  English/French summary pair.

  Scenario: Exactly one model call summarizes and anonymizes a report
    Given a report has been submitted and its summarization outbox item is due
    When the Worker processes the summarization attempt
    Then the Worker makes exactly one call to the model
    And that call produces both the English and French summary texts
    And no deterministic text scrubber, separate redaction pass, PII-audit
      call, or translation call runs

  Scenario: Only eligible, labeled fields reach the model
    Given a report has non-private answered fields and private answered fields
    When the Worker builds the model input DTO
    Then report_content contains only non-private answered fields eligible to
      contribute facts
    And private_context contains only private answered fields, supplied to
      help recognize identifying details that recur in report content
    And skipped/null answers, the system consent answer, and file-upload
      answers are excluded from both arrays
    And the DTO contains no attachment bytes, document text, storage keys,
      admin data, audit data, deleted content, or client filenames

  Scenario: A fact appearing only in private context is never summarized
    Given a fact exists only in private_context and nowhere in report_content
    When the model produces a summary
    Then that fact does not appear in either summary text

  Scenario: The Worker accepts only the exact two-field JSON response
    Given the model returns a response for a summarization attempt
    When the Worker validates the response
    Then a response with exactly two nonblank string fields
      "ai_summary_en" and "ai_summary_fr" is accepted
    And a response with a Markdown fence, commentary, an extra key, a null
      field, or only one language is rejected

  Scenario: A private person's identity is replaced with their role
    Given a private pilot's name is repeated in a report's narrative
    When the model produces the anonymized summary
    Then every occurrence of that identity becomes exactly "the pilot" in the
      English summary and "le pilote" in the French summary
    And no first name, surname, initials, fragment, hash, bracket, or generic
      numbered placeholder remains

  Scenario Outline: An identifying category is never disclosed in a summary
    Given a report's eligible content contains <category>
    When the model produces the anonymized summary
    Then <category> does not appear in either summary text

    Examples:
      | category                                                         |
      | a name, initial, membership number, email, phone number, address, or account identifier |
      | an exact site, coordinates, or uniquely identifying location description |
      | an aircraft manufacturer or model                                |
      | a filename, attachment/document content, or hidden metadata      |

  Scenario: A private-only fact is never added merely for completeness
    Given a private fact would make the narrative more complete
    And that fact is not otherwise eligible summary content
    When the model produces the summary
    Then the fact is not added to either summary text

  Scenario: Documents never reach the model
    Given a report has document attachments
    When the Worker builds the summarization input
    Then no document or document-derived text is sent to the model
    And document text is not extracted, summarized, translated, or anonymized

  Scenario: A valid response is persisted as one pair-level summary row
    Given the model returns a valid two-field response
    When the Worker persists it
    Then one summary row is created or replaced with AiSummaryEn, AiSummaryFr,
      shared model and prompt_version provenance, and creation/update
      timestamps
    And no separate row is created per locale

  Scenario: Editing either summary text clears pair approval
    Given a summary pair has been approved
    When a safety officer edits either the English or French text
    Then ApprovedBy and ApprovedAt are cleared
    And the officer reviews and approves the pair as a whole, never one
      language independently

  Scenario: Retries repeat the single-call operation without adding stages
    Given a summarization attempt fails with a transient provider error or
      invalid output
    When the outbox retries the attempt within its bounded budget
    Then the retry repeats the single model call
    And no repair or audit call is added

  Scenario: Exhausted retries surface a manually authorable failure
    Given a report's summarization retry budget is exhausted
    When the Worker gives up on the attempt
    Then the report becomes SummaryFailed with a safe operational error
    And the report appears in the review queue
    And a human can author both summary texts manually and continue review

  Scenario: Sensitive summarization data is never logged
    Given a summarization attempt runs, succeeds, or fails
    When the Worker emits application logs
    Then prompts, model responses, private context, and raw report content
      are never written to those logs
