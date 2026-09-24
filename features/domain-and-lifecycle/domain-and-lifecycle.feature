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
  | from          | event                                           | to            |
  | Submitted     | Worker claims the summary job                   | Summarizing   |
  | Summarizing   | a valid bilingual pair is saved                 | PendingReview |
  | Summarizing   | bounded retries are exhausted                   | SummaryFailed |
  | SummaryFailed | an officer writes both texts                    | PendingReview |
  | PendingReview | either summary text is edited                   | PendingReview |
  | PendingReview | an officer approves the pair and consent is yes | Published     |
  | PendingReview | an officer approves the pair and consent is no  | Approved      |
  | PendingReview | an officer rejects the report                   | Rejected      |
  | Approved      | either summary text is edited                   | PendingReview |
  | Published     | either summary text is edited                   | PendingReview |
  | Published     | an officer unpublishes the report               | PendingReview |
  | Rejected      | an officer reopens the report                   | PendingReview |

@REQ-DOM-014
Scenario Outline: A review action outside its states is refused and changes nothing
  Given a report is in state <from>
  When an officer tries to <action>
  Then the action is refused
  And the report stays in state <from>

Examples:
  | from          | action               |
  | Submitted     | approve the pair     |
  | SummaryFailed | approve the pair     |
  | Rejected      | approve the pair     |
  | Rejected      | edit a summary text  |
  | Published     | reject the report    |
  | PendingReview | reopen the report    |
  | PendingReview | unpublish the report |
  | PendingReview | write a manual pair  |

@REQ-DOM-002
@ignore
Scenario: SummaryFailed remains visible to safety officers
  Given a report's summarization retries are exhausted
  When the report becomes SummaryFailed
  Then it remains visible in the safety officer review queue
  And it does not disappear because AI processing failed

@REQ-DOM-003
Scenario: A report is publishable only when every invariant holds
  Given a report and its summary row are not deleted
  And ConsentPublish is exactly true
  And both English and French summary texts are nonblank
  And the pair has a current human approval
  And the report has not been rejected
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
  | the report has been rejected                |

@REQ-DOM-005
Scenario: Editing a summary text unpublishes the report
  Given a report is Published
  When either the English or French summary text is edited
  Then the pair's approver subject and approval timestamp are cleared
  And the report immediately stops satisfying the publication invariant

@REQ-DOM-006
Scenario: A report without publication consent is never summarized
  Given a report whose reporter did not consent to publication is due for summarization
  When the Worker processes its summarization attempt
  Then no model call is made
  And the report goes to Pending review with no summary
  And the report can never satisfy the public query

@REQ-DOM-007
Scenario: Soft deletion removes a report from every normal path
  Given a report exists in any lifecycle state
  When a safety officer soft-deletes it
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
  When no safety officer has deleted it
  Then the report is retained indefinitely
  And there is no scheduled report purge and no physical-delete path in the application

@REQ-DOM-011
@ignore
Scenario: Soft-deleted and private data remain under managed retention
  Given a report has been soft-deleted, or a question revision has a private original or derivative
  When that data is no longer reachable through normal application paths
  Then it remains under managed storage/database retention rather than being purged
  And backups of that data follow infrastructure policy

@REQ-DOM-012
@ignore
Scenario: Unreferenced quarantine objects expire without affecting reports
  Given a submission fails before its transaction commits, or an upload is never claimed
  When the resulting quarantine objects are never referenced by a report
  Then those objects may expire automatically through storage lifecycle rules
  And that operational cleanup does not change report retention

@REQ-DOM-013
@ignore
Scenario Outline: An audited action is recorded in the immutable audit log
  Given <action> occurs
  When the action completes
  Then an audit log entry records the acting token subject and action metadata
  And the subject is an opaque string that joins to no user record
  And it never contains raw answers, names, credentials, tokens, or client filenames

Examples:
  | action                                                    |
  | an authorization denial that matters to a privileged path |
  | a question revision is created or deleted                 |
  | a report is deleted                                       |
  | summary generation fails                                  |
  | a summary is manually edited                              |
  | a summary pair is approved                                |
  | a report is rejected                                      |
  | a report is published                                     |
