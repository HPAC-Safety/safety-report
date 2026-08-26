Feature: Domain and lifecycle
  A report moves through a fixed set of states from submission to
  publication, and soft deletion can remove it from that lifecycle at any
  point.

  Scenario Outline: A report follows the defined lifecycle transitions
    Given a report is in state <from>
    When <event> occurs
    Then the report moves to state <to>

    Examples:
      | from          | event                                          | to            |
      | Submitted     | Worker claims the summary job                  | Summarizing   |
      | Summarizing   | a valid bilingual pair is saved                | PendingReview |
      | Summarizing   | bounded retries are exhausted                  | SummaryFailed |
      | SummaryFailed | an officer writes both texts                   | PendingReview |
      | PendingReview | either summary text is edited                  | PendingReview |
      | PendingReview | an officer approves the pair                   | Approved      |
      | PendingReview | an officer rejects the report                  | Rejected      |
      | Approved      | either summary text is edited                  | PendingReview |
      | Approved      | consent is yes and the report is not deleted   | Published     |
      | Published     | either summary text is edited                  | PendingReview |

  Scenario: SummaryFailed remains visible to safety officers
    Given a report's summarization retries are exhausted
    When the report becomes SummaryFailed
    Then it remains visible in the safety officer review queue
    And it does not disappear because AI processing failed

  Scenario: A report is publishable only when every invariant holds
    Given a report and its summary row are not deleted
    And ConsentPublish is exactly true
    And both English and French summary texts are nonblank
    And the pair has a current human approval
    And the report has not been rejected
    When the public query evaluates the report
    Then the report is publishable

  Scenario Outline: A report is not publishable when one invariant fails
    Given a report otherwise satisfies every publication invariant
    But <violation>
    When the public query evaluates the report
    Then the report is not publishable

    Examples:
      | violation                                    |
      | the report or summary row is deleted         |
      | ConsentPublish is not exactly true            |
      | the English or French summary text is blank   |
      | the pair has no current human approval        |
      | the report has been rejected                  |

  Scenario: Editing a summary text unpublishes the report
    Given a report is Published
    When either the English or French summary text is edited
    Then the pair's approver and approval timestamp are cleared
    And the report immediately stops satisfying the publication invariant

  Scenario: Negative consent still allows internal review
    Given a reporter has not consented to publication
    When the report is summarized and reviewed
    Then internal summarization and safety review proceed normally
    And the report can never satisfy the public query

  Scenario: Soft deletion removes a report from every normal path
    Given a report exists in any lifecycle state
    When a safety officer soft-deletes it
    Then one application transaction stamps the same deleted timestamp on the report and all owned/dependent rows: answers, summary, files, and report outbox items
    And an immutable audit entry is recorded
    And pending Worker work for the report stops, and the Worker rechecks deletion before committing output
    And public and normal admin queries hide the report immediately
    And there is no restore transition

  Scenario: Deleting an admin user revokes access but preserves history
    Given an admin user is soft-deleted
    When the deletion transaction commits
    Then that admin's authorization is revoked
    And historical audit rows remain and may still reference that admin's ID

  Scenario: A question revision can be deleted only when unreferenced
    Given a question revision is referenced by no answer, including answers on deleted reports
    When an Administrator deletes it
    Then the revision and its option children are stamped with one deleted timestamp
    And once any answer references a revision, that revision is never deletable again

  Scenario: Raw reports are retained until explicit deletion
    Given a report has been submitted
    When no safety officer has deleted it
    Then the report is retained indefinitely
    And there is no scheduled report purge and no physical-delete path in the application

  Scenario: Soft-deleted and private data remain under managed retention
    Given a report has been soft-deleted, or a question revision has a private original or derivative
    When that data is no longer reachable through normal application paths
    Then it remains under managed storage/database retention rather than being purged
    And backups of that data follow infrastructure policy

  Scenario: Unreferenced quarantine objects expire without affecting reports
    Given a multipart request fails or is abandoned before the transaction commits
    When the resulting quarantine objects are never referenced by a report
    Then those objects may expire automatically through storage lifecycle rules
    And that operational cleanup does not change report retention

  Scenario Outline: An audited action is recorded in the immutable audit log
    Given <action> occurs
    When the action completes
    Then an audit log entry records identifiers and action metadata
    And it never contains raw answers, names, credentials, tokens, or client filenames

    Examples:
      | action                                                    |
      | an authentication outcome that matters to authorization   |
      | a question revision is created or deleted                 |
      | an admin allowlist or role change                         |
      | a report is deleted                                       |
      | summary generation fails                                  |
      | a summary is manually edited                               |
      | a summary pair is approved                                 |
      | a report is rejected                                       |
      | a report is published                                      |
