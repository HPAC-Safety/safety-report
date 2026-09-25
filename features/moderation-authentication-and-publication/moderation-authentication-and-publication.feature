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

@REQ-MOD-087
@ui
Scenario: An Administrator's Admin menu shows how much work is waiting
  Given the API counts 3 reports needing action and 2 answers awaiting translation
  And a visitor signs in as an Administrator
  Then the Admin menu shows a count of 5
  When the visitor activates the Admin menu
  Then the manage-reports option shows a count of 3
  And the manage-answer-translations option shows a count of 2
  And the manage-questions option shows no count

@REQ-MOD-088
@ui
Scenario: A SafetyOfficer's Admin menu counts only the reports needing action
  Given the API counts 4 reports needing action and no answers awaiting translation
  And a visitor signs in as a SafetyOfficer
  Then the Admin menu shows a count of 4
  When the visitor activates the Admin menu
  Then the manage-reports option shows a count of 4

@REQ-MOD-089
@ui
Scenario: With nothing waiting, the Admin menu shows no count
  Given the API counts 0 reports needing action and 0 answers awaiting translation
  And a visitor signs in as an Administrator
  Then the Admin menu shows no count
  When the visitor activates the Admin menu
  Then no option shows a count

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
  And publish, unpublish, and soft-delete reports

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
Scenario: The Needs action filter shows pending, failed, and stuck reports
  Given reports exist in every workflow state
  And one report has waited in Submitted and one in Summarizing for more than 24 hours
  And one report has waited in Summarizing for less than 24 hours
  When a reviewer lists reports needing action
  Then the list holds the pending, summary-failed, and two stuck reports
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
  | unpublished    | unpublished reports                         |
  | summary-failed | reports whose summarization failed          |

@REQ-MOD-084
Scenario: A reviewer reads how many reports need action
  Given reports exist in every workflow state
  And one report has waited in Submitted and one in Summarizing for more than 24 hours
  When a SafetyOfficer reads the pending counts
  Then the reports count equals the number of reports the Needs action filter lists
  And the counts carry no answers-awaiting-translation count

@REQ-MOD-085
Scenario: Only an Administrator's pending counts include answers awaiting translation
  Given an answer is awaiting machine translation
  When an Administrator reads the pending counts
  Then the translation count equals the number of answers in the translation queue

@REQ-MOD-086
Scenario: A User cannot read the pending counts
  When a User reads the pending counts
  Then the API refuses the pending counts with 403

@REQ-MOD-090
Scenario: A report without publication consent never needs action
  Given a report whose reporter did not consent to publication is Unpublished
  When a SafetyOfficer lists reports needing action and reads the pending counts
  Then that report is not listed
  And the reports count does not include it

@REQ-MOD-031
Scenario: A report detail view exposes only what the reviewer needs
  Given a reviewer opens a report's detail view
  When the detail query runs
  Then it supplies the reporter language, exact bilingual question labels and each question's type, answers with privacy indicated, processing state, both summary texts with their shared provenance/approval, and each attachment's kind and whether it can be opened
  And it supplies no storage key and no link; an attachment is opened only through its own audited view or download request

@REQ-MOD-051
Scenario: Opening a report's detail view is audited
  Given a reviewer opens a report's detail view
  When the detail query runs
  Then an audit entry records the reviewer's token subject, ViewedRawReport, the report, and the time
  And the audit entry records no report content

@REQ-MOD-032
Scenario: Editing a summary clears approval and returns the report to Pending
  Given a reviewer edits either summary language
  When the edit is saved
  Then the pair's approval is cleared
  And a previously published report returns to Pending and leaves the public feed

@REQ-MOD-033
Scenario: Publishing approves the current bilingual pair once
  Given a reviewer publishes the current English/French summary pair
  When the publication is recorded
  Then it applies to that pair as a whole, not to one language
  And the approving token subject is recorded as an opaque string

@REQ-MOD-035
Scenario: Publication requires every guard to pass, with no bypass
  Given a report is non-deleted, has explicit positive consent, has two nonblank summary texts, and a reviewer publishes it
  When the publication is recorded
  Then the pair is approved and the report is Published in the same action
  And no Administrator, migration, background worker, or direct API caller can bypass any of these guards

@REQ-MOD-036
Scenario: The public DTO exposes only the approved summary and its metadata
  Given a report is published
  When the public API returns it
  Then the response contains only the opaque report ID, ai_summary_en, ai_summary_fr, the publication timestamp, the number of visible comments, and each public file's opaque id, kind, and — for a document only — coarse format
  And it never contains question keys, labels, answers, consent values, report language, private flags, raw reports, attachment names, sizes, content types, keys, or URLs, member or reviewer identities, model provenance, or audit records

@REQ-MOD-037
Scenario: The public feed lists only publishable reports
  Given some reports are publishable and others are not
  When the public feed is queried
  Then the response is a deterministic paginated list containing only publishable reports
  And no non-publishable report ever appears
  And the list is newest published first, a tie broken by report ID, and each page names the cursor that continues it

@REQ-MOD-038
Scenario: An unknown or non-public report id returns 404
  Given a report id is unknown, deleted, pending, unpublished, or not consented
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
  And no request for that route's data is made, the Admin menu's pending counts aside

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
@ui
Scenario: Signing out sends nothing to the API
  Given a signed-in member activates the logout action
  When the client discards its token
  Then no request reaches the API for that logout

@REQ-MOD-091
Scenario: Sign-out is not an audited event
  Given the API's mapped routes
  Then none of them signs a member out
  And no audit action records a sign-out

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
  And the safety officer opens a pending report
  Then its answers are shown under their questions, with each private answer marked private
  And both the English and French summary texts are shown with the model and prompt version

@REQ-MOD-075
@ui
Scenario Outline: A date, time, or yes/no answer reads in the reviewer's language, not in its stored form
  Given a safety officer is signed in and a report with a <type> answer stored as "<stored>" exists
  And the interface language is <language>
  When the safety officer opens that report
  Then the answer reads "<shown>"
  And no translation is shown beside it

Examples:
  | type   | stored     | language | shown              |
  | date   | 2026-09-13 | English  | September 13, 2026 |
  | date   | 2026-09-13 | French   | 13 septembre 2026  |
  | time   | 14:30      | English  | 2:30 p.m.          |
  | time   | 14:30      | French   | 14 h 30            |
  | yes/no | yes        | English  | Yes                |
  | yes/no | no         | French   | Non                |
  | yes/no | oui        | English  | Yes                |
  | yes/no | non        | French   | Non                |

@REQ-MOD-076
@ui
Scenario: A stored date that is not a real date is shown as stored
  Given a safety officer is signed in and a report with a date answer stored as "2026-13-45" exists
  And the interface language is English
  When the safety officer opens that report
  Then the answer reads "2026-13-45"

@REQ-MOD-055
Scenario Outline: Publishing a consented report's pair makes it public
  Given a <from> report whose reporter consented to publication
  When a reviewer publishes the pair
  Then the report becomes Published
  And the approval and the publication are recorded in one audited action

Examples:
  | from        |
  | Pending     |
  | Unpublished |

@REQ-MOD-057
Scenario Outline: Unpublishing takes a report off the public feed and keeps it for learning
  Given a <from> report whose reporter consented to publication
  When a reviewer unpublishes it
  Then it is no longer publishable
  And the pair's approval is cleared and the report is Unpublished
  And the report remains available for internal learning
  And the unpublishing is audited

Examples:
  | from      |
  | Pending   |
  | Published |

@REQ-MOD-058
Scenario: Unpublishing may carry a note that only reviewers see
  Given a reviewer unpublishes a report with a note
  When the unpublishing is recorded
  Then the detail view shows the note to reviewers
  And the note never reaches the public API, the audit log, or the application logs
  And unpublishing without a note also succeeds

@REQ-MOD-059
Scenario: A reviewer writes the pair by hand when summarization failed
  Given a report is SummaryFailed
  When a reviewer saves an English and a French summary text
  Then the report has one summary pair with "manual" as its model and prompt version
  And the report goes to Pending
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
  And the entry records no summary text, answer, or unpublishing note

Examples:
  | action                | audit action      |
  | edit the summary pair | EditedSummary     |
  | publish the pair      | PublishedReport   |
  | unpublish the report  | UnpublishedReport |
  | write a manual pair   | EditedSummary     |

@REQ-MOD-062
@ui
Scenario Outline: The report view offers only the actions its state allows
  Given a safety officer is signed in and a <status> report exists
  When the safety officer opens that report
  Then the offered actions are <actions>

Examples:
  | status              | actions                                  |
  | pending             | Edit summary, Publish, Unpublish, Delete |
  | published           | Edit summary, Unpublish, Delete          |
  | unpublished         | Edit summary, Publish, Delete            |
  | summary-failed      | Write summary, Delete                    |
  | private-unpublished | Delete                                   |

@REQ-MOD-063
@ui
Scenario: Editing the summary pair saves both texts and clears approval
  Given a safety officer is signed in and a published report exists
  When the safety officer opens that report
  And the safety officer edits the English summary and saves
  Then the report shows the "Pending" badge
  And the saved English text is shown

@REQ-MOD-064
@ui
Scenario: Publishing a consented report shows it Published
  Given a safety officer is signed in and a pending report exists
  When the safety officer opens that report
  And the safety officer publishes it
  Then the report shows the "Published" badge

@REQ-MOD-065
@ui
Scenario: Unpublishing with a note shows the note on the report
  Given a safety officer is signed in and a pending report exists
  When the safety officer opens that report
  And the safety officer unpublishes it with the note "Duplicate of an earlier report"
  Then the report shows the "Unpublished" badge
  And the note "Duplicate of an earlier report" is shown

@REQ-MOD-066
@ui
Scenario: A stale action tells the reviewer to reload
  Given a safety officer is signed in and a pending report exists
  And another reviewer has changed that report since it was opened
  When the safety officer opens that report
  And the safety officer publishes it
  Then a message says the report changed and offers to reload it

@REQ-MOD-067
@ui
Scenario: Deleting a report asks for confirmation first
  Given a safety officer is signed in and a pending report exists
  When the safety officer opens that report
  And the safety officer chooses Delete
  Then a confirmation asks whether to delete the report
  When the safety officer confirms
  Then the browser returns to Manage reports

@REQ-MOD-068
@ui
Scenario: Opening an attachment requests its own audited link
  Given a safety officer is signed in and a pending report exists
  When the safety officer opens that report
  And the safety officer opens its document attachment
  Then the browser requests that attachment's download link

@REQ-MOD-069
Scenario Outline: Only a reviewer may request a machine translation
  Given a member signed in as <role>
  When that member requests a translation
  Then the API answers <outcome>

Examples:
  | role          | outcome       |
  | User          | forbidden     |
  | SafetyOfficer | a translation |
  | Administrator | a translation |

@REQ-MOD-070
Scenario Outline: Each summary language records how it was produced
  Given <situation>
  When the pair is saved
  Then the English text is recorded as <english> and the French text as <french>

Examples:
  | situation                                                                     | english   | french    |
  | the Worker produced the pair                                                  | generated | generated |
  | a reviewer edited only the English text of a generated pair                   | human     | generated |
  | a reviewer edited the English text and accepted its French translation        | human     | machine   |
  | a reviewer wrote both texts by hand after summarization failed                | human     | human     |
  | a reviewer wrote the French text by hand and accepted its English translation | machine   | human     |

@REQ-MOD-071
@ui
Scenario Outline: The editor offers a translate button for each language the reviewer changed
  Given a safety officer is signed in and a pending report exists
  When the safety officer opens that report
  And the safety officer opens the summary editor
  And the safety officer changes <changed>
  Then the translate buttons offered are <buttons>

Examples:
  | changed                     | buttons                                   |
  | nothing                     | none                                      |
  | the English text            | Translate to French                       |
  | the French text             | Translate to English                      |
  | the English and French text | Translate to French, Translate to English |

@REQ-MOD-072
@ui
Scenario: Translating asks before overwriting and shows what would change
  Given a safety officer is signed in and a pending report exists
  When the safety officer opens that report
  And the safety officer opens the summary editor
  And the safety officer changes the English text
  And the safety officer chooses Translate to French
  Then a confirmation shows the current French text and the proposed translation with their differences marked
  When the safety officer keeps the current text
  Then the French text is unchanged
  When the safety officer chooses Translate to French and accepts the translation
  Then the French text is the proposed translation
  And the Translate to English button is not offered for it

@REQ-MOD-073
@ui
Scenario: Writing a pair by hand offers the translate buttons too
  Given a safety officer is signed in and a summary-failed report exists
  When the safety officer opens that report
  And the safety officer chooses Write summary
  And the safety officer types the English text
  Then the translate buttons offered are Translate to French

@REQ-MOD-077
Scenario: The report detail view gives a second language only for an answer that has one
  Given a submitted report answered a first name, an email, a date, a picker, and a narrative marked for translation
  And the Worker has translated the narrative
  When a reviewer opens the report's detail view
  Then the picker and the narrative each carry their second language
  And the first name, the email, and the date carry none, even if one was stored before this rule

@REQ-MOD-078
@ui
Scenario: Opening a report shows a translation only under answers that have one
  Given a safety officer is signed in and reports exist in several states
  When the safety officer opens Manage reports
  And the safety officer opens a pending report
  Then a translated narrative answer shows its translation beneath it
  And a name or email answer shows no translation line

@REQ-MOD-074
@ui
Scenario: The report view shows how each summary language was produced
  Given a safety officer is signed in and a report whose French text was machine-translated exists
  When the safety officer opens that report
  Then the English text is labelled as edited by a reviewer
  And the French text is labelled as machine-translated

@REQ-MOD-079
@ui
Scenario: Each report in the public feed opens at its own address
  Given the public feed has published reports
  When a visitor opens View safety reports and selects one
  Then the address bar shows /reports/ followed by that report's ID
  And the page shows that report's full summary in the visitor's language

@REQ-MOD-080
@ui
Scenario: A report's address opens it directly and survives a reload
  Given a visitor has the address of a published report
  When the visitor opens that address directly
  Then the page shows that report's full summary
  When the page reloads
  Then the page still shows that report's full summary

@REQ-MOD-081
@ui
Scenario: An address for a report that is not public shows not found
  Given a report ID the public API answers with 404
  When a visitor opens /reports/ followed by that ID
  Then the page says the report was not found
  And it says nothing about whether such a report exists

@REQ-MOD-082
@ui
Scenario: The public feed pages forward and the address keeps the page
  Given the public feed has more published reports than fit on one page
  When a visitor moves to the next page
  Then the address bar carries that page's cursor
  And going back returns the visitor to the first page

@REQ-MOD-083
@ui
Scenario: A reviewer can open a published report's public page
  Given a safety officer is signed in and a published report exists
  When the safety officer opens that report
  Then the report view links to the report's public address
  And a report that is not published shows no such link
