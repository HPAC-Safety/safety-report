Feature: Moderation, authentication, and publication
  Members authenticate through a small port, get one of two roles, review
  reports, and only a fully approved, consented, non-deleted report ever
  reaches the public feed.

  @ui
  Scenario: The member login page presents credential fields and a third-party sign-in option
    Given a visitor activates the member-login action
    Then the login page shows a username field, a password field, a third-party sign-in option, and a login action

  @ui
  Scenario: A member's signed-in session persists across a reload and clears on logout
    Given a visitor signs in from the member login page
    Then the header shows a logout action instead of the member-login action
    When the page reloads
    Then the header still shows the logout action
    When the visitor activates the logout action
    Then the header shows the member-login action again

  @ui
  Scenario: A signed-in member's header exposes an Admin menu with manage-reports and manage-questions options
    Given a visitor signs in from the member login page
    Then the header shows an Admin menu and no other header nav change
    When the visitor activates the Admin menu
    Then it opens with manage-reports and manage-questions options

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

  @ignore
  Scenario: Signing in through the third-party option completes the same authentication path
    Given a member selects the third-party sign-in option on the login page
    When that identity flow completes successfully
    Then the result is authenticated the same way as the credential form, through IMemberAuthenticator
    And the local allowlist still governs whether that identity receives a role

  @ignore
  Scenario: Successful authentication issues a short-lived secure cookie
    Given a member authenticates through IMemberAuthenticator
    And the local allowlist grants that identity a role
    When authentication succeeds
    Then a short-lived Secure, HttpOnly, SameSite cookie is issued

  @ignore
  Scenario: Credentials are never stored, logged, or cached
    Given a member submits credentials to the authenticator adapter
    When the adapter proxies them to the upstream member login endpoint
    Then the credentials are never stored, logged, cached, enqueued, or put in a URL
    And the adapter uses no caller-supplied upstream host and follows a narrow redirect policy with timeouts

  @ignore
  Scenario: Login does not reveal allowlist membership
    Given an identity is not on the admin allowlist
    When that identity attempts to sign in
    Then the login response does not reveal whether the identity is allowlisted
    And the attempt is subject to trusted-IP rate limiting and per-identity lockout

  @ignore
  Scenario: A revoked or soft-deleted admin's session becomes invalid
    Given an admin is currently signed in
    When that admin is revoked or soft-deleted
    Then the admin's session becomes invalid

  @ignore
  Scenario: Every operation is authorized by the API, not just the UI
    Given an authenticated member without the required role calls an admin operation
    When the API processes the request
    Then the API rejects the operation regardless of what the UI would have shown

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
    And can additionally create question revisions and manage the admin allowlist and roles

  @ignore
  Scenario: Only an active Administrator manages the allowlist
    Given a member is not an active Administrator
    When that member attempts to list, add, change, revoke, or delete an allowlist entry
    Then the API rejects the attempt

  @ignore
  Scenario: A stale allowlist change fails instead of overwriting a newer decision
    Given two Administrators load the same allowlist entry
    When one saves a role or access change and the other then submits a stale concurrency token
    Then the second, stale change is rejected
    And the first change is not overwritten

  @ignore
  Scenario: Sensitive admin actions are audited without report content
    Given a sensitive read or material mutation occurs in the admin application
    When the action completes
    Then an audit entry records actor, action, target, and time
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
    And it never contains question keys, labels, answers, consent value, report language, private flags, raw reports, attachment metadata or URLs, admin identities, model provenance, or audit records

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
  Scenario: Soft-deleting an admin preserves historic audit attribution
    Given an admin user is soft-deleted
    When the deletion is recorded
    Then the admin's current access is revoked
    And historic audit attribution to that admin is preserved
    And there is no restore workflow and no UI action that physically deletes either record
