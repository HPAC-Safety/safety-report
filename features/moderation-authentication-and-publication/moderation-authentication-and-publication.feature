Feature: Moderation, authentication, and publication
  Members present a signed token, get one of three roles from its claims,
  review reports, and only a fully approved, consented, non-deleted report ever
  reaches the public feed.

  @ui @ignore
  Scenario: In development the login page offers no third-party sign-in option
    Given a visitor activates the member-login action
    Then the login page shows a username field, a password field, and a login action
    And the login page shows no third-party sign-in option

  @ui @ignore
  Scenario: Where a third-party provider is configured, the login page offers it
    Given the API reports that a third-party provider is configured
    When a visitor activates the member-login action
    Then the login page also shows a third-party sign-in option

  @ui @ignore
  Scenario: Signing in with member credentials returns a session that survives a reload
    Given a visitor signs in with valid member credentials
    Then the header shows a logout action instead of the member-login action
    When the page reloads
    Then the header still shows the logout action

  @ui @ignore
  Scenario: Bad credentials show one generic failure and no session
    Given a visitor submits credentials that are not valid
    Then the login page shows one generic failure message
    And the failure does not say whether the username or the password was wrong
    And the header still shows the member-login action

  @ui
  Scenario: A member's signed-in session persists across a reload and clears on logout
    Given a visitor signs in from the member login page
    Then the header shows a logout action instead of the member-login action
    When the page reloads
    Then the header still shows the logout action
    When the visitor activates the logout action
    Then the header shows the member-login action again

  @ui @ignore
  Scenario: A signed-in Administrator's Admin menu offers every option
    Given a visitor signs in as an Administrator
    Then the header shows an Admin menu and no other header nav change
    When the visitor activates the Admin menu
    Then it opens with manage-reports, manage-questions, and manage-choice-lists options

  @ui @ignore
  Scenario: A signed-in SafetyOfficer's Admin menu offers manage-reports only
    Given a visitor signs in as a SafetyOfficer
    When the visitor activates the Admin menu
    Then it opens with a manage-reports option
    And it offers no manage-questions or manage-choice-lists option

  @ui @ignore
  Scenario: A signed-in User sees no Admin menu
    Given a visitor signs in as a User
    Then the header shows a logout action
    And the header shows no Admin menu

  @ui
  Scenario: An open Admin menu keeps every option on a single line
    Given a visitor signs in from the member login page
    When the visitor activates the Admin menu
    Then every option is on one line and none is truncated

  @ui
  Scenario Outline: Activating an Admin menu option navigates to its placeholder page
    Given a visitor signs in from the member login page
    When the visitor activates the Admin menu
    And the visitor activates the <option> option
    Then the browser navigates to the <destination> placeholder page

    Examples:
      | option           | destination       |
      | Manage reports   | manage-reports     |
      | Manage questions | manage-questions   |

  @ui
  Scenario: The Admin menu is absent for a signed-out visitor
    Given a visitor loads the homepage
    Then the header shows no Admin menu

  Scenario: A token signed by an unknown key is rejected
    Given a bearer token signed with a key the API does not trust
    When it is presented to any authenticated endpoint
    Then the API refuses the request
    And it does not disclose why the token was refused

  Scenario: A token whose signature has been altered is rejected
    Given a validly issued bearer token whose signature segment has been changed
    When it is presented to any authenticated endpoint
    Then the API refuses the request

  Scenario: An expired token is rejected
    Given a bearer token whose expiry has passed
    When it is presented to any authenticated endpoint
    Then the API refuses the request

  Scenario: A token for the wrong audience is rejected
    Given a bearer token issued for a different audience
    When it is presented to any authenticated endpoint
    Then the API refuses the request

  Scenario: A token with no recognized role claim authenticates as User
    Given a validly signed bearer token carrying no recognized role claim
    When it is presented to the API
    Then the request is authenticated
    And the identity has the User role and no administrative capability

  Scenario: The API never reads a name, an email, or any other claim
    Given a validly signed bearer token carrying a name, an email, and a picture claim
    When the API establishes the caller's identity
    Then it reads only the subject and the role claim
    And no other claim reaches domain code, a log, or the database

  @ignore
  Scenario: The development token endpoint does not exist outside development
    Given the API is not running in development
    When the development token endpoint is called
    Then the route does not exist

  @ignore
  Scenario: An unauthenticated request to an admin endpoint is refused before the handler
    Given a request carries no bearer token
    When it reaches an admin endpoint
    Then the API refuses it before the handler runs

  @ignore
  Scenario: Every operation is authorized by the API, not just the UI
    Given an authenticated member without the required role calls an admin operation
    When the API processes the request
    Then the API rejects the operation regardless of what the UI would have shown

  @ignore
  Scenario: User capabilities
    Given a member has the User role
    Then the member can submit an occurrence report
    And the member has no review, authoring, or publication capability

  @ignore
  Scenario: SafetyOfficer capabilities
    Given a member has the SafetyOfficer role
    Then the member can view the review queue and private report material
    And view safe image/video derivatives and download validated unredacted documents
    And edit the bilingual summary pair
    And approve, reject, publish, and soft-delete reports

  @ignore
  Scenario: Administrator capabilities include everything SafetyOfficer has
    Given a member has the Administrator role
    Then the member has every SafetyOfficer capability
    And can additionally create question revisions and manage shared choice lists

  @ignore
  Scenario Outline: Only an Administrator may author a question revision
    Given a member has the <role> role
    When that member attempts to create a question revision
    Then the API <outcome> the attempt

    Examples:
      | role          | outcome  |
      | User          | rejects  |
      | SafetyOfficer | rejects  |
      | Administrator | accepts  |

  @ignore
  Scenario: Sensitive admin actions are audited without report content
    Given a sensitive read or material mutation occurs in the admin application
    When the action completes
    Then an audit entry records the acting token subject, action, target, and time
    And the subject is stored as an opaque string that joins to no user record
    And it never records report content

  @ignore
  Scenario: The review queue shows reports needing action
    Given reports exist in various non-deleted states
    When a reviewer opens the default review queue
    Then it shows reports that are submitted/stuck, summarizing beyond their expected age, summary failed, or pending review

  @ignore
  Scenario: A report detail view exposes only what the reviewer needs
    Given a reviewer opens a report's detail view
    When the detail query runs
    Then it supplies the reporter language, exact bilingual question labels and answers with privacy indicated, processing state, both summary texts with their shared provenance/approval, and short-lived links only for successful image/video derivatives or validated private documents

  @ignore
  Scenario: Editing a summary clears approval and unpublishes
    Given a reviewer edits either summary language
    When the edit is saved
    Then the pair's approval is cleared
    And a previously published report is unpublished

  @ignore
  Scenario: Approval applies once to the current bilingual pair
    Given a reviewer approves the current English/French summary pair
    When the approval is recorded
    Then it applies to that pair as a whole, not to one language
    And the approving token subject is recorded as an opaque string

  @ignore
  Scenario: Rejection blocks publication but keeps the report for learning
    Given a reviewer rejects a report
    When the rejection is recorded
    Then the report can never satisfy the publication invariant
    And the report remains available for internal learning

  @ignore
  Scenario: Publication requires every guard to pass, with no bypass
    Given a report is non-deleted, has explicit positive consent, has two nonblank summary texts, and has current human approval of the pair
    When the report is published
    Then publication succeeds
    And no Administrator, migration, background worker, or direct API caller can bypass any of these guards

  @ignore
  Scenario: The public DTO exposes only the approved summary and its metadata
    Given a report is published
    When the public API returns it
    Then the response contains only the opaque report ID, ai_summary_en, ai_summary_fr, and the publication timestamp
    And it never contains question keys, labels, answers, consent value, report language, private flags, raw reports, attachment metadata or URLs, member or reviewer identities, model provenance, or audit records

  @ignore
  Scenario: The public feed lists only publishable reports
    Given some reports are publishable and others are not
    When the public feed is queried
    Then the response is a deterministic paginated list containing only publishable reports
    And no non-publishable report ever appears

  @ignore
  Scenario: An unknown or non-public report id returns 404
    Given a report id is unknown, deleted, unapproved, rejected, or not consented
    When the public API is asked for that report
    Then the API returns 404
    And non-public ids are indistinguishable from unknown ids

  @ignore
  Scenario: There is no publication channel besides the HPAC public feed
    Given a report becomes publishable
    When it is published
    Then it appears only on the HPAC public feed and report-detail page
    And no email, messaging, social, webhook, or third-party channel publishes it

  @ignore
  Scenario: Soft-deleting a report stops it everywhere immediately
    Given a report exists in any state
    When a safety officer soft-deletes it
    Then it is immediately removed from the public feed and normal review queries
    And ordinary Worker processing for it stops

  @ignore
  Scenario: Revoking a member's access is the identity provider's decision
    Given a member's access is revoked at the identity provider
    When their current token expires or stops being issued
    Then they can no longer authenticate
    And this system holds no record of them to revoke
    And historic audit rows keep the opaque subject they were written with
