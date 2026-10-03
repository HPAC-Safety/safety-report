Feature: Domain and lifecycle
A report moves through a fixed set of states from submission to
publication, and soft deletion can remove it from that lifecycle at any
point.

@REQ-DOM-001
Scenario Outline: A report follows the defined lifecycle transitions
  Given a report is in state <from>
  When <event> occurs
  Then the report moves to state <to>

Examples:
  | from          | event                                            | to            |
  | Submitted     | Worker claims the summary job and consent is yes | Summarizing   |
  | Submitted     | Worker claims the summary job and consent is no  | Unpublished   |
  | Summarizing   | a valid summary pair is saved                  | Pending       |
  | Summarizing   | bounded retries are exhausted                    | Summary failed |
  | Summary failed | an officer writes both texts                     | Pending       |
  | Pending       | either summary text is edited                    | Pending       |
  | Pending       | an officer publishes the pair                    | Published     |
  | Pending       | an officer unpublishes the report                | Unpublished   |
  | Published     | either summary text is edited                    | Published     |
  | Published     | an officer unpublishes the report                | Unpublished   |
  | Unpublished   | an officer publishes the pair                    | Published     |
  | Unpublished   | either summary text is edited                    | Pending       |

@REQ-DOM-014
Scenario Outline: A review action outside its states is refused and changes nothing
  Given a report is in state <from>
  When an officer tries to <action>
  Then the action is refused
  And the report stays in state <from>

Examples:
  | from          | action               |
  | Submitted     | publish the pair     |
  | Summarizing   | unpublish the report |
  | Summary failed | publish the pair     |
  | Published     | publish the pair     |
  | Unpublished   | unpublish the report |
  | Pending       | write a manual pair  |
  | Published     | write a manual pair  |

@REQ-DOM-015
Scenario Outline: A report without publication consent is unpublished for good
  Given a report whose reporter did not consent to publication is Unpublished
  When an officer tries to <action>
  Then the action is refused
  And the report stays Unpublished with nothing changed
  And soft deletion is still the one thing an officer can do to it (REQ-DOM-007)

Examples:
  | action               |
  | publish the pair     |
  | edit a summary text  |
  | write a manual pair  |
  | unpublish the report |

@REQ-DOM-003
Scenario: A report is publishable only when every invariant holds
  Given a report and its summary row are not deleted
  And ConsentPublish is exactly true
  And both English and French summary texts are nonblank
  And the pair has a current human approval
  And the report is Published
  When the public query evaluates the report
  Then the report is publishable

@REQ-DOM-004
Scenario Outline: A report is not publishable when one invariant fails
  Given a report otherwise satisfies every publication invariant
  But <violation>
  When the public query evaluates the report
  Then the report is not publishable

Examples:
  | violation                                   |
  | the report or summary row is deleted        |
  | ConsentPublish is not exactly true          |
  | the English or French summary text is blank |
  | the pair has no current human approval      |
  | the report is not Published                 |

@REQ-DOM-005
Scenario: Editing the summary of a Published report publishes the new revision at once
  Given a report is Published
  When either the English or French summary text is edited
  Then the new revision is approved by its editor at once
  And the report still satisfies the publication invariant with the new text

@REQ-DOM-006
Scenario: A report without publication consent is never summarized
  Given a report whose reporter did not consent to publication is due for summarization
  When the Worker processes its summarization attempt
  Then no model call is made
  And the report goes to Unpublished with no summary
  And the report can never satisfy the public query

@REQ-DOM-007
Scenario: Soft deletion removes a report from every normal path
  Given a report exists in any lifecycle state
  When a Safety Officer soft-deletes it
  Then one application transaction stamps the same deleted timestamp on the report and all owned and dependent rows: answers, summary, files, and report outbox items
  And an immutable audit entry is recorded
  And pending Worker work for the report stops, and the Worker rechecks deletion before committing output
  And public and normal admin queries hide the report immediately
  And there is no restore transition

@REQ-DOM-008
Scenario: A question revision can be deleted only when unreferenced
  Given a question revision is referenced by no answer, including answers on deleted reports
  When an Administrator deletes that revision
  Then the revision is stamped with a deleted timestamp
  And once any answer references a revision, that revision is never deletable again

@REQ-DOM-009
Scenario: Retiring a question is a soft delete with no way back
  Given a question is retired, either by an Administrator or by being replaced through an edit
  When the deletion is committed
  Then the question is stamped with a deleted timestamp rather than removed
  And its revisions, choices, and every answer given to it are untouched
  And there is no restore transition

@REQ-DOM-010
Scenario: Raw reports are retained until explicit deletion
  Given a synthetic report has been submitted
  When no Safety Officer has deleted it
  Then the report is retained indefinitely
  And there is no scheduled report purge and no physical-delete path in the application

@REQ-DOM-011
Scenario: Soft-deleting a report keeps its row and its stored files
  Given a synthetic report with an attachment has been submitted
  When a Safety Officer soft-deletes the report
  Then the report row remains, stamped with a deleted timestamp
  And its answers, files, and stored objects remain
  And no application path removes them afterwards

@REQ-DOM-013
Scenario Outline: An audited action is recorded in the immutable audit log
  Given <action> occurs
  When the action completes
  Then an audit log entry records the acting token subject and action metadata
  And the token subject is an opaque string that joins to no user record
  And it never contains raw answers, names, credentials, tokens, or client filenames

Examples:
  | action                                  |
  | a question is created                   |
  | a question is revised                   |
  | a question is deleted                   |
  | a question revision is deleted          |
  | a report is deleted                     |
  | a summary is edited                     |
  | a summary is rolled back                |
  | a report is published                   |
  | a report is unpublished                 |

@REQ-DOM-016
Scenario: An operator requeues poisoned outbox work
  Given an outbox message has reached the poison threshold and stopped retrying
  When the Worker is invoked with a requeue-poison payload
  Then the message's poison state is cleared and its attempt count resets
  And it becomes claimable again immediately
  And only the requeued count and the message's own identifier are logged, never its payload

@REQ-DOM-017
Scenario: A poison-requeue payload naming a time window only requeues messages poisoned within it
  Given one outbox message was poisoned before the given window and another was poisoned within it
  When the Worker is invoked with a requeue-poison payload naming that window
  Then only the message poisoned within the window is requeued
  And the message poisoned before the window is left poisoned

@REQ-DOM-018
Scenario Outline: The database refuses a change to what a reporter answered
  Given a submitted report with answers, a file, and a summary
  When a statement sets <assignment> on a report_answers row
  Then Postgres refuses it, naming <column>
  And the row is as it was

Examples:
  | column                              | assignment                                   |
  | report_answers.id                   | id = 'xxxxxxxxxx1'                           |
  | report_answers.report_id            | report_id = 'xxxxxxxxxx1'                    |
  | report_answers.question_id          | question_id = 'xxxxxxxxxx1'                  |
  | report_answers.question_revision_id | question_revision_id = 'xxxxxxxxxx1'         |
  | report_answers.question_key         | question_key = 'another_key'                 |
  | report_answers.is_private           | is_private = NOT is_private                  |
  | report_answers.value                | value = 'A different account.'               |
  | report_answers.value                | value = NULL                                 |
  | report_answers.value_boolean        | value_boolean = true                         |
  | report_answers.choice_id            | choice_id = 'xxxxxxxxxx1'                    |
  | report_answers.locale               | locale = 'fr-CA'                             |
  | report_answers.translation_mode     | translation_mode = 'machine'                 |
  | report_answers.answered_at          | answered_at = answered_at + interval '1 day' |

@REQ-DOM-019
Scenario Outline: An answer's second language and its deletion stamp are written once
  Given a submitted report with answers, a file, and a summary
  And a statement has set <first> on a report_answers row
  When a statement sets <second> on that row
  Then Postgres refuses it, naming <column>
  And the row is as it was

Examples:
  | column                            | first                               | second                                   |
  | report_answers.translated_value   | translated_value = 'First.'         | translated_value = 'Second.'             |
  | report_answers.translated_value   | translated_value = 'First.'         | translated_value = NULL                  |
  | report_answers.translation_source | translation_source = 'auto'         | translation_source = 'human'             |
  | report_answers.translation_source | translation_source = 'auto'         | translation_source = NULL                |
  | report_answers.deleted            | deleted = now()                     | deleted = NULL                           |
  | report_answers.deleted            | deleted = now()                     | deleted = now() + interval '1 day'       |

@REQ-DOM-020
Scenario Outline: The database refuses a change to what an attachment arrived as
  Given a submitted report with answers, a file, and a summary
  When a statement sets <assignment> on a report_files row
  Then Postgres refuses it, naming <column>
  And the row is as it was

Examples:
  | column                          | assignment                                   |
  | report_files.id                 | id = 'xxxxxxxxxx1'                           |
  | report_files.report_id          | report_id = 'xxxxxxxxxx1'                    |
  | report_files.report_answer_id   | report_answer_id = 'xxxxxxxxxx1'             |
  | report_files.kind               | kind = 'video'                               |
  | report_files.blob_key           | blob_key = 'another/original/key'            |
  | report_files.original_file_name | original_file_name = 'another.jpg'           |
  | report_files.content_type       | content_type = 'image/png'                   |
  | report_files.byte_size          | byte_size = byte_size + 1                    |
  | report_files.uploaded_at        | uploaded_at = uploaded_at + interval '1 day' |

@REQ-DOM-021
Scenario Outline: What the Worker and a reviewer record about an attachment stays writable
  Given a submitted report with answers, a file, and a summary
  When a statement sets <assignment> on a report_files row
  Then the write succeeds
  And the row now reads differently

Examples:
  | assignment                                                            |
  | stripped_blob_key = 'report/stripped/other', exif_stripped_at = now() |
  | exif_stripped_at = NULL, stripped_blob_key = NULL                     |
  | processing_error_code = 'unreadable'                                  |
  | hidden_at = now(), hidden_by_subject = 'synthetic-officer'            |
  | deleted = now()                                                       |

@REQ-DOM-022
Scenario Outline: The database refuses a change to a report's language, submission time, or consent
  Given a submitted report with answers, a file, and a summary
  When a statement sets <assignment> on a reports row
  Then Postgres refuses it, naming <column>
  And the row is as it was

Examples:
  | column                    | assignment                                     |
  | reports.id                | id = 'xxxxxxxxxx1'                             |
  | reports.language          | language = 'fr-CA'                             |
  | reports.submitted_at      | submitted_at = submitted_at + interval '1 day' |
  | reports.consent_publish   | consent_publish = NOT consent_publish          |
  | reports.consent_publish   | consent_publish = NULL                         |
  | reports.consent_media     | consent_media = true                           |
  | reports.consent_documents | consent_documents = true                       |

@REQ-DOM-023
Scenario Outline: A report's review state and its deletion stamp stay writable
  Given a submitted report with answers, a file, and a summary
  When a statement sets <assignment> on a reports row
  Then the write succeeds
  And the row now reads differently

Examples:
  | assignment                          |
  | status = 'unpublished'              |
  | published_at = now()                |
  | unpublish_note = 'Out of scope.'    |
  | summary_error = 'Provider was down' |
  | deleted = now()                     |

@REQ-DOM-024
Scenario Outline: The database refuses a change to a saved summary revision
  Given a submitted report with answers, a file, and a summary
  When a statement sets <assignment> on a summary_revisions row
  Then Postgres refuses it, naming <column>
  And the row is as it was

Examples:
  | column                             | assignment                                 |
  | summary_revisions.id               | id = 'xxxxxxxxxx1'                         |
  | summary_revisions.summary_id       | summary_id = 'xxxxxxxxxx1'                 |
  | summary_revisions.sequence         | sequence = sequence + 1                    |
  | summary_revisions.ai_summary_en    | ai_summary_en = 'Rewritten.'               |
  | summary_revisions.ai_summary_fr    | ai_summary_fr = 'Réécrit.'                 |
  | summary_revisions.source_en        | source_en = 'human'                        |
  | summary_revisions.source_fr        | source_fr = 'human'                        |
  | summary_revisions.model            | model = 'another-model'                    |
  | summary_revisions.prompt_version   | prompt_version = 'another.v9'              |
  | summary_revisions.author_subject   | author_subject = 'someone-else'            |
  | summary_revisions.created_at       | created_at = created_at + interval '1 day' |
  | summary_revisions.restored_from_id | restored_from_id = 'xxxxxxxxxx1'           |

@REQ-DOM-025
Scenario Outline: A revision's approval may be set and cleared, and it may be stamped deleted
  Given a submitted report with answers, a file, and a summary
  When a statement sets <assignment> on a summary_revisions row
  Then the write succeeds
  And the row now reads differently

Examples:
  | assignment                                                    |
  | approved_at = NULL, approved_by_subject = NULL                |
  | approved_at = now(), approved_by_subject = 'another-approver' |
  | deleted = now()                                               |

@REQ-DOM-026
Scenario: A revision's deletion stamp is written once
  Given a submitted report with answers, a file, and a summary
  And a statement has set deleted = now() on a summary_revisions row
  When a statement sets deleted = NULL on that row
  Then Postgres refuses it, naming summary_revisions.deleted
  And the row is as it was

@REQ-DOM-027
Scenario Outline: The database never deletes a report, an answer, a file, or a summary revision
  Given a submitted report with answers, a file, and a summary
  When a statement deletes a <table> row
  Then Postgres refuses it, saying <table> rows are never deleted
  And the row is as it was

Examples:
  | table             |
  | reports           |
  | report_answers    |
  | report_files      |
  | summary_revisions |

@REQ-DOM-030
Scenario Outline: The database never truncates a report, an answer, a file, or a summary revision
  Given a submitted report with answers, a file, and a summary
  When a statement truncates <table>
  Then Postgres refuses it, saying <table> rows are never deleted
  And the row is as it was

Examples:
  | table             |
  | reports           |
  | report_answers    |
  | report_files      |
  | summary_revisions |

@REQ-DOM-028
Scenario: A statement that leaves a locked column as it was is not a change
  Given a submitted report with answers, a file, and a summary
  When a statement sets language = language, submitted_at = submitted_at, status = 'unpublished' on a reports row
  Then the write succeeds

@REQ-DOM-029
Scenario: A migration that must change a locked column disables the trigger inside its own transaction
  Given a submitted report with answers, a file, and a summary
  When a migration disables the reports trigger, sets language = 'fr-CA', and enables it again in one transaction
  Then the write succeeds
  And the report's language is fr-CA
  And a later statement setting language = 'en-CA' on a reports row is refused, naming reports.language
