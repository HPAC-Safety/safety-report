Feature: Moderation, authentication, and publication
  Members authenticate through a small port, get one of two roles, review
  reports, and only a fully approved, consented, non-deleted report ever
  reaches the public feed.

  Scenario: Successful authentication issues a short-lived secure cookie
    Given a member authenticates through IMemberAuthenticator
    And the local allowlist grants that identity a role
    When authentication succeeds
    Then a short-lived Secure, HttpOnly, SameSite cookie is issued

  Scenario: Credentials are never stored, logged, or cached
    Given a member submits credentials to the authenticator adapter
    When the adapter proxies them to the upstream member login endpoint
    Then the credentials are never stored, logged, cached, enqueued, or put in
      a URL
    And the adapter uses no caller-supplied upstream host and follows a narrow
      redirect policy with timeouts

  Scenario: Login does not reveal allowlist membership
    Given an identity is not on the admin allowlist
    When that identity attempts to sign in
    Then the login response does not reveal whether the identity is
      allowlisted
    And the attempt is subject to trusted-IP rate limiting and per-identity
      lockout

  Scenario: A revoked or soft-deleted admin's session becomes invalid
    Given an admin is currently signed in
    When that admin is revoked or soft-deleted
    Then the admin's session becomes invalid

  Scenario: Every operation is authorized by the API, not just the UI
    Given an authenticated member without the required role calls an admin
      operation
    When the API processes the request
    Then the API rejects the operation regardless of what the UI would have
      shown

  Scenario: SafetyOfficer capabilities
    Given a member has the SafetyOfficer role
    Then the member can view the review queue and private report material
    And view safe image/video derivatives and download validated unredacted
      documents
    And edit the bilingual summary pair
    And approve, reject, publish, and soft-delete reports

  Scenario: Administrator capabilities include everything SafetyOfficer has
    Given a member has the Administrator role
    Then the member has every SafetyOfficer capability
    And can additionally create question revisions and manage the admin
      allowlist and roles

  Scenario: Sensitive admin actions are audited without report content
    Given a sensitive read or material mutation occurs in the admin
      application
    When the action completes
    Then an audit entry records actor, action, target, and time
    And it never records report content

  Scenario: The review queue shows reports needing action
    Given reports exist in various non-deleted states
    When a reviewer opens the default review queue
    Then it shows reports that are submitted/stuck, summarizing beyond their
      expected age, summary failed, or pending review

  Scenario: A report detail view exposes only what the reviewer needs
    Given a reviewer opens a report's detail view
    When the detail query runs
    Then it supplies the reporter language, exact bilingual question labels
      and answers with privacy indicated, processing state, both summary
      texts with their shared provenance/approval, and short-lived links only
      for successful image/video derivatives or validated private documents

  Scenario: Editing a summary clears approval and unpublishes
    Given a reviewer edits either summary language
    When the edit is saved
    Then the pair's approval is cleared
    And a previously published report is unpublished

  Scenario: Approval applies once to the current bilingual pair
    Given a reviewer approves the current English/French summary pair
    When the approval is recorded
    Then it applies to that pair as a whole, not to one language

  Scenario: Rejection blocks publication but keeps the report for learning
    Given a reviewer rejects a report
    When the rejection is recorded
    Then the report can never satisfy the publication invariant
    And the report remains available for internal learning

  Scenario: Publication requires every guard to pass, with no bypass
    Given a report is non-deleted, has explicit positive consent, has two
      nonblank summary texts, and has current human approval of the pair
    When the report is published
    Then publication succeeds
    And no Administrator, migration, background worker, or direct API caller
      can bypass any of these guards

  Scenario: The public DTO exposes only the approved summary and its metadata
    Given a report is published
    When the public API returns it
    Then the response contains only the opaque report ID, ai_summary_en,
      ai_summary_fr, and the publication timestamp
    And it never contains question keys, labels, answers, consent value,
      report language, private flags, raw reports, attachment metadata or
      URLs, admin identities, model provenance, or audit records

  Scenario: There is no publication channel besides the HPAC public feed
    Given a report becomes publishable
    When it is published
    Then it appears only on the HPAC public feed and report-detail page
    And no email, messaging, social, webhook, or third-party channel
      publishes it

  Scenario: Soft-deleting a report stops it everywhere immediately
    Given a report exists in any state
    When a safety officer soft-deletes it
    Then it is immediately removed from the public feed and normal review
      queries
    And ordinary Worker processing for it stops

  Scenario: Soft-deleting an admin preserves historic audit attribution
    Given an admin user is soft-deleted
    When the deletion is recorded
    Then the admin's current access is revoked
    And historic audit attribution to that admin is preserved
    And there is no restore workflow and no UI action that physically
      deletes either record
