Feature: Comments
Any member may comment on a published report. Everyone can read the
comments in either official language. Authors can edit or delete their own,
and reviewers can hide any of them.

@REQ-COM-001
Scenario: A member comments on a published report
  Given a report is published
  And the visitor is a member
  When the member posts a comment on it
  Then the comment is created and returned to its author
  And the comment is listed on that report for every reader, signed in or not

@REQ-COM-002
Scenario: Commenting requires a member
  Given a report is published
  When a request without a member token posts a comment on it
  Then the comment is refused as unauthenticated
  And no comment is stored

@REQ-COM-003
Scenario: A report no visitor can see cannot be commented on
  Given a report is not public
  And the visitor is a member
  When the member posts a comment on it
  Then the report is not found
  And no comment is stored

@REQ-COM-004
Scenario Outline: A comment must have text, and at most 2000 characters
  Given a report is published
  And the visitor is a member
  When the member posts a comment whose text is <text>
  Then the comment is refused as invalid
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
  And the visitor is a member
  And no translation provider is reachable
  When the member posts a comment on it
  Then the comment is created
  And one translation job for the comment is waiting for the Worker

@REQ-COM-007
Scenario: A reader is told which comments are theirs, and never who wrote the others
  Given two members have each commented on a published report
  When the first member reads the report's comments
  Then only the first member's comment is marked as theirs
  And no comment carries its author's token subject or any other identity
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
  And the comment and its revisions are deleted, not erased

@REQ-COM-010
Scenario Outline: Nobody may change another member's comment
  Given a member commented on a published report
  And the visitor is another member
  When the other member tries to <action> the comment
  Then the attempt is refused as forbidden
  And the comment is unchanged

Examples:
  | action |
  | edit   |
  | delete |

@REQ-COM-011
Scenario: A reviewer hides a comment, and the hiding is audited
  Given a member commented on a published report
  When a Safety Officer hides the comment
  Then the comment is no longer listed and the report's comment count drops by one
  And the audit log records who hid it, without its text
  And the comment is kept in the database

@REQ-COM-012
Scenario: A member who is not a reviewer cannot hide a comment
  Given a member commented on a published report
  And the visitor is another member
  When the other member tries to hide the comment
  Then the attempt is refused as forbidden
  And the comment is still listed

@REQ-COM-013
Scenario: Unpublishing a report hides its comments
  Given a member commented on a published report
  When a reviewer unpublishes the report
  Then a visitor sees no comments for it and the report is not in the feed
  And the comment is kept in the database

@REQ-COM-022
Scenario: Publishing a report again brings its comments back
  Given a member commented on a published report that a reviewer has since unpublished
  When a reviewer publishes the report again
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
Scenario: An anonymous visitor is invited to sign in to comment
  Given a published report has comments
  When an anonymous visitor opens it
  Then the comments are shown, each labelled "Member"
  And the page offers to sign in to comment, with no comment box

@REQ-COM-023
@ui
Scenario: An anonymous visitor who signs in to comment returns to the report
  Given an anonymous visitor is reading a published report that has comments
  When the visitor signs in through the report's invitation to comment
  Then the visitor is returned to the report

@REQ-COM-017
@ui
Scenario: A member posts a comment, labelled as their own
  Given the member is reading a published report that has comments
  When the member posts a comment
  Then the comment is listed, labelled "You"

@REQ-COM-024
@ui
Scenario: A member reading a published report is offered a comment box with a reminder not to identify anyone
  Given the visitor is a member and a published report has comments
  When the member opens the report
  Then a comment box is shown with a reminder not to name or identify people

@REQ-COM-025
@ui
Scenario: A member edits their own comment
  Given the member is reading a published report that carries a comment of their own
  When the member edits that comment
  Then the comment shows the new text, marked as edited

@REQ-COM-026
@ui
Scenario: A member deletes their own comment
  Given the member is reading a published report that carries a comment of their own
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

@REQ-COM-027
@ui
Scenario: Switching the site's language shows a translated comment as it was written, with no translation icon
  Given a visitor is reading, in French, a published report with a comment written in English and machine-translated into French
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
  Given a Safety Officer is reading a published report that has comments
  When the Safety Officer hides a comment and confirms
  Then that comment is no longer listed

@REQ-COM-028
@ui
Scenario: A reviewer is offered to hide every comment on the report page
  Given a Safety Officer is on the admin site and a published report has comments
  When the Safety Officer opens the report
  Then every comment offers to hide it
