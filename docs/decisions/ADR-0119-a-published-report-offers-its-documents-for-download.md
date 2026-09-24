---
title: A published report offers its documents for download
description: A validated document on a published report is public by default when the reporter consented under media-consent wording that names documents, is served unchanged as a short-lived forced download under a server-minted name, and is moderated after the fact like photos and video.
type: adr
status: accepted
date: 2026-09-24
decision-makers: Chase Florell
keywords: documents, attachments, publication, consent, pre-signed URLs, public page, moderation, privacy, ADR-0117, ADR-0026, ADR-0089
---

# ADR-0119 — A published report offers its documents for download

**Status:** Accepted. **Amends**
[ADR-0117](ADR-0117-a-published-report-shows-the-reporters-photos-and-video.md):
a document is no longer excluded from the public page, and media consent now
covers documents. **Amends**
[ADR-0026](ADR-0026-presigned-urls-and-private-blob-storage.md): an anonymous
visitor may be issued a short-lived pre-signed GET to a document's private
original, and only to one this record makes public. Changes `AGENTS.md`
invariant 5: a document may now be published, as a forced download.

## Context

ADR-0117 made a published report's photos and video public and kept every
document private. HPAC wants a reporter's supporting documents — a checklist,
a written account, a manufacturer's bulletin — to reach the membership the
same way (#428). Safety officers and administrators already see every
attachment.

A document is unlike a photo in one way that matters. A photo reaches the
public as a derivative: decoded, re-encoded, and stripped of its metadata. A
document has no derivative. This system validates a document's format and
keeps it exactly as it arrived, and it never parses, converts, or strips one
(invariant 5, [ADR-0089](ADR-0089-no-malware-scanning-for-attachments.md)). Publishing a
document therefore publishes the reporter's own bytes: the names, medical
detail, author and tracked-change metadata, and embedded content they hold.

## Decision

1. **A validated document on a published report is public, unless a reviewer
   hid it.** It follows ADR-0117's rule for photos and video, with no
   pre-publication check. A safety officer or administrator may hide a
   document and show it again. Each change is audited.
2. **Only a document the Worker validated.** The Worker now records when it
   accepted a document (`report_files.validated_at`). A document it has not
   processed yet, or one that failed validation, is never public. Before this
   there was no record to tell those apart from a validated one.
3. **Media consent covers documents, under its new wording.** `consent_media`
   is reworded to ask about photos, videos, and documents, and the form now
   asks it whenever any file is attached, not only an image or video. A yes
   covers the documents only when it answered the wording the form showed at
   submission. The report records that as `reports.consent_documents`. It
   matches `consent_media` when the answered revision was the question's
   current revision, and is left unanswered otherwise. So a report filed before
   the rewording keeps its documents private, even though its yes still shows
   its photos and video. A stale draft that answered an older wording does
   too. Silence is not consent, and consent to one wording is not consent to
   another. An administrator may still reword the question. A reporter who
   answered the wording just replaced then keeps their documents private,
   which fails closed.
4. **The view still holds the rule.** `public_report_media` also lists a live,
   unhidden, validated document with no processing error on a report whose
   `consent_documents` is true
   ([ADR-0116](ADR-0116-a-read-rule-lives-in-a-view.md)).
5. **A forced download of the original, under a server-minted name.** The
   report page lists each public document's opaque id, the kind `document`,
   and a coarse format (`pdf`, `doc`, `docx`, `rtf`, `md`, `txt`, `odt`).
   `GET /api/v1/public/reports/{id}/media/{fileId}` returns a pre-signed GET
   to the private original that lives at most `BlobUrlLifetime.Maximum` (15
   minutes). It carries `Content-Disposition: attachment` under a name built
   from the file id and the format, never the reporter's filename, and the
   link response carries `X-Content-Type-Options: nosniff`. A document is
   never rendered inline, publicly or for a reviewer. `PublicMediaLink` signs
   an original only for a document, as `ReviewerMediaLink` already does.
6. **Retraction is ADR-0117's.** Hiding, unpublishing, or deleting makes the
   endpoint answer 404 at once. A link already issued keeps working until it
   expires, at most 15 minutes. A file already downloaded cannot be recalled.

## Rejected alternatives

- **A reviewer opts each document in.** It is the safer rule for bytes that
  nothing strips. The owner chose publish-by-default with after-the-fact
  moderation, the same as photos, video, and comments.
- **Documents stay private** (ADR-0117 as it stood).
- **A public icon a visitor cannot open.** It discloses that a file exists
  without giving it.
- **Downloading under the reporter's sanitized filename.** Filenames often
  carry a name, a date, or a place.
- **Letting the existing media-consent wording cover documents.** Its wording
  names photos and video only. A yes to it is not a yes to documents.
- **A third system question, `consent_documents`.** The owner preferred one
  question covering every attachment.
- **Stripping metadata, or converting to PDF, before publishing.** Either one
  is a document derivative, which invariant 5 rules out and this change does
  not revisit.

## Consequences

- **Accepted risk.** A document that identifies someone is public from
  approval until a reviewer hides it, plus up to 15 minutes. Unlike a photo,
  it reaches the public with every byte the reporter supplied, including
  metadata the reporter may not know is there. A visitor who downloaded it
  keeps it. The owner accepted this on 2026-09-24.
- The consent question's new wording tells the reporter this plainly, in both
  languages.
- There is now one public path to an original, through `PublicMediaLink`'s
  document check. An image or video original is still never public.
- `report_files.validated_at` records a fact the Worker already established.
  Only documents need it today.
