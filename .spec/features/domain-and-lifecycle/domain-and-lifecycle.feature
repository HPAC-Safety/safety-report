Feature: Domain and lifecycle
A report moves through a fixed set of states from submission to
publication, and deletion can remove it from that lifecycle at any
point.

@REQ-DOM-001
Scenario Outline: A report follows the defined lifecycle transitions
  Given a report is in state <from>
  When <event> occurs
  Then the report moves to state <to>

Examples:
  | from           | event                                            | to             |
  | Submitted      | Worker claims the summary job and consent is yes | Summarizing    |
  | Submitted      | Worker claims the summary job and consent is no  | Unpublished    |
  | Summarizing    | a valid summary pair is saved                    | Pending        |
  | Summarizing    | bounded retries are exhausted                    | Summary failed |
  | Summary failed | an officer writes both texts                     | Pending        |
  | Pending        | either summary text is edited                    | Pending        |
  | Pending        | an officer publishes the pair                    | Published      |
  | Pending        | an officer unpublishes the report                | Unpublished    |
  | Published      | either summary text is edited                    | Published      |
  | Published      | an officer unpublishes the report                | Unpublished    |
  | Unpublished    | an officer publishes the pair                    | Published      |
  | Unpublished    | either summary text is edited                    | Pending        |

@REQ-DOM-014
Scenario Outline: A review action outside its states is refused and changes nothing
  Given a report is in state <from>
  When an officer tries to <action>
  Then the action is refused
  And the report stays in state <from>

Examples:
  | from           | action               |
  | Submitted      | publish the pair     |
  | Summarizing    | unpublish the report |
  | Summary failed | publish the pair     |
  | Published      | publish the pair     |
  | Unpublished    | unpublish the report |
  | Pending        | write a manual pair  |
  | Published      | write a manual pair  |

@REQ-DOM-015
Scenario Outline: A report without publication consent is unpublished for good
  Given a report whose reporter did not consent to publication is Unpublished
  When an officer tries to <action>
  Then the action is refused
  And the report stays Unpublished with nothing changed
  And deletion is still the one thing an officer can do to it (REQ-DOM-007)

Examples:
  | action               |
  | publish the pair     |
  | edit a summary text  |
  | write a manual pair  |
  | unpublish the report |

@REQ-DOM-003
Scenario: A report is publishable only when every invariant holds
  Given a report and its summary row are not deleted
  And the reporter's publication consent is exactly yes
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
  | the publication consent is not exactly yes  |
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
Scenario: Deletion removes a report from every normal path
  Given a report exists in any lifecycle state
  When a Safety Officer deletes it
  Then one application transaction marks the report and everything it owns deleted with one deletion time: answers, summary, attachments, and Worker jobs
  And an immutable audit entry is recorded
  And pending Worker work for the report stops, and the Worker rechecks deletion before committing output
  And public and normal admin queries hide the report immediately
  And there is no restore transition

@REQ-DOM-008
Scenario: A question revision can be deleted only when unreferenced
  Given a question revision is referenced by no answer, including answers on deleted reports
  When an Administrator deletes that revision
  Then the revision is marked deleted
  And once any answer references a revision, that revision is never deletable again

@REQ-DOM-009
Scenario: Retiring a question is a deletion with no way back
  Given a question is deleted by an Administrator, or retired by being replaced through an edit
  When the deletion is committed
  Then the question keeps its row, marked deleted
  And its revisions, choices, and every answer given to it are untouched
  And there is no restore transition

@REQ-DOM-010
Scenario: Raw reports are retained until explicit deletion
  Given a synthetic report has been submitted
  When no Safety Officer has deleted it
  Then the report is retained indefinitely
  And there is no scheduled report purge and no physical-delete path in the application

@REQ-DOM-011
Scenario: Deleting a report keeps its row and its stored files
  Given a synthetic report with an attachment has been submitted
  When a Safety Officer deletes the report
  Then the report row remains, marked deleted
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
Scenario: An operator requeues a poisoned Worker job
  Given a Worker job has reached the poison threshold and stopped retrying
  When the Worker is invoked with a requeue-poison payload
  Then the job's poison state is cleared and its attempt count resets
  And it becomes claimable again immediately
  And only the requeued count and the job's own identifier are logged, never its payload

@REQ-DOM-017
Scenario: A poison-requeue payload naming a time window only requeues jobs poisoned within it
  Given one Worker job was poisoned before the given window and another was poisoned within it
  When the Worker is invoked with a requeue-poison payload naming that window
  Then only the job poisoned within the window is requeued
  And the job poisoned before the window is left poisoned

@REQ-DOM-018
Scenario Outline: What a reporter answered cannot be changed once stored
  Given a submitted report with answers, a file, and a summary
  When a stored answer's <part> is <change>
  Then the change is refused, naming that part
  And the answer is as it was

Examples:
  | part              | change  |
  | identifier        | changed |
  | report            | changed |
  | question          | changed |
  | question revision | changed |
  | question key      | changed |
  | privacy           | changed |
  | wording           | changed |
  | wording           | cleared |
  | yes or no         | changed |
  | choice            | changed |
  | language          | changed |
  | translation need  | changed |
  | answer time       | changed |

@REQ-DOM-019
Scenario Outline: An answer's second language and its deletion time are written once
  Given a submitted report with answers, a file, and a summary
  And a stored answer's <part> has been written
  When it is <change>
  Then the change is refused, naming that part
  And the answer is as it was

Examples:
  | part                   | change  |
  | second language        | changed |
  | second language        | cleared |
  | second language source | changed |
  | second language source | cleared |
  | deletion time          | cleared |
  | deletion time          | changed |

@REQ-DOM-020
Scenario Outline: What an attachment arrived as cannot be changed once stored
  Given a submitted report with answers, a file, and a summary
  When a stored attachment's <part> is <change>
  Then the change is refused, naming that part
  And the attachment is as it was

Examples:
  | part             | change  |
  | identifier       | changed |
  | report           | changed |
  | answer           | changed |
  | kind             | changed |
  | stored original  | changed |
  | file name        | changed |
  | content type     | changed |
  | size             | changed |
  | upload time      | changed |

@REQ-DOM-021
Scenario Outline: What the Worker and a reviewer record about an attachment stays writable
  Given a submitted report with answers, a file, and a summary
  When a stored attachment's <part> is <change>
  Then the change is kept
  And the attachment now reads differently

Examples:
  | part             | change  |
  | stripped copy    | written |
  | stripped copy    | cleared |
  | processing error | written |
  | hidden state     | written |
  | deletion time    | written |

@REQ-DOM-022
Scenario Outline: A report's language, submission time, and consent cannot be changed once stored
  Given a submitted report with answers, a file, and a summary
  When a stored report's <part> is <change>
  Then the change is refused, naming that part
  And the report is as it was

Examples:
  | part                 | change  |
  | identifier           | changed |
  | language             | changed |
  | submission time      | changed |
  | publication consent  | changed |
  | publication consent  | cleared |
  | media consent        | changed |
  | document consent     | changed |

@REQ-DOM-023
Scenario Outline: A report's review state and its deletion time stay writable
  Given a submitted report with answers, a file, and a summary
  When a stored report's <part> is <change>
  Then the change is kept
  And the report now reads differently

Examples:
  | part             | change  |
  | review state     | changed |
  | publish time     | written |
  | unpublish note   | written |
  | summary error    | written |
  | deletion time    | written |

@REQ-DOM-024
Scenario Outline: A saved summary revision cannot be changed
  Given a submitted report with answers, a file, and a summary
  When a stored summary revision's <part> is <change>
  Then the change is refused, naming that part
  And the summary revision is as it was

Examples:
  | part              | change  |
  | identifier        | changed |
  | summary           | changed |
  | number            | changed |
  | English text      | changed |
  | French text       | changed |
  | English source    | changed |
  | French source     | changed |
  | model             | changed |
  | prompt version    | changed |
  | author            | changed |
  | creation time     | changed |
  | restored revision | changed |

@REQ-DOM-025
Scenario Outline: A revision's approval may be set and cleared, and it may be marked deleted
  Given a submitted report with answers, a file, and a summary
  When a stored summary revision's <part> is <change>
  Then the change is kept
  And the summary revision now reads differently

Examples:
  | part          | change  |
  | approval      | cleared |
  | approval      | written |
  | deletion time | written |

@REQ-DOM-026
Scenario: A revision's deletion time is written once
  Given a submitted report with answers, a file, and a summary
  And a stored summary revision's deletion time has been written
  When it is cleared
  Then the change is refused, naming that part
  And the summary revision is as it was

@REQ-DOM-027
Scenario Outline: A report, an answer, an attachment, or a summary revision is never erased
  Given a submitted report with answers, a file, and a summary
  When a stored <record> is erased
  Then the erasure is refused, saying that kind of record is never erased
  And the <record> is as it was

Examples:
  | record           |
  | report           |
  | answer           |
  | attachment       |
  | summary revision |

@REQ-DOM-030
Scenario Outline: Reports, answers, attachments, and summary revisions are never erased all at once
  Given a submitted report with answers, a file, and a summary
  When every stored <record> is erased at once
  Then the erasure is refused, saying that kind of record is never erased
  And the <record> is as it was

Examples:
  | record           |
  | report           |
  | answer           |
  | attachment       |
  | summary revision |

@REQ-DOM-028
Scenario: Writing a locked part's own value back is not a change
  Given a submitted report with answers, a file, and a summary
  When a stored report's language and submission time are written back unchanged, with a new review state
  Then the change is kept

@REQ-DOM-029
Scenario: A migration that must change a locked part lifts the guard inside its own transaction
  Given a submitted report with answers, a file, and a summary
  When a migration lifts the report's guard, changes its language to French, and restores the guard in one transaction
  Then the change is kept
  And the report's language is French
  And a later change to the report's language is refused, naming that part
