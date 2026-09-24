---
title: Comments
description: Supporting detail for the member comment, translation, and moderation scenarios.
type: spec
area: comments
---

# Comments

Supporting detail for [`comments.feature`](comments.feature). The decision and
its rejected alternatives are in
[ADR-0114](../../docs/decisions/ADR-0114-members-may-comment-on-a-published-report.md).

## Who may do what

| Action | Who | Where |
|---|---|---|
| Read a report's comments | Anyone | `GET /api/v1/public/reports/{id}/comments` |
| Post a comment | Any signed-in member | `POST /api/v1/public/reports/{id}/comments` |
| Edit or delete a comment | Its author only | `PUT` / `DELETE /api/v1/public/reports/{id}/comments/{commentId}` |
| Hide a comment | A safety officer or administrator | `POST /api/admin/comments/{commentId}/hide` |

Every write needs the report to be public at that moment. A report that is not
public answers `404`, exactly as its detail does. The author is the token's
subject, compared on the server. The API never returns a subject: a signed-in
reader gets `isMine` on each comment, and nothing else about who wrote it.

## What a comment looks like

Each comment is labelled "Member", or "You" on the reader's own. Every comment
is machine-translated into the other official language, and a reader sees
each one in the site's language, the one chosen with the header's language
toggle. As with a report's summary, there is no per-comment language control:

- **Written in the reader's language:** the text as written.
- **Translated:** the machine translation, with a small, muted translation
  icon beside the author label. Its accessible name and tooltip are
  "Translated automatically".
- **Not translated yet:** the original, marked as awaiting translation, until
  the Worker supplies it.

A visitor who is not signed in sees "Sign in to comment". It opens the member
login with `returnTo` set to the report. The login page follows it only to a
path on this site, and brings the member back to the report they were
reading.

An edited comment is marked "Edited". The comment box reminds the member not
to name or identify anyone: comments are public, and nothing anonymizes them.
It counts characters against the 2000 limit.

## Where the numbers come from

The API reads comments from the `public_report_comments` view and the count
from `public_reports.comment_count`. Both count only comments that are neither
deleted nor hidden, on reports the public can see. Unpublishing a report
removes it and its comments from every public read. Publishing it again
brings them back as they were.

## Out of scope

What not to build here
([ADR-0083](../../docs/decisions/ADR-0083-specification-driven-development.md)):

- The author's name, email address, or HPAC number (#413).
- Replies, threads, reactions, or votes.
- Notifying anyone of a comment.
- Attachments, links rendered as links, or formatting in a comment.
- A reviewer queue or search of comments, and un-hiding a comment.
- Pre-moderation, or holding a comment until a reviewer approves it.
- A rate limit on posting beyond the member token and the length cap.
- Editing or deleting another member's comment, even as an administrator.
  A reviewer hides it instead.
