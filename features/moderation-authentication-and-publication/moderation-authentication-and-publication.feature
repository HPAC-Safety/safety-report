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
  Then it opens with manage-reports, review-type-ahead-values, and manage-questions options

@REQ-MOD-092
@ui
Scenario: A signed-in SafetyOfficer's Admin menu offers reports and type-ahead review
  Given a visitor signs in as a SafetyOfficer
  When the visitor activates the Admin menu
  Then it opens with manage-reports and review-type-ahead-values options
  And it offers no manage-questions option

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
  Given the API counts 3 reports needing action
  And a visitor signs in as an Administrator
  Then the Admin menu shows a count of 3
  When the visitor activates the Admin menu
  Then the manage-reports option shows a count of 3
  And the manage-questions option shows no count

@REQ-MOD-093
@ui
Scenario: A SafetyOfficer's Admin menu counts reports and type-ahead values waiting
  Given the API counts 4 reports needing action and 3 type-ahead values awaiting review
  And a visitor signs in as a SafetyOfficer
  Then the Admin menu shows a count of 7
  When the visitor activates the Admin menu
  Then the manage-reports option shows a count of 4
  And the review-type-ahead-values option shows a count of 3

@REQ-MOD-089
@ui
Scenario: With nothing waiting, the Admin menu shows no count
  Given the API counts 0 reports needing action
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

@REQ-MOD-156
Scenario: An environment with no identity provider configured still starts and serves its public endpoints, and refuses every bearer token
  Given the API is not running in development and no identity provider is configured
  When the health endpoint is requested
  Then the API answers 200
  When a request carrying a bearer token reaches an authorization-protected endpoint
  Then the API refuses it before the handler runs

@REQ-MOD-157
Scenario: With the temporary interim issuer enabled, a member signs in with their members-site credentials, and the fixed development accounts do not exist
  Given the API is not running in development and the temporary interim issuer is enabled
  When a member signs in with credentials the members site accepts
  Then the API issues a token the API itself accepts
  And an allowlisted administrator account's token carries the Administrator role
  When a sign-in is attempted with the fixed development administrator account
  Then the API refuses it

@REQ-MOD-158
Scenario: With the temporary interim issuer disabled, none of its endpoints exist
  Given the API is not running in development and the temporary interim issuer is disabled
  When the interim issuer's discovery document is requested
  Then the API answers 404
  When the interim issuer's JWKS is requested
  Then the API answers 404
  When a token is requested from the token endpoint
  Then the API answers 404

@REQ-MOD-159
Scenario: The temporary interim issuer's JWKS publishes only a public key
  Given the API is not running in development and the temporary interim issuer is enabled
  When the interim issuer's JWKS is requested
  Then the response carries only a public key, never a private key field

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
  And can additionally create question revisions and author every question's choices

@REQ-MOD-094
Scenario Outline: A Safety Officer or an Administrator reviews type-ahead values
  Given a member has the <role> role
  When that member approves, corrects, merges, relinks, or removes a reporter-added type-ahead value
  Then the API <outcome> the attempt

Examples:
  | role          | outcome  |
  | User          | forbids  |
  | SafetyOfficer | allows   |
  | Administrator | allows   |

@REQ-MOD-097
@ui
Scenario: A Safety Officer approves, corrects, and removes type-ahead values on the review page
  Given a signed-in Safety Officer and three type-ahead values flagged for review
  When they open the review-type-ahead-values page
  Then each value is listed under its question's heading, with the language it was typed in and how many answers name it
  When they approve "Mount 7", correct "coopers" to "Cooper's", and remove "Test site"
  Then the API is asked to approve, correct, and remove exactly those values
  And the page lists no value left to review

@REQ-MOD-095
@ui
Scenario: A Safety Officer reviews flagged type-ahead values on one page
  Given a signed-in Safety Officer and two type-ahead questions with values flagged for review
  When they open the review-type-ahead-values page
  Then every flagged value is listed under its question's heading, with its language and how many answers name it
  When they merge "Coopers" into "Cooper's"
  Then "Coopers" leaves the list
  And "Cooper's" is no longer flagged

@REQ-MOD-160
@ui
Scenario: The review queue groups flagged values under their question, questions ordered alphabetically
  Given a signed-in Safety Officer and flagged values under two type-ahead questions, returned by the API with the later question first
  When they open the review-type-ahead-values page
  Then the question headings read, top to bottom, "Where did this happen?" then "Where did you launch?"

@REQ-MOD-161
@ui
Scenario Outline: Values within a question's group are sorted alphabetically in the viewer's language, ignoring case and accents
  Given a Safety Officer who reads <language> and one type-ahead question whose flagged values are "<second>", "<first>", and "<third>", in that order
  When they open the review-type-ahead-values page
  Then the values under that question's heading read, top to bottom, "<first>", "<second>", and "<third>"

Examples:
  | language | first  | second   | third |
  | English  | cooper | Cooper's | zulu  |
  | French   | Étang  | Etna     | zone  |

@REQ-MOD-162
@ui
Scenario Outline: Approving, correcting, removing, merging, and relinking a value keeps the reviewer's scroll position, with no loading state
  Given a signed-in Safety Officer and twenty flagged values under one type-ahead question
  When they open the review-type-ahead-values page
  And they scroll to "Site 15"
  And they <action> "Site 15"
  Then the page never shows the loading text
  And the scroll position is unchanged

Examples:
  | action  |
  | approve |
  | remove  |
  | correct |
  | merge   |
  | relink  |

@REQ-MOD-163
@ui
Scenario: A merged value leaves the queue in place, and a merge target still awaiting review shows its updated answer count
  Given a signed-in Safety Officer and two flagged values of the same question, one also awaiting review in its own right
  When they open the review-type-ahead-values page
  And they merge "Coopers" into "Cooper's"
  Then "Coopers" leaves the list, and the rows around it stay where they are
  And "Cooper's" is still listed, showing 5 answers naming it

# Re-translating a type-ahead value's wording while correcting it (ADR-0141,
# ADR-0144): the correction view reuses the question editor's choice
# affordance, scoped to the one value being corrected. No new API.

@REQ-MOD-166
@ui
Scenario: A value written in both languages offers Translate only once its wording differs from what correction opened with
  Given a signed-in Safety Officer and three type-ahead values flagged for review
  When they open the review-type-ahead-values page
  And they begin correcting "coopers"
  And they write its French wording as "Coopers (fr)"
  Then that value's Translate action is unavailable
  When they edit its English wording to "Cooper's"
  Then that value's Translate action becomes available

@REQ-MOD-167
@ui
Scenario: A value's Translate is unavailable after it translates, until its source is edited again
  Given a signed-in Safety Officer and three type-ahead values flagged for review
  When they open the review-type-ahead-values page
  And they begin correcting "coopers", edit its English wording to "Cooper's", and press Translate
  Then that value's Translate action is unavailable
  When they edit that value's English wording again
  Then that value's Translate action becomes available

@REQ-MOD-168
@ui
Scenario: Pressing Translate drafts the other language, still editable, and saves nothing by itself
  Given a signed-in Safety Officer and three type-ahead values flagged for review
  When they open the review-type-ahead-values page
  And they begin correcting "coopers", edit its English wording to "Cooper's", and press Translate
  Then that value's French field is filled with the translation and remains editable
  And nothing is saved until they press Save correction

@REQ-MOD-169
@ui
Scenario: The direction switch changes which language Translate reads from
  Given a signed-in Safety Officer and three type-ahead values flagged for review
  When they open the review-type-ahead-values page
  And they begin correcting "Test site"
  Then that value's direction switch translates English to French
  When they flip that value's direction switch to French to English
  And they write its French wording as "Site d'essai" and press Translate
  Then that value's English field is filled with the translation

@REQ-MOD-170
@ui
Scenario: Translate is unavailable when the server has no translation provider
  Given a signed-in Safety Officer and three type-ahead values flagged for review, on a server with no translation provider
  When they open the review-type-ahead-values page
  And they begin correcting "coopers"
  Then that value's Translate action is unavailable and says why

@REQ-MOD-171
@ui
Scenario: A failed translation says so on the value's row and drafts nothing
  Given a signed-in Safety Officer and three type-ahead values flagged for review, on a server whose translation fails
  When they open the review-type-ahead-values page
  And they begin correcting "coopers", edit its English wording to "Cooper's", and press Translate
  Then that value's row says the translation failed
  And that value's French field still reads ""
  And that value's Translate action becomes available

@REQ-MOD-172
@ui
Scenario: A translation overtaken by a direction flip is dropped, and Translate stops showing as working
  Given a signed-in Safety Officer and three type-ahead values flagged for review, on a server whose translation answers only when released
  When they open the review-type-ahead-values page
  And they begin correcting "coopers", edit its English wording to "Cooper's", and press Translate
  And they flip that value's direction switch while the translation is still out
  And the translation then answers
  Then its answer is dropped and Translate is no longer shown as working

@REQ-MOD-096
Scenario Outline: A report's consent reaches the admin view as true, false, or null
  Given a report whose publication consent is <publication> and whose media consent is <media>
  When a safety officer reads the report list and the report's detail
  Then the list row and the detail give consent as <consent json>
  And the detail gives media consent as <media json>

Examples:
  | publication | media     | consent json | media json |
  | given       | given     | true         | true       |
  | given       | refused   | true         | false      |
  | given       | not asked | true         | null       |
  | refused     | not asked | false        | null       |

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
  And no answer text or summary text appears in the list, except the reporter's and pilot's names

@REQ-MOD-124
Scenario Outline: The admin report list shows the reporter's and pilot's names by stable role, blank when unanswered
  Given a report whose reporter first name is "<reporter first>", reporter last name is "<reporter last>", pilot first name is "<pilot first>", and pilot last name is "<pilot last>"
  When a reviewer lists reports
  Then the row's reporter name reads "<reporter name>"
  And the row's pilot name reads "<pilot name>"

Examples:
  | reporter first | reporter last | pilot first | pilot last | reporter name | pilot name  |
  | Alex            | Rivera        | Sam         | Chen       | Alex Rivera   | Sam Chen    |
  | Alex            | Rivera        | Alex        | Rivera     | Alex Rivera   | Alex Rivera |
  | Alex            |               |             |            | Alex          |             |
  |                 |               | Sam         | Chen       |               | Sam Chen    |
  |                 |               |             |            |               |             |

@REQ-MOD-119
Scenario: A list row carries the version a review command sends back
  Given reports exist in every workflow state
  When a reviewer lists reports
  Then each row carries the same version the report's detail view gives
  And publishing an unpublished report with its row's version succeeds without opening the report
  And no ViewedRawReport entry is written for that report

@REQ-MOD-129
Scenario: The admin report list pages forward with a keyset cursor, restarting from the top for an unreadable one
  Given reports exist in every workflow state
  When a reviewer lists reports
  And a reviewer lists reports after a cursor naming a report no longer in the queue
  Then that list starts with the same report the first page did

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
  Then the response contains only the opaque report ID, ai_summary_en, ai_summary_fr, the publication timestamp, the number of visible comments, the viewer-scoped attachment count, each public file's opaque id, kind, and — for a document only — coarse format, and the staff attachment list, null for this anonymous viewer
  And it never contains question keys, labels, answers, consent values, report language, private flags, raw reports, attachment names, sizes, content types, keys, or URLs, member or reviewer identities, model provenance, or audit records

@REQ-MOD-150
Scenario: The feed's attachment count is the public count for a visitor and the full count for staff
  Given a published report has one public attachment and one attachment only staff may see
  When an anonymous visitor lists the feed
  Then the report's attachment count is 1
  When a signed-in safety officer lists the feed
  Then the report's attachment count is 2

@REQ-MOD-155
Scenario: An ordinary member's token widens nothing; only SafetyOfficer or Administrator does
  Given a published report has one public attachment and one attachment only staff may see
  When a signed-in member with the User role lists the feed
  Then the report's attachment count is 1
  When a signed-in Administrator lists the feed
  Then the report's attachment count is 2

@REQ-MOD-151
Scenario: The admin report list carries every non-deleted attachment's count
  Given a report has one hidden attachment and one still-processing attachment
  When a reviewer lists reports
  Then the row's attachment count is 2

@REQ-MOD-152
Scenario: A signed-in safety officer sees every attachment on the public report page, each marked public or not
  Given a published report has a public image and a hidden image
  When a signed-in safety officer asks the public API for that report
  Then the response carries a staff attachment for each file, with its state and public visibility
  And the hidden file's visibility reads "hidden"
  And the public file's visibility reads "public"

@REQ-MOD-164
@ui
Scenario Outline: A published report page offers a same-tab link to its admin detail page for a reviewer
  Given <visitor> visits a published report's page
  Then the page offers a link to that report's admin detail page
  When the visitor activates that link
  Then the browser opens the report's admin detail page, in the same tab

Examples:
  | visitor                    |
  | a signed-in Administrator  |
  | a signed-in SafetyOfficer  |

@REQ-MOD-165
@ui
Scenario Outline: A published report page offers no admin link to a non-reviewer
  Given <visitor> visits a published report's page
  Then the page offers no link to the admin detail page

Examples:
  | visitor              |
  | a signed-in User      |
  | a signed-out visitor  |

@REQ-MOD-037
Scenario: The public feed lists only publishable reports, newest submitted first
  Given some reports are publishable and others are not
  When the public feed is queried
  Then the response is a deterministic paginated list containing only publishable reports
  And no non-publishable report ever appears
  And the list is newest submitted first, a tie broken by report ID, and each page names the cursor that continues it
  And no feed entry names its submission time
  And no cursor reveals a submission time

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

@REQ-MOD-184
@ui
Scenario: There is no admin page left to edit an answer's translation by hand
  Given a visitor signs in as an Administrator
  When the visitor navigates directly to /admin/answer-translations
  Then the page shows the not-found view

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

@REQ-MOD-125
@ui
Scenario: Manage reports shows each row's reporter and pilot names, blank when unanswered
  Given a safety officer is signed in and reports exist in several states
  When the safety officer opens Manage reports
  Then the pending row shows reporter name "Alex Rivera" and pilot name "Sam Chen"
  And the published row shows no reporter or pilot name

@REQ-MOD-153
@ui
Scenario: The public feed shows each report's attachment icon and count, omitted at zero
  Given the public feed holds a report with attachments and one with none
  When a visitor opens the public feed
  Then the report with attachments shows an attachment icon with its count, accessibly labelled
  And the report with none shows no attachment icon

@REQ-MOD-154
@ui
Scenario: Manage reports shows each row's attachment icon and count, omitted at zero
  Given a safety officer is signed in and Manage reports holds a report with attachments and one with none
  When the safety officer opens Manage reports
  Then the row with attachments shows an attachment icon with its count, accessibly labelled
  And the row with none shows no attachment icon

@REQ-MOD-053
@ui
Scenario: Choosing a filter on Manage reports narrows the list
  Given a safety officer is signed in and reports exist in several states
  When the safety officer opens Manage reports
  And the safety officer chooses the "Published" filter
  Then only published reports are listed
  And the chosen filter stays in the address bar

@REQ-MOD-120
@ui
Scenario Outline: Each row of Manage reports offers the quick actions its state allows
  Given a safety officer is signed in and reports exist in several states
  When the safety officer opens Manage reports
  Then the <row> row offers <actions>

Examples:
  | row                 | actions           |
  | pending             | Publish, Delete   |
  | published           | Unpublish, Delete |
  | unpublished         | Publish, Delete   |
  | private-unpublished | Delete            |
  | summary-failed      | Delete            |
  | stuck               | Delete            |

@REQ-MOD-121
@ui
Scenario: Publishing and unpublishing from the list updates the row in place
  Given a safety officer is signed in and reports exist in several states
  When the safety officer opens Manage reports
  And the safety officer publishes the pending row
  Then the pending row shows the "Published" badge and offers Unpublish
  When the safety officer unpublishes the published row
  Then the published row shows the "Unpublished" badge and offers Publish
  And each row action sent the version its row was listed with

@REQ-MOD-122
@ui
Scenario: Deleting from the list asks for confirmation first
  Given a safety officer is signed in and reports exist in several states
  When the safety officer opens Manage reports
  And the safety officer chooses Delete on the pending row
  Then a confirmation asks whether to delete it
  When the safety officer keeps the report
  Then the pending row is still listed and nothing was deleted
  When the safety officer chooses Delete on the pending row
  And the safety officer confirms
  Then the pending row is no longer listed and it was deleted

@REQ-MOD-123
@ui
Scenario: A stale row action tells the reviewer to reload the list
  Given a safety officer is signed in and reports exist in several states
  And another reviewer has changed the pending report since the list was loaded
  When the safety officer opens Manage reports
  And the safety officer publishes the pending row
  Then a message says the report changed and offers to reload the list
  And the pending row still shows the "Pending" badge

@REQ-MOD-130
Scenario Outline: Searching Manage reports finds a report matched by any part of it
  Given a report carries a distinct word in its <source>
  When a reviewer searches for that word
  Then the report is found

Examples:
  | source                          |
  | private answer                  |
  | choice label                    |
  | summary pair                    |
  | private note                    |
  | member comment                  |
  | reporter-uploaded attachment name |
  | staff-only attachment name       |

@REQ-MOD-131
Scenario: A misspelled search still finds the report
  Given a report carries a distinct word in its narrative
  When a reviewer searches for a misspelling of that word
  Then the report is found

@REQ-MOD-132
Scenario: A search matches across English and French stemming
  Given a report carries a distinct word in its narrative
  When a reviewer searches for that word
  Then the report is found

@REQ-MOD-133
Scenario: The best match is listed first
  Given two reports share a word, one repeating it and one only carrying a near-miss typo of it
  When a reviewer searches for that word
  Then the report repeating the word is listed before the one with the typo

@REQ-MOD-135
Scenario: A search stays within the chosen filter
  Given a pending report and a published report share a distinct word
  When a reviewer searches for that word within the published filter
  Then only the published report is found

@REQ-MOD-138
Scenario Outline: Only a reviewer may find a match inside private report content
  Given a report carries a distinct word only in its private answer, its private note, and its staff-only attachment name
  When <who> searches for that word
  Then <result>

Examples:
  | who                  | result                                 |
  | an anonymous visitor | the request is refused as unauthorized |
  | a member             | the request is refused as forbidden    |
  | a safety officer     | the report is found                    |
  | an administrator     | the report is found                    |

@REQ-MOD-139
Scenario: The search query text is never logged
  Given a report carries a distinct word in its narrative
  When a reviewer searches for that word
  Then the query text never appears in anything the host logs

@REQ-MOD-134
@ui
Scenario: Clearing the search box returns to newest submitted first
  Given a safety officer is signed in and reports exist in several states
  When the safety officer opens Manage reports
  And the safety officer searches for "Alex"
  And the safety officer clears the search box
  Then every report is listed newest first as before the search

@REQ-MOD-136
@ui
Scenario: The search text lives in the address bar and survives a reload
  Given a safety officer is signed in and reports exist in several states
  When the safety officer opens Manage reports
  And the safety officer searches for "Alex"
  Then the address bar carries "q=Alex"
  When the safety officer reloads the page
  Then the search box still reads "Alex"

@REQ-MOD-137
@ui
Scenario: A search matching nothing shows a message naming the query, not an error
  Given a safety officer is signed in and reports exist in several states
  When the safety officer opens Manage reports
  And the safety officer searches for a word that matches nothing
  Then a message says no reports match that search
  And no error is shown

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
  | yes/no | true       | English  | Yes                |
  | yes/no | true       | French   | Oui                |
  | yes/no | false      | English  | No                 |
  | yes/no | false      | French   | Non                |

@REQ-MOD-076
@ui
Scenario: A stored date that is not a real date is shown as stored
  Given a safety officer is signed in and a report with a date answer stored as "2026-13-45" exists
  And the interface language is English
  When the safety officer opens that report
  Then the answer reads "2026-13-45"

@REQ-MOD-118
@ui
Scenario Outline: A phone answer reads formatted, and one stored before phone numbers were validated reads as stored
  Given a safety officer is signed in and a report with a phone answer stored as "<stored>" exists
  And the interface language is <language>
  When the safety officer opens that report
  Then the answer reads "<shown>"
  And no translation is shown beside it

Examples:
  | stored        | language | shown            |
  | +16045551234  | English  | +1 604 555 1234  |
  | +16045551234  | French   | +1 604 555 1234  |
  | +442079460018 | English  | +44 20 7946 0018 |
  | 604-555-1234  | English  | 604-555-1234     |

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
Scenario: The public feed loads more reports automatically, and going back restores them
  Given the public feed has more published reports than fit on one page
  When a visitor scrolls to the end of the list
  Then the older reports load without a page change or an address change
  When a visitor opens one of them and goes back
  Then the same reports are still shown, at the same scroll position

@REQ-MOD-178
@ui
Scenario: Opening the public feed afresh starts at its top and loads its first page again
  Given the public feed has more published reports than fit on one page
  And the visitor's window is too short to show the whole feed
  When a visitor scrolls to the end of the list
  And the visitor follows the footer's link to the contact page, then the one back to View safety reports
  Then the public feed asks for its first page again
  And the public feed is shown from its top

@REQ-MOD-179
@ui
Scenario: Opening Manage reports afresh loads its first page again, not the list kept from earlier
  Given a safety officer is signed in and more reports exist than fit on one page
  When the safety officer goes to another page and opens Manage reports again from the Admin menu
  Then Manage reports asks for its first page again

@REQ-MOD-126
@ui
Scenario: The public feed's next page offers a keyboard-only fallback and announces itself
  Given the public feed has more published reports than fit on one page
  When a visitor opens the feed
  Then the "Load more" action is not visible
  When a keyboard visitor tabs to the "Load more" action
  Then it becomes visible
  When that visitor activates it
  Then the older reports load
  And a screen reader is told how many more reports loaded

@REQ-MOD-127
@ui
Scenario: The public feed offers a visible Retry action when its next page fails to load
  Given the public feed's next page fails to load
  When a visitor activates the "Load more" action without scrolling
  Then the feed offers a visible "Retry" action instead of failing silently

@REQ-MOD-128
@ui
Scenario: Manage reports loads more automatically and offers the same hidden fallback and visible retry
  Given a safety officer is signed in and more reports exist than fit on one page
  Then the "Load more" action is not visible
  When a keyboard visitor tabs to the "Load more" action
  Then it becomes visible
  When that visitor activates it
  Then the older reports load without leaving Manage reports
  Given the next report page fails to load
  When the safety officer activates the "Load more" action
  Then the list offers a visible "Retry" action instead of failing silently

@REQ-MOD-083
@ui
Scenario: A reviewer can open a published report's public page
  Given a safety officer is signed in and a published report exists
  When the safety officer opens that report
  Then the report view links to the report's public address
  And a report that is not published shows no such link

# Private notes (ADR-0133). Staff-only plain text on a report: never
# summarized, translated, or published.

@REQ-MOD-098
Scenario Outline: Only a Safety Officer or an Administrator may keep private notes
  Given a report carrying one private note
  When <who> adds, lists, edits, reads the history of, and removes private notes on it
  Then the API answers <outcome> to every one of those requests

Examples:
  | who                  | outcome            |
  | an anonymous visitor | 401                |
  | a User               | 403                |
  | a SafetyOfficer      | with success       |
  | an Administrator     | with success       |

@REQ-MOD-099
Scenario Outline: Staff add any number of private notes to a report in any status
  Given a <status> report that staff keep private notes on
  When a safety officer adds two private notes and an administrator adds a third
  Then all three private notes are listed, newest first
  And each lists its text, its writer's token subject, and when it was written
  And writing them queued no work for the Worker

Examples:
  | status         |
  | pending        |
  | published      |
  | unpublished    |
  | summary-failed |
  | no-consent     |

@REQ-MOD-100
Scenario: Editing a private note adds a revision and keeps every earlier one
  Given a safety officer wrote a private note on a report
  When an administrator edits that private note twice
  Then the private note lists the latest text, written by the administrator, marked as edited
  And its history lists all three revisions oldest first, each unchanged with its own text, writer, and time
  And an edit based on an earlier revision is refused with 409 and saves nothing

@REQ-MOD-101
Scenario: Removing a private note soft-deletes it
  Given a safety officer wrote a private note on a report and edited it once
  When an administrator removes that private note
  Then the private note is no longer listed, and editing it or reading its history answers 404
  And the private note and both its revisions are stamped deleted at one time, and nothing is erased
  And one audit entry records the administrator's token subject, RemovedPrivateNote, the note, and the time, without its text

@REQ-MOD-102
Scenario Outline: A private note is plain text of 1 to 4000 characters
  Given a pending report that staff keep private notes on
  When a safety officer adds a private note whose text is <text>
  Then the API answers 400 and no private note is stored

Examples:
  | text                 |
  | empty                |
  | only whitespace      |
  | 4001 characters long |

@REQ-MOD-103
Scenario: A deleted report's private notes go with it
  Given a report carrying one private note
  When a safety officer deletes that report
  Then the private note and its revision are stamped deleted at the report's deletion time
  And adding, listing, or editing private notes on that report answers 404

@REQ-MOD-104
Scenario: No public or member read ever returns a private note, not even a count
  Given a published report whose reporter consented to publication and media carries one private note
  When an anonymous visitor and a User read the public feed, that report's public page, and its comments
  Then no response carries the private note's text or identifier, or any count of private notes
  And no database view other than admin_report_search_document reads a private-note table

@REQ-MOD-105
Scenario: A private note never reaches the model or a translation provider
  Given a consented report carrying one private note is due for summarization
  When the Worker claims the message and builds the model input DTO
  Then the model input carries nothing from the private note
  And no outbox message names the private note or its revision

@REQ-MOD-106
@ui
Scenario: A safety officer keeps private notes on the report page
  Given a safety officer is signed in and a pending report exists
  And another reviewer left the private note "Called the pilot; follow up Monday."
  When the safety officer opens that report
  Then the private notes section lists "Called the pilot; follow up Monday." with its writer and time
  When the safety officer adds the private note "Investigator report requested."
  Then "Investigator report requested." is listed first, marked as theirs
  When the safety officer edits that private note to "Investigator report received."
  Then that private note reads "Investigator report received." and is marked as edited
  And its history shows both revisions
  When the safety officer removes that private note and confirms
  Then "Investigator report received." is no longer listed

@REQ-MOD-107
Scenario Outline: Only a Safety Officer or an Administrator may reach private attachments
  Given a report carrying one private attachment
  When <who> mints a private upload for, adds, lists, downloads, and removes private attachments on it
  Then the API answers <outcome> to every one of those private-attachment requests

Examples:
  | who                  | outcome      |
  | an anonymous visitor | 401          |
  | a User               | 403          |
  | a SafetyOfficer      | with success |
  | an Administrator     | with success |

@REQ-MOD-108
Scenario Outline: Staff add private attachments to a report in any status
  Given a <status> report that staff add private attachments to
  When a safety officer adds a private attachment with a description and then an administrator adds one without
  Then both private attachments are listed, newest first
  And each lists its file name, size, description, adder's token subject, and when it was added
  And adding them queued no work for the Worker
  And the report's detail view lists neither among its attachments

Examples:
  | status         |
  | pending        |
  | published      |
  | unpublished    |
  | summary-failed |
  | no-consent     |

@REQ-MOD-109
Scenario: Removing a private attachment soft-deletes it and keeps its bytes
  Given a report carrying one private attachment
  When an administrator removes that private attachment
  Then the private attachment is no longer listed, and downloading or removing it answers 404
  And its row is stamped deleted with the administrator's token subject, and its bytes are still stored
  And one audit entry records the administrator's token subject, RemovedPrivateAttachment, the attachment, and the time

@REQ-MOD-110
Scenario Outline: A private attachment needs a usable name, a short description, and a sent upload
  Given a pending report that staff add private attachments to
  When a safety officer adds a private attachment whose <field> is <value>
  Then the API answers 400 and no private attachment is stored

Examples:
  | field       | value                    |
  | file name   | empty                    |
  | file name   | only reserved characters |
  | description | 501 characters long      |
  | upload      | one that was never sent  |

@REQ-MOD-111
Scenario: A deleted report's private attachments go with it
  Given a report carrying one private attachment
  When a safety officer deletes the report carrying that private attachment
  Then the private attachment is stamped deleted at the report's deletion time, and its bytes are still stored
  And minting, adding, listing, or downloading private attachments on that report answers 404

@REQ-MOD-112
Scenario: No public or member read ever returns a private attachment, not even a count
  Given a published report whose reporter consented to publication and media carries one private attachment
  When an anonymous visitor and a User read the public feed, that report's public page, and its public media
  Then no response carries the private attachment's name, description, or identifier, or any count of private attachments
  And asking for the private attachment's identifier as public media answers 404
  And no database view other than admin_report_search_document reads the private-attachment table

@REQ-MOD-113
Scenario: A private attachment never reaches the model
  Given a consented report carrying one private attachment is due for summarization
  When the Worker claims the message and builds the model input DTO
  Then the model input carries nothing from the private attachment
  And no outbox message names the private attachment

@REQ-MOD-114
Scenario: A private note may refer to a private attachment on its own report only
  Given a report carrying one private attachment, and another report carrying one of its own
  When a safety officer adds a private note referring to the first report's private attachment
  Then the private note lists the private attachment it refers to, by identifier and file name
  When an administrator edits that private note to refer to no private attachment
  Then the private note refers to none, and its history shows the first revision still referring to it
  And a private note referring to the other report's private attachment is refused with 400 and nothing is stored
  And a private note referring to a removed private attachment is refused with 400

@REQ-MOD-115
@ui
Scenario: A safety officer stages, describes, adds, downloads, and removes a private attachment on the report page
  Given a safety officer is signed in and a pending report exists
  When the safety officer opens that report
  And the safety officer stages the private attachment "coroner-report.zip"
  Then the staged attachment "coroner-report.zip" finishes uploading and offers a description box
  When the safety officer describes the staged attachment "coroner-report.zip" as "Received from the coroner"
  And the safety officer adds the staged private attachments
  Then the private attachments section lists "coroner-report.zip" with its description, its adder, and when it was added
  When the safety officer downloads the private attachment "coroner-report.zip"
  Then the browser saves a file named "coroner-report.zip"
  When the safety officer removes the private attachment "coroner-report.zip" and confirms
  Then the private attachments section lists no attachments

@REQ-MOD-116
@ui
Scenario: A private note refers to a private attachment on the report page
  Given a safety officer is signed in and a pending report exists
  And the report carries the private attachment "police-report.pdf"
  When the safety officer opens that report
  And the safety officer adds the private note "See the police report." referring to "police-report.pdf"
  Then that private note shows that it refers to "police-report.pdf"

@REQ-MOD-117
@ui
Scenario: A safety officer cancels a private attachment while it uploads
  Given a safety officer is signed in and a pending report exists
  And storage is slow to accept a private attachment
  When the safety officer opens that report
  And the safety officer stages the private attachment "investigation-archive.zip"
  Then the staged attachment "investigation-archive.zip" shows its upload progress, offers to cancel it, and "Add 0 attachments" stays disabled
  When the safety officer cancels the staged upload "investigation-archive.zip"
  Then the staged attachment "investigation-archive.zip" is gone from the staging list
  And the cancelled upload is erased

@REQ-MOD-173
@ui
Scenario Outline: Several private attachments staged at once each upload independently
  Given a safety officer is signed in and a pending report exists
  When the safety officer opens that report
  And the safety officer <method> the private attachments "site-photo.jpg" and "weather-log.pdf" at once
  Then both staged attachments finish uploading independently, each with its own progress
  When the safety officer describes the staged attachment "site-photo.jpg" as "Taken at the site"
  And the safety officer describes the staged attachment "weather-log.pdf" as "Environment Canada log"
  And the safety officer adds the staged private attachments
  Then the private attachments section lists "site-photo.jpg" and "weather-log.pdf", each with its own description

Examples:
  | method                       |
  | drops                        |
  | chooses, through the picker, |

@REQ-MOD-174
@ui
Scenario: Removing a staged private attachment before it is added leaves the others staged
  Given a safety officer is signed in and a pending report exists
  When the safety officer opens that report
  And the safety officer drops the private attachments "keep-me.pdf" and "drop-me.pdf" at once
  Then both staged attachments finish uploading independently, each with its own progress
  When the safety officer removes the staged attachment "drop-me.pdf"
  Then only "keep-me.pdf" remains in the staging list, and nothing erases the upload for "drop-me.pdf"
  When the safety officer adds the staged private attachments
  Then the private attachments section lists "keep-me.pdf" only

@REQ-MOD-175
@ui
Scenario: A too-large private attachment is refused on its own row while the others proceed
  Given a safety officer is signed in and a pending report exists
  When the safety officer opens that report
  And the safety officer drops one ordinary private attachment and one larger than the private cap, at once
  Then the too-large attachment's staged row states the private cap and cannot be added
  And the ordinary attachment finishes uploading and offers a description box
  When the safety officer adds the staged private attachments
  Then the private attachments section lists only the ordinary attachment

@REQ-MOD-176
@ui
Scenario: "Add N attachments" is disabled until every staged private attachment has settled
  Given a safety officer is signed in and a pending report exists
  And storage is slow to accept a private attachment
  When the safety officer opens that report
  And the safety officer stages the private attachment "slow-upload.zip"
  Then "Add 0 attachments" stays disabled while "slow-upload.zip" uploads
  When storage finishes accepting the staged upload
  Then "Add 1 attachment" becomes enabled

@REQ-MOD-177
@ui
Scenario: Leaving the report page with staged, un-added private attachments warns
  Given a safety officer is signed in and a pending report exists
  When the safety officer opens that report
  And the safety officer stages the private attachment "unfinished.pdf"
  Then the staged attachment "unfinished.pdf" finishes uploading and offers a description box
  When the safety officer tries to close or reload the tab
  Then the browser's own unload prompt appears, with no custom text
  When the safety officer navigates away from the report through a link
  Then a bilingual dialog asks whether to leave, offering to stay
  When they keep the page
  Then the safety officer stays on the report page
  When the safety officer navigates away from the report through a link
  And they confirm leaving
  Then the safety officer leaves the report page

@REQ-MOD-180
@ui
Scenario: A staged private attachment cannot be removed or re-described while it is being added
  Given a safety officer is signed in and a pending report exists
  And the report is slow to accept a private attachment
  When the safety officer opens that report
  And the safety officer stages the private attachment "held.pdf"
  Then the staged attachment "held.pdf" finishes uploading and offers a description box
  When the safety officer adds the staged private attachments
  Then the staged attachment "held.pdf" can be neither removed nor re-described while it is added
  When the report finishes accepting the private attachment
  Then the private attachments section lists "held.pdf" only

@REQ-MOD-181
@ui
Scenario: Leaving the report page with only refused private attachments staged does not warn
  Given a safety officer is signed in and a pending report exists
  When the safety officer opens that report
  And the safety officer drops only a private attachment larger than the private cap
  Then the too-large attachment's staged row states the private cap and cannot be added
  When the safety officer reloads the report page
  Then the page reloads without warning, and the refused row is gone

@REQ-MOD-140
Scenario: Search matches the approved published summary in the visitor's site language
  Given a published report whose English summary says "The pilot landed in a field."
  When a visitor searches "landed" in English
  Then the report is listed among the results

@REQ-MOD-141
Scenario: Search matches a visible member comment as shown in the visitor's site language
  Given a published report carrying a member comment that says "Good reminder to check the fuel gauge."
  When a visitor searches "fuel gauge" in English
  Then the report is listed among the results

@REQ-MOD-142
Scenario: Search is scoped to the visitor's current site language only
  Given a published report whose French summary mentions a word its English summary does not
  When a visitor searches that French-only word in English
  Then the report is not listed among the results
  When a visitor searches that French-only word in French
  Then the report is listed among the results

@REQ-MOD-143
Scenario Outline: The public search never widens by caller role
  Given a published report whose summary contains a public word, and whose private answer, private note, and private attachment file name each hold their own word no summary or visible comment contains
  And another report is not publishable, and its summary contains a further private-only word
  When <who> searches for the public word
  Then the report is listed among the results
  When <who> searches for each private-only word
  Then no report is listed among the results, for every one of those searches

Examples:
  | who                  |
  | an anonymous visitor |
  | a User               |
  | a SafetyOfficer      |
  | an Administrator     |

@REQ-MOD-144
Scenario Outline: A non-publishable report's summary text never matches
  Given a <status> report whose summary would otherwise match
  When a visitor searches its summary's distinctive word
  Then the report is not listed among the results

Examples:
  | status      |
  | pending     |
  | unpublished |
  | no-consent  |
  | deleted     |

@REQ-MOD-145
Scenario: A hidden or a deleted comment never matches
  Given a published report carrying a comment that is later hidden
  And another published report carrying a comment that is later deleted
  When a visitor searches the hidden comment's distinctive word
  Then the report with the hidden comment is not listed among the results
  When a visitor searches the deleted comment's distinctive word
  Then the report with the deleted comment is not listed among the results

@REQ-MOD-146
Scenario Outline: A typo or a missing accent still finds the best match
  Given a published report whose English summary says "The pilot landed in a field." and whose French summary says "Le pilote s'est posé dans un champ."
  When a visitor searches <query> in <language>
  Then the report is listed among the results

Examples:
  | query      | language |
  | "landde"   | English  |
  | "pose"     | French   |

@REQ-MOD-147
Scenario: Best match ranks first while a query is active
  Given a published report whose summary contains the exact phrase "hydraulic leak"
  And another published report whose summary only misspells "hydraulic leak"
  When a visitor searches "hydraulic leak" in English
  Then the exact match is ranked above the misspelled match

@REQ-MOD-148
Scenario: An empty search box lists newest submitted first, unchanged
  Given some reports are publishable and others are not
  When the public feed is queried with a blank search box
  Then a blank search box's first page is identical to the plain feed's first page

@REQ-MOD-149
@ui
Scenario: The search box sits at the top of the public feed, and its query is bookmarkable
  Given the public feed has published reports
  When a visitor opens View safety reports
  And the visitor types a search term into the search box at the top of the page
  Then the address bar carries that search term as ?q=
  And only matching reports are listed
  When the page reloads
  Then the search box still shows that search term, and only matching reports are listed
  When the visitor goes back
  Then the search box is empty and the full feed is shown again

@REQ-MOD-178
@ui
Scenario: Leaving the summary editor with unsaved changes is confirmed before they are discarded
  Given a safety officer is signed in and a pending report exists
  When the safety officer opens that report
  And the safety officer opens the summary editor
  And types into the English text without saving
  And navigates to another admin page
  Then a bilingual dialog asks whether to leave, offering to stay
  When they confirm leaving
  Then the browser navigates to that page and the edit is gone

@REQ-MOD-179
@ui
Scenario: Leaving the type-ahead value review queue with an uncorrected draft is confirmed
  Given a signed-in Safety Officer and two type-ahead questions with values flagged for review
  When they open the review-type-ahead-values page
  And they begin correcting "Coopers"
  And they edit its English wording to "Cooper's Hill"
  And they navigate to another admin page
  Then a bilingual dialog asks whether to leave, offering to stay
  When they confirm leaving
  Then the browser navigates to that page and the correction is gone

@REQ-MOD-180
@ui
Scenario: Leaving with an unsaved private note is confirmed
  Given a safety officer is signed in and a pending report exists
  When the safety officer opens that report
  And the safety officer starts writing a private note without saving it
  And navigates to another admin page
  Then a bilingual dialog asks whether to leave, offering to stay
