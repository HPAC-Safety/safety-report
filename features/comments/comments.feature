Feature: Comments
Any signed-in member may comment on a published report. Everyone can read the
comments in either official language. Authors can edit or delete their own,
and reviewers can hide any of them.

@REQ-COM-001
Scenario: A signed-in member comments on a published report
  Given a report is published
  And a member is signed in
  When the member posts a comment on it
  Then the API answers 201 with the comment
  And the comment is listed on that report for every reader, signed in or not

@REQ-COM-002
Scenario: Commenting requires a member
  Given a report is published
  When a request without a member token posts a comment on it
  Then the API answers 401
  And no comment is stored

@REQ-COM-003
Scenario: A report the public cannot see cannot be commented on
  Given a report is not public
  And a member is signed in
  When the member posts a comment on it
  Then the API answers 404
  And no comment is stored

@REQ-COM-004
Scenario Outline: A comment must have text, and at most 2000 characters
  Given a report is published
  And a member is signed in
  When the member posts a comment whose text is <text>
  Then the API answers 400
  And no comment is stored

Examples:
  | text                     |
  | empty                    |
  | only whitespace          |
  | 2001 characters long     |

@REQ-COM-005
Scenario: A comment is stored in the language it was written in, and the Worker supplies the other
  Given a member posted a comment in French
  When the Worker processes the comment's translation work
  Then the comment keeps its French text
  And its English text is the machine translation, recorded as "auto"

@REQ-COM-006
Scenario: Posting a comment never waits for, or calls, a translation provider
  Given a report is published
  And a member is signed in
  And no translation provider is reachable
  When the member posts a comment on it
  Then the API answers 201
  And one translation job for the comment is waiting for the Worker

@REQ-COM-007
Scenario: The API tells a reader which comments are theirs and never who wrote the others
  Given two members have each commented on a published report
  When the first member reads the report's comments
  Then only the first member's comment is marked as theirs
  And no comment carries its author's subject or any other identity
  And an anonymous reader sees no comment marked as theirs

@REQ-COM-008
Scenario: An author's edit adds a revision and keeps the one before it
  Given a member commented on a published report
  When the member edits the comment
  Then the comment shows the new text, marked as edited
  And the earlier text is kept as an earlier revision
  And the new text waits for its own translation

@REQ-COM-009
Scenario: An author's deleted comment disappears but is not erased
  Given a member commented on a published report
  When the member deletes the comment
  Then the comment is no longer listed and the report's comment count drops by one
  And the comment and its revisions are soft-deleted, not removed from the database

@REQ-COM-010
Scenario Outline: Nobody may change another member's comment
  Given a member commented on a published report
  And another member is signed in
  When the other member tries to <action> the comment
  Then the API answers 403
  And the comment is unchanged

Examples:
  | action |
  | edit   |
  | delete |

@REQ-COM-011
Scenario: A reviewer hides a comment, and the hiding is audited
  Given a member commented on a published report
  When a safety officer hides the comment
  Then the comment is no longer listed and the report's comment count drops by one
  And the audit log records who hid it, without its text
  And the comment is kept in the database

@REQ-COM-012
Scenario: A member who is not a reviewer cannot hide a comment
  Given a member commented on a published report
  And another member is signed in
  When the other member tries to hide the comment
  Then the API answers 403
  And the comment is still listed

@REQ-COM-013
Scenario: Unpublishing a report hides its comments, and publishing it again brings them back
  Given a member commented on a published report
  When a reviewer unpublishes the report
  Then the public API lists no comments for it and the report is not in the feed
  And the comment is kept in the database
  When a reviewer approves the report again
  Then the comment is listed again

@REQ-COM-014
Scenario: The public feed carries each report's comment count
  Given a published report has two visible comments and one hidden comment
  When the public feed is read
  Then that report's comment count is 2

@REQ-COM-015
@ui
Scenario: The feed shows how many comments each report has
  Given the public feed has published reports with comments
  When a visitor opens View safety reports
  Then each report shows its number of comments

@REQ-COM-016
@ui
Scenario: A visitor who is not signed in is invited to sign in to comment
  Given a published report has comments
  When a visitor who is not signed in opens it
  Then the comments are shown, each labelled "Member"
  And instead of a comment box the page offers to sign in to comment
  When the visitor signs in from there
  Then the visitor is returned to the report

@REQ-COM-017
@ui
Scenario: A signed-in member posts, edits, and deletes their own comment
  Given a member is signed in and a published report has comments
  When the member opens the report
  Then a comment box is shown with a reminder not to name or identify people
  When the member posts a comment
  Then the comment is listed, labelled "You"
  When the member edits that comment
  Then the comment shows the new text, marked as edited
  When the member deletes that comment and confirms
  Then the comment is no longer listed
  And no other member's comment offers to edit or delete it

@REQ-COM-018
@ui
Scenario: A reader sees every comment in the site's language, a translated one marked by a subtle icon
  Given a published report has a comment written in English and machine-translated into French
  When a visitor reads the report in French
  Then the comment shows its French text, with a small icon that says it was translated automatically
  And the comment offers no control to show the original
  When the visitor switches the site's language
  Then the comment shows its English text, with no translation icon

@REQ-COM-019
@ui
Scenario: A comment still awaiting translation shows its original text
  Given a published report has a comment written in English that has no French text yet
  When a visitor reads the report in French
  Then the comment shows its English text, marked as awaiting translation

@REQ-COM-020
@ui
Scenario: A reviewer hides a comment from the report page
  Given a safety officer is signed in and a published report has comments
  When the safety officer opens the report
  Then every comment offers to hide it
  When the safety officer hides a comment and confirms
  Then that comment is no longer listed
