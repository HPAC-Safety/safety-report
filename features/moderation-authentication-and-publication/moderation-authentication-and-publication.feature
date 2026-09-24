Feature: Moderation, authentication, and publication
Members present a signed token, get one of three roles from its claims,
review reports, and only a fully approved, consented, non-deleted report ever
reaches the public feed.

@REQ-MOD-001
@ui
Scenario: In development the login page offers no third-party sign-in option
  Given a visitor activates the member-login action
  Then the login page shows a username field, a password field, and a login action
  And the login page shows no third-party sign-in option

@REQ-MOD-002
@ui
Scenario: Where a third-party provider is configured, the login page offers it
  Given the API reports that a third-party provider is configured
  When a visitor activates the member-login action
  Then the login page also shows a third-party sign-in option

@REQ-MOD-003
@ui
Scenario: Signing in with member credentials returns a session that survives a reload
  Given a visitor signs in with valid member credentials
  Then the header shows a logout action instead of the member-login action
  When the page reloads
  Then the header still shows the logout action

@REQ-MOD-004
@ui
Scenario: Bad credentials show one generic failure and no session
  Given a visitor submits credentials that are not valid
  Then the login page shows one generic failure message
  And the failure does not say whether the username or the password was wrong
  And the header still shows the member-login action

@REQ-MOD-005
Scenario: Repeated sign-in attempts for one identity are rate limited
  Given repeated sign-in attempts arrive for the same username
  When the sign-in rate limit for that identity is exceeded
  Then the API rejects further attempts with 429 and a safe retry signal
  And the rejection does not reveal whether any attempted username or password was valid

@REQ-MOD-006
@ui
Scenario: A member's signed-in session persists across a reload and clears on logout
  Given a visitor signs in from the member login page
  Then the header shows a logout action instead of the member-login action
  When the page reloads
  Then the header still shows the logout action
  When the visitor activates the logout action
  Then the header shows the member-login action again

@REQ-MOD-007
@ui
Scenario: A signed-in Administrator's Admin menu offers every option
  Given a visitor signs in as an Administrator
  Then the header shows an Admin menu and no other header nav change
  When the visitor activates the Admin menu
  Then it opens with manage-reports, manage-questions, and manage-answer-translations options

@REQ-MOD-008
@ui
Scenario: A signed-in SafetyOfficer's Admin menu offers manage-reports only
  Given a visitor signs in as a SafetyOfficer
  When the visitor activates the Admin menu
  Then it opens with a manage-reports option
  And it offers no manage-questions or manage-answer-translations option

@REQ-MOD-009
@ui
Scenario: A signed-in User sees no Admin menu
  Given a visitor signs in as a User
  Then the header shows a logout action
  And the header shows no Admin menu

@REQ-MOD-010
@ui
Scenario: An open Admin menu keeps every option on a single line
  Given a visitor signs in from the member login page
  When the visitor activates the Admin menu
  Then every option is on one line and none is truncated

@REQ-MOD-011
@ui
Scenario Outline: Activating an Admin menu option navigates to its page
  Given a visitor signs in from the member login page
  When the visitor activates the Admin menu
  And the visitor activates the <option> option
  Then the browser navigates to the <destination> page

Examples:
  | option           | destination      |
  | Manage reports   | manage-reports   |
  | Manage questions | manage-questions |

@REQ-MOD-012
@ui
Scenario: The Admin menu is absent for a signed-out visitor
  Given a visitor loads the homepage
  Then the header shows no Admin menu

@REQ-MOD-013
Scenario: A token signed by an unknown key is rejected
  Given a bearer token signed with a key the API does not trust
  When it is presented to any authenticated endpoint
  Then the API refuses the request
  And it does not disclose why the token was refused

@REQ-MOD-014
Scenario: A token whose signature has been altered is rejected
  Given a validly issued bearer token whose signature segment has been changed
  When it is presented to any authenticated endpoint
  Then the API refuses the request

@REQ-MOD-015
Scenario: An expired token is rejected
  Given a bearer token whose expiry has passed
  When it is presented to any authenticated endpoint
  Then the API refuses the request

@REQ-MOD-016
Scenario: A token for the wrong audience is rejected
  Given a bearer token issued for a different audience
  When it is presented to any authenticated endpoint
  Then the API refuses the request

@REQ-MOD-017
Scenario: A token with no recognized role claim authenticates as User
  Given a validly signed bearer token carrying no recognized role claim
  When it is presented to the API
  Then the request is authenticated
  And the identity has the User role and no administrative capability

@REQ-MOD-018
Scenario: The API never reads a name, an email, or any other claim
  Given a validly signed bearer token carrying a name, an email, and a picture claim
  When the API establishes the caller's identity
  Then it reads only the subject and the role claim
  And no other claim reaches domain code, a log, or the database

@REQ-MOD-019
Scenario: The development token endpoint does not exist outside development
  Given the API is not running in development
  When the development token endpoint is called
  Then the route does not exist

@REQ-MOD-020
Scenario Outline: A development login verified against the members site resolves role from the email lists
  Given the development token endpoint is available
  And "<email>" is <listed>
  When that email logs in with credentials the members site accepts
  Then the API returns a signed development token with the <role> role

Examples:
  | email                       | listed                                 | role          |
  | admin@example.test          | on the development administrator list  | Administrator |
  | officer@example.test        | on the development safety-officer list | SafetyOfficer |
  | nobody-special@example.test | on neither development list            | User          |

@REQ-MOD-021
Scenario: Bad members-site credentials show the same generic failure as bad fixed-account credentials
  Given the development token endpoint is available
  When a login is attempted with credentials the members site does not accept
  Then the API returns one generic invalid-credentials failure
  And nothing distinguishes it from an unknown fixed development account

@REQ-MOD-022
Scenario: A members-site outage during a development login is reported distinctly from bad credentials
  Given the development token endpoint is available
  When the members site cannot be reached during a login attempt
  Then the API reports the members site as unavailable
  And it does not report invalid credentials

@REQ-MOD-023
Scenario: An unauthenticated request to an admin endpoint is refused before the handler
  Given a request carries no bearer token
  When it reaches an admin endpoint
  Then the API refuses it before the handler runs

@REQ-MOD-024
Scenario: Every operation is authorized by the API, not just the UI
  Given an authenticated member without the required role calls an admin operation
  When the API processes the request
  Then the API rejects the operation regardless of what the UI would have shown

@REQ-MOD-025
@ignore
Scenario: User capabilities
  Given a member has the User role
  Then the member can submit an occurrence report
  And the member has no review, authoring, or publication capability

@REQ-MOD-026
@ignore
Scenario: SafetyOfficer capabilities
  Given a member has the SafetyOfficer role
  Then the member can view the review queue and private report material
  And view safe image/video derivatives and download validated unredacted documents
  And edit the bilingual summary pair
  And approve, reject, publish, and soft-delete reports

@REQ-MOD-027
@ignore
Scenario: Administrator capabilities include everything SafetyOfficer has
  Given a member has the Administrator role
  Then the member has every SafetyOfficer capability
  And can additionally create question revisions and curate reporter-added choices

@REQ-MOD-028
Scenario Outline: Only an Administrator may author a question revision
  Given a member has the <role> role
  When that member attempts to create a question revision
  Then the API <outcome> the attempt

Examples:
  | role          | outcome |
  | User          | rejects |
  | SafetyOfficer | rejects |
  | Administrator | accepts |

@REQ-MOD-029
@ignore
Scenario: Sensitive admin actions are audited without report content
  Given a sensitive read or material mutation occurs in the admin application
  When the action completes
  Then an audit entry records the acting token subject, action, target, and time
  And the subject is stored as an opaque string that joins to no user record
  And it never records report content

@REQ-MOD-030
Scenario: The admin report list shows every live report with its state
  Given reports exist in every workflow state, one without publication consent, and one soft-deleted
  When a reviewer lists reports
  Then every live report appears, newest first, with its workflow status and whether publication consent was refused
  And the soft-deleted report does not appear
  And no answer text or summary text appears in the list

@REQ-MOD-049
Scenario: The Needs action filter shows pending review, failed, and stuck reports
  Given reports exist in every workflow state
  And one report has waited in Submitted and one in Summarizing for more than 24 hours
  And one report has waited in Summarizing for less than 24 hours
  When a reviewer lists reports needing action
  Then the list holds the pending-review, summary-failed, and two stuck reports
  And each stuck report is marked stuck
  And the report summarizing for less than 24 hours is not listed

@REQ-MOD-050
Scenario Outline: A status filter narrows the admin report list
  Given reports exist in every workflow state, one without publication consent, and one soft-deleted
  When a reviewer lists reports with the <filter> filter
  Then the list holds only <reports>

Examples:
  | filter         | reports                                     |
  | published      | published reports                           |
  | private        | live reports whose reporter refused consent |
  | rejected       | rejected reports                            |
  | summary-failed | reports whose summarization failed          |

@REQ-MOD-031
Scenario: A report detail view exposes only what the reviewer needs
  Given a reviewer opens a report's detail view
  When the detail query runs
  Then it supplies the reporter language, exact bilingual question labels and answers with privacy indicated, processing state, both summary texts with their shared provenance/approval, and each attachment's kind and whether it can be opened
  And it supplies no storage key and no link; an attachment is opened only through its own audited view or download request

@REQ-MOD-051
Scenario: Opening a report's detail view is audited
  Given a reviewer opens a report's detail view
  When the detail query runs
  Then an audit entry records the reviewer's token subject, ViewedRawReport, the report, and the time
  And the audit entry records no report content

@REQ-MOD-032
Scenario: Editing a summary clears approval and unpublishes
  Given a reviewer edits either summary language
  When the edit is saved
  Then the pair's approval is cleared
  And a previously published report is unpublished

@REQ-MOD-033
Scenario: Approval applies once to the current bilingual pair
  Given a reviewer approves the current English/French summary pair
  When the approval is recorded
  Then it applies to that pair as a whole, not to one language
  And the approving token subject is recorded as an opaque string

@REQ-MOD-034
Scenario: Rejection blocks publication but keeps the report for learning
  Given a reviewer rejects a report
  When the rejection is recorded
  Then the report can never satisfy the publication invariant
  And the report remains available for internal learning

@REQ-MOD-035
Scenario: Publication requires every guard to pass, with no bypass
  Given a report is non-deleted, has explicit positive consent, has two nonblank summary texts, and a reviewer approves the pair
  When the approval is recorded
  Then the report is published in the same action
  And no Administrator, migration, background worker, or direct API caller can bypass any of these guards

@REQ-MOD-036
@ignore
Scenario: The public DTO exposes only the approved summary and its metadata
  Given a report is published
  When the public API returns it
  Then the response contains only the opaque report ID, ai_summary_en, ai_summary_fr, and the publication timestamp
  And it never contains question keys, labels, answers, consent value, report language, private flags, raw reports, attachment metadata or URLs, member or reviewer identities, model provenance, or audit records

@REQ-MOD-037
@ignore
Scenario: The public feed lists only publishable reports
  Given some reports are publishable and others are not
  When the public feed is queried
  Then the response is a deterministic paginated list containing only publishable reports
  And no non-publishable report ever appears

@REQ-MOD-038
@ignore
Scenario: An unknown or non-public report id returns 404
  Given a report id is unknown, deleted, unapproved, rejected, or not consented
  When the public API is asked for that report
  Then the API returns 404
  And non-public ids are indistinguishable from unknown ids

@REQ-MOD-039
@ignore
Scenario: There is no publication channel besides the HPAC public feed
  Given a report becomes publishable
  When it is published
  Then it appears only on the HPAC public feed and report-detail page
  And no email, messaging, social, webhook, or third-party channel publishes it

@REQ-MOD-040
@ignore
Scenario: Soft-deleting a report stops it everywhere immediately
  Given a report exists in any state
  When a safety officer soft-deletes it
  Then it is immediately removed from the public feed and normal review queries
  And ordinary Worker processing for it stops

@REQ-MOD-041
@ignore
Scenario: Revoking a member's access is the identity provider's decision
  Given a member's access is revoked at the identity provider
  When their current token expires or stops being issued
  Then they can no longer authenticate
  And this system holds no record of them to revoke
  And historic audit rows keep the opaque subject they were written with

@REQ-MOD-042
@ui
Scenario: A signed-out visitor who navigates to an admin route is sent to sign in
  Given a visitor is signed out
  When the visitor navigates directly to an admin route
  Then the browser is redirected to the member-login page
  And no admin page content is shown first

@REQ-MOD-043
@ui
Scenario Outline: A signed-in member without the required role sees a real 403, not a 404 or the page content
  Given a visitor signs in as a <role>
  When the visitor navigates directly to <route>, which their role cannot use
  Then the page shows a forbidden (403) view in place of the route's content
  And it is not the not-found page
  And no request for that route's data is made

Examples:
  | role          | route                      |
  | User          | /admin/reports             |
  | User          | /admin/questions           |
  | SafetyOfficer | /admin/questions           |
  | SafetyOfficer | /admin/answer-translations |

@REQ-MOD-044
Scenario: A successful sign-in writes an audit row
  Given a member signs in with valid credentials
  When the sign-in succeeds
  Then an audit entry records the token subject, a sign-in-succeeded action, and the time
  And it never records the credentials

@REQ-MOD-045
Scenario: A failed sign-in attempt writes an audit row
  Given a sign-in attempt uses credentials that are not valid
  When the attempt is rejected
  Then an audit entry records a sign-in-failed action and the time
  And it never records the attempted credentials
  And the actor is recorded as the attempted identity rather than left blank

@REQ-MOD-046
Scenario: A reviewer's attachment view writes its own audit row, distinct from a raw-report view
  Given a reviewer opens a private attachment
  When the view completes
  Then an audit entry records an attachment-viewed action naming that attachment as the target
  And it is distinguishable from a raw-report-viewed audit entry for the same report

@REQ-MOD-047
@ignore
Scenario: A failed audit write blocks the action it would have recorded
  Given an administrator or reviewer performs an action that must be audited
  When the audit row fails to write
  Then the action itself does not commit
  And the caller sees the action as failed, not succeeded

@REQ-MOD-048
@ignore
Scenario: Sign-out is not an audited event
  Given a signed-in member activates the logout action
  When the client discards its token
  Then no request reaches the API for that logout
  And no audit entry is written for it

@REQ-MOD-052
@ui
Scenario: The Manage reports page lists reports with a status badge and a Private badge
  Given a safety officer is signed in and reports exist in several states
  When the safety officer opens Manage reports
  Then each report shows its submission time and a badge for its workflow status
  And a report whose reporter refused consent also shows a "Private (no consent)" badge
  And a stuck report shows a "Stuck" badge

@REQ-MOD-053
@ui
Scenario: Choosing a filter on Manage reports narrows the list
  Given a safety officer is signed in and reports exist in several states
  When the safety officer opens Manage reports
  And the safety officer chooses the "Published" filter
  Then only published reports are listed
  And the chosen filter stays in the address bar

@REQ-MOD-054
@ui
Scenario: Opening a report shows its answers with private answers marked, and its summary pair
  Given a safety officer is signed in and reports exist in several states
  When the safety officer opens Manage reports
  And the safety officer opens a pending-review report
  Then its answers are shown under their questions, with each private answer marked private
  And both the English and French summary texts are shown with the model and prompt version

@REQ-MOD-055
Scenario Outline: Approving the pair publishes it only when the reporter consented
  Given a pending-review report whose reporter answered <consent> to publication
  When a reviewer approves the pair
  Then the report becomes <status>
  And the approval and any publication are recorded in one audited action

Examples:
  | consent | status    |
  | yes     | Published |
  | no      | Approved  |

@REQ-MOD-056
Scenario: A rejected report can be reopened for review
  Given a reviewer rejected a report
  When a reviewer reopens it
  Then the report returns to Pending review
  And the reopening is audited

@REQ-MOD-057
Scenario: Unpublishing takes a report off the public feed and back to review
  Given a report is published
  When a reviewer unpublishes it
  Then it is no longer publishable
  And the pair's approval is cleared and the report returns to Pending review
  And the unpublishing is audited

@REQ-MOD-058
Scenario: A rejection may carry a note that only reviewers see
  Given a reviewer rejects a report with a note
  When the rejection is recorded
  Then the detail view shows the note to reviewers
  And the note never reaches the public API, the audit log, or the application logs
  And rejecting without a note also succeeds

@REQ-MOD-059
Scenario: A reviewer writes the pair by hand when summarization failed
  Given a report is SummaryFailed
  When a reviewer saves an English and a French summary text
  Then the report has one summary pair with "manual" as its model and prompt version
  And the report returns to Pending review
  And writing the pair is audited

@REQ-MOD-060
Scenario: A review action based on a stale view is refused
  Given two reviewers opened the same report
  And the first reviewer has saved a change to it
  When the second reviewer sends a change based on the view they loaded
  Then the API answers 409 with a problem that asks them to reload
  And nothing the second reviewer sent is saved

@REQ-MOD-061
Scenario Outline: Every review action writes one content-free audit entry in its own transaction
  Given a report on which a reviewer can <action>
  When the reviewer does so
  Then one audit entry records the reviewer's token subject, <audit action>, the report, and the time
  And the entry records no summary text, answer, or rejection note

Examples:
  | action                | audit action      |
  | edit the summary pair | EditedSummary     |
  | approve the pair      | ApprovedReport    |
  | reject the report     | RejectedReport    |
  | reopen the report     | ReopenedReport    |
  | unpublish the report  | UnpublishedReport |
  | write a manual pair   | EditedSummary     |

@REQ-MOD-062
@ui
Scenario Outline: The report view offers only the actions its state allows
  Given a safety officer is signed in and a <status> report exists
  When the safety officer opens that report
  Then the offered actions are <actions>

Examples:
  | status         | actions                               |
  | pending-review | Edit summary, Approve, Reject, Delete |
  | published      | Edit summary, Unpublish, Delete       |
  | rejected       | Reopen, Delete                        |
  | summary-failed | Write summary, Delete                 |

@REQ-MOD-063
@ui
Scenario: Editing the summary pair saves both texts and clears approval
  Given a safety officer is signed in and a published report exists
  When the safety officer opens that report
  And the safety officer edits the English summary and saves
  Then the report shows the "Pending review" badge
  And the saved English text is shown

@REQ-MOD-064
@ui
Scenario: Approving a consented report publishes it
  Given a safety officer is signed in and a pending-review report exists
  When the safety officer opens that report
  And the safety officer approves it
  Then the report shows the "Published" badge

@REQ-MOD-065
@ui
Scenario: Rejecting with a note shows the note on the report
  Given a safety officer is signed in and a pending-review report exists
  When the safety officer opens that report
  And the safety officer rejects it with the note "Duplicate of an earlier report"
  Then the report shows the "Rejected" badge
  And the note "Duplicate of an earlier report" is shown

@REQ-MOD-066
@ui
Scenario: A stale action tells the reviewer to reload
  Given a safety officer is signed in and a pending-review report exists
  And another reviewer has changed that report since it was opened
  When the safety officer opens that report
  And the safety officer approves it
  Then a message says the report changed and offers to reload it

@REQ-MOD-067
@ui
Scenario: Deleting a report asks for confirmation first
  Given a safety officer is signed in and a pending-review report exists
  When the safety officer opens that report
  And the safety officer chooses Delete
  Then a confirmation asks whether to delete the report
  When the safety officer confirms
  Then the browser returns to Manage reports

@REQ-MOD-068
@ui
Scenario: Opening an attachment requests its own audited link
  Given a safety officer is signed in and a pending-review report exists
  When the safety officer opens that report
  And the safety officer opens its document attachment
  Then the browser requests that attachment's download link
