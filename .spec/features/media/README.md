---
title: Attachments
description: Supporting detail for the image, video, document, quarantine, and derivative scenarios.
type: spec
area: media
---

# Attachments

Supporting detail for [`media.feature`](media.feature) that doesn't fit
Gherkin.

## Allowlist evolution

The initial document allowlist covers the common formats in the feature file;
it is configuration-backed so another document type can be added
deliberately. The allowlist is constrained by deployed detection/codec
support. Adding an image or video format requires signature detection, safe
derivative processing, tests, and a localized UI update. Adding a document
type requires reliable format detection, download-safety tests, and the same
UI update. Accepting a MIME label or filename extension alone is
insufficient.

Markdown and plain text share one bounded UTF-8 text-validation path because
their bytes cannot be distinguished reliably; their declared type changes only
the private download label, never security handling or rendering.

## Storage compartments

- Quarantine contains an upload the browser sent, not yet validated, under
  `quarantine/<upload id>`,
  until a submission claims it or the lifecycle rule expires it fifteen days
  after it was written, the same window as the saved report that names it. A
  reporter removing the file, or abandoning the report, erases every version
  of it
  ([ADR-0096](../../decisions/ADR-0096-an-attachment-uploads-on-attach-and-is-claimed-at-submission.md),
  [ADR-0100](../../decisions/ADR-0100-an-attachment-is-kept-as-long-as-the-saved-report.md),
  [ADR-0126](../../decisions/ADR-0126-an-attachment-uploads-straight-to-quarantine-by-pre-signed-put.md)).
- Private original is the retained canonical input after validation.
- Derivative contains the safe reviewer copy.

All compartments are private. Storage blocks public access, uses TLS in
transit and provider-managed encryption at rest, and grants least-privilege
access to the API/Worker roles. There are no application-encrypted blobs.
Referenced report objects follow report retention and are not physically
purged by the application after soft deletion.

## Processing records

Processing records status, safe content type, byte size, derivative key where
applicable, and timestamps without recording supplied names or metadata. Tool
output and error messages are sanitized before logging.

## Where ffmpeg comes from

The Worker image installs Ubuntu's ffmpeg, in development and deployed, and
runs it only as a child process
([ADR-0118](../../decisions/ADR-0118-the-worker-image-installs-ubuntus-ffmpeg.md)).
A Worker run without it, such as a local `dotnet run` on a machine with no
ffmpeg, still accepts video and keeps it with no derivative
([ADR-0094](../../decisions/ADR-0094-video-is-remuxed-not-transcoded-and-never-refused.md)).

## Where processing happens

A submission copies each claimed upload, inside storage, to the report's
original compartment and commits; it never decodes a file. The Worker's
attachment handler then sniffs the original and writes the derivative, one
outbox message per file
([ADR-0098](../../decisions/ADR-0098-submission-copies-the-original-and-the-worker-makes-the-derivative.md)).
Until it has, a reviewer sees the file as awaiting processing.

Processing streams: the original is copied in bounded chunks into a temporary
file, hashed as it goes, and every sniffer and derivative step reads that file
or writes another, deleted when processing ends. Only decoding an image holds
its pixels in memory, which re-encoding it requires (#362).

## Every video derivative is an MP4

Whatever container a video arrives in, an iPhone's QuickTime included, the
remux writes an MP4 and the verification requires one. The derivative is
stored and served as `video/mp4`, and a reviewer downloads it as `.mp4`
(REQ-MED-007, REQ-MED-043, REQ-MED-044,
[ADR-0122](../../decisions/ADR-0122-a-video-derivative-is-always-an-mp4.md)).
A stream MP4 cannot hold is a remux that fails, never a reason to transcode.

## A video with no derivative

A video that cannot be remuxed into a verified derivative is retained rather
than refused (REQ-MED-015,
[ADR-0094](../../decisions/ADR-0094-video-is-remuxed-not-transcoded-and-never-refused.md)).
The same now holds for a still-processing or failed **image** (REQ-MED-013,
REQ-MED-053, widening ADR-0094 — #427): either way the reviewer strip marks
the tile Processing or Failed, and `GET
/api/admin/reports/{reportId}/attachments/{attachmentId}/original` gives an
authorized reviewer a short-lived, forced, audited download of the raw
original under its own `AuditAction.DownloadedOriginalMedia`, never rendered
inline and never opened in the lightbox. It refuses once a derivative exists
(REQ-MED-054, use `/view` instead) and for a document (REQ-MED-055, use
`/download`). Unlike a document it is never published, not even on a
published report that shows its other media. That reviewer path is built with
the reviewer endpoints (#311, #427); this page records that an unstripped
video and a still-processing or failed image share it rather than each
getting a rule of its own.

The anonymity contract is unaffected. No attachment of any kind reaches the
model — the summary never sees one.

## Public media

A published report's page shows its image and video derivatives (REQ-MED-025
to REQ-MED-036,
[ADR-0117](../../decisions/ADR-0117-a-published-report-shows-the-reporters-photos-and-video.md)).
One view, `public_report_media`, holds the whole rule: the report is in
`public_reports`, its reporter answered yes to media consent, and the file is
a live image or video with a verified derivative, no processing error, and no
reviewer hide. The report page lists each such file's opaque id and kind,
nothing more.

The bytes are never on the CDN. The page asks
`GET /api/v1/public/reports/{id}/media/{fileId}` for each file and gets back a
pre-signed GET to the derivative that lives at most fifteen minutes and is
served inline. When an image or video errors, the page asks again — a video
resumes where it was, paused if it was paused and playing if it was playing —
and removes the file if the answer is 404. So a hide or
an unpublish reaches every open page within fifteen minutes.

Moderation happens after publication. A safety officer or administrator hides
a file from the public report page or the admin report page, and shows it
again from the admin report page; both are audited. The file itself is never
deleted by a hide.

Media consent (`consent_media`) is the form's second system question. The form
asks it only when publication consent is yes and a file is attached. A report
filed before it existed has no answer and shows no media.

## Public documents

A published report also offers its validated documents (REQ-MED-037 to
REQ-MED-042,
[ADR-0119](../../decisions/ADR-0119-a-published-report-offers-its-documents-for-download.md)).
`public_report_media` lists a live, unhidden document with no processing error
once the Worker has recorded it validated (`validated_at`), on a report whose
`consent_documents` is yes. The report page lists its opaque id, the kind
`document`, and a coarse format, nothing more.

The link endpoint answers a document with a pre-signed GET to the unchanged
original that lives at most fifteen minutes and forces a download under a name
made from the file id and the format. The reporter's own filename never
reaches the public.

`consent_documents` is `consent_media`'s answer, recorded only when the
reporter answered the wording the form showed at submission. Media consent was
reworded to name documents. A yes given before that shows the report's photos
and video and keeps its documents private. A yes to a superseded wording that a
stale draft still held is refused at submission, like any answer to a superseded
revision
([ADR-0185](../../decisions/ADR-0185-a-submission-answers-only-current-revisions-and-the-browser-drops-the-rest.md)).

## The thumbnail strip, lightbox, and viewer-scoped counts (#427)

A report's own page and the admin report page each show its attachments as
one horizontal, scrollable strip of thumbnails, in place of stacked embeds or
list rows. An image thumbnail is its existing derivative scaled with CSS; a
video gets a generic play tile; a document gets a type icon (PDF, DOC, DOCX,
RTF, MD, TXT, ODT) and is never opened in the lightbox — activating it
downloads instead, through the same public or staff path `/reports/:id` and
`/admin/reports/:id` already use. No new Worker derivative.

Activating an image or video thumbnail opens a lightbox that steps through
every image and video on the report, in attachment order: caret buttons and
the Left/Right arrow keys move, Escape closes, focus is trapped while it is
open and returns to the thumbnail on close, and navigation wraps at both
ends. A video plays with native controls and its audio, and stops when the
lightbox moves away from it or closes. Labels stay generic ("Photo 1 of 3",
"Video 1 of 2") — no reviewer-authored text.

**Who sees what, on both pages:**

- The public (anonymous visitors and `User`) sees only what `public_report_media`
  already lists.
- A signed-in `SafetyOfficer`/`Administrator` sees every attachment, each
  marked with its state (`ready`, `processing`, `failed`) and its public
  visibility (`public`, `hidden`, `no_consent`, `when_published`, or
  `private`) — the same vocabulary the admin report page already used for its
  list rows — with Hide and Show. `/admin/reports/:id` works this way even for
  an unpublished report.
- The public report endpoint (`GET /api/v1/public/reports/{id}`) stays
  anonymous and reads a staff bearer token only when one is sent (JwtBearer is
  the default scheme, so a sent token is still authenticated); a hidden or
  otherwise non-public item's state and visibility appear only in that staff
  response.

**Counts, on the public feed and the admin report list:** a report with at
least one attachment the viewer may see shows an attachment icon and count,
omitted at zero, with an accessible localized label ("3 attachments"/"3
pièces jointes"). The count is viewer-scoped exactly like the strip: the
public count from `public_report_media` for the public, every non-deleted
`report_files` row for staff — computed in the `public_reports` and
`admin_report_queue` views, never in C#, and never counting a staff-only
private attachment (see "Private attachments" below, ADR-0135). The public
feed item carries only the count — no ids, kinds, names, or links.

**Link and audit rules, unchanged in spirit, adjusted in shape:**

- `/view` mints an **inline** link to an image or video's derivative now
  (REQ-MED-010), not a forced download, so the lightbox can embed it; still
  audited, still at most fifteen minutes. `/download` is unchanged
  (documents only).
- A public item on a staff page loads through the anonymous public link,
  unaudited, the same as it does for a visitor. A non-public item goes
  through the audited staff mint (`ViewedAttachment`, or
  `DownloadedOriginalMedia` for a raw original), one row per mint. The
  lightbox reuses a thumbnail's link while it is valid; a refresh after an
  error writes another row and resumes playback position.
  A 404 removes the item from both the strip and the lightbox.

See [ADR-0117](../../decisions/ADR-0117-a-published-report-shows-the-reporters-photos-and-video.md#amendment-2026-09-28)
for the full record.

## Private attachments (#507)

A safety officer or administrator may add files to a report that are for
staff only: a coroner's report, a police report, an investigation archive
([ADR-0135](../../decisions/ADR-0135-staff-add-private-attachments-to-a-report.md)).
They are not the reporter's attachments, and none of the rules above about
formats, sniffing, derivatives, consent, or publication applies to them.
**A private attachment is never anonymized**: no metadata stripping, no
derivative, no redaction, and no marking pass. It is stored and downloaded
byte for byte as the staff member uploaded it (REQ-MED-052).

- **Upload.** The browser asks
  `POST /api/admin/reports/{reportId}/private-attachments/uploads` with the
  declared type and exact size, gets a pre-signed PUT to
  `quarantine/<upload id>`, and sends the file straight to storage, exactly as
  a reporter's upload does. A private upload is judged on size alone: above
  zero and at most `HpacSafety:Media:PrivateAttachments:MaxByteSize`
  (1 GB by default), which is separate from the reporter caps. Any type is
  accepted; an empty or malformed type is signed as
  `application/octet-stream` (REQ-MED-046, REQ-MED-047).
- **Claim.** `POST /api/admin/reports/{reportId}/private-attachments` names the
  upload, the file name, and an optional description. The API copies the
  upload, inside storage and unchanged, to
  `<report id>/private/<attachment id>`, records it, and erases the quarantine
  copy. An upload nobody claims expires with the same quarantine lifecycle rule
  as any other (REQ-MED-048, REQ-MED-050).
- **Download.** A pre-signed GET of at most fifteen minutes, forcing a
  download under the sanitized file name with its own extension, and one
  `DownloadedPrivateAttachment` audit entry per link. Only
  `PrivateAttachmentLink` signs a URL for the private compartment, and it
  signs nothing else (REQ-MED-049, REQ-MED-051).

Who may do this, removal, and the private-note reference are in
[`.spec/features/moderation-authentication-and-publication`](../moderation-authentication-and-publication/README.md).

## Out of scope

What not to build here. The global list in
[system overview](../../system-overview.md) still holds; this narrows it
to this area ([ADR-0083](../../decisions/ADR-0083-specification-driven-development.md)).

- Parsing, extracting, indexing, or searching the contents of a document.
- Inline rendering or preview of a document, including a thumbnail or a first
  page, publicly or for a reviewer.
- Any public delivery of an image or video original, of a document other than
  as a forced download of its validated original, or of any file before its
  report is published.
- Stripping a document's metadata, converting it, or redacting it before
  publication. A public document is exactly what the reporter uploaded.
- Showing the reporter's filename to the public.
- A CDN-served, public-bucket, or long-lived copy of any attachment.
- Reviewer-authored alt text, captions, or transcripts. A public file carries
  a generic localized label.
- Blurring, cropping, muting, or otherwise editing media before publication,
  and a pre-publication media review step.
- Choosing, per file, which attachments to share. Media consent covers all of
  a report's images, videos, and documents.
- Worker-made thumbnails, video poster frames, and document previews or
  first-page images (already out of scope above) — an image thumbnail is its
  existing derivative scaled with CSS, a video gets a generic play tile, and a
  document gets a type icon (#427).
- Zoom, pan, and share buttons in the lightbox.
- Attachment previews in the report and admin lists beyond the icon and
  count — no ids, kinds, names, or links (#427).
- Anonymizing or transforming a document. A validated original is retained
  exactly as it arrived.
- Client-side processing, resizing, or stripping before upload. Validation and
  metadata removal happen server-side, where they can be trusted.
- A resumable, chunked, or multipart upload protocol. A file reaches quarantine
  only through the one pre-signed `PUT` the API minted for its upload ID,
  signed for its declared type and exact size, and is validated when a
  submission claims it
  ([ADR-0126](../../decisions/ADR-0126-an-attachment-uploads-straight-to-quarantine-by-pre-signed-put.md)).
- A pre-signed URL that writes anywhere but `quarantine/<upload id>`, or that
  names a report or a member. A private attachment's upload follows the same
  rule (ADR-0135).
- An upload table, or any record linking an upload to the member who made it.
  A private attachment records the staff member who added it only once it is
  claimed onto a report (ADR-0135).
- For private attachments: anonymizing them in any way — EXIF or other
  metadata stripping, a derivative, redaction, or the marking pass — previews,
  thumbnails, unpacking a zip,
  sniffing or an allowlist, a malware scan (ADR-0089), a multipart or
  resumable upload, editing or replacing a file (remove it and add it again),
  restoring a removed one, a per-report count cap, and any sharing or
  publication beyond the two reviewer roles.
- A filesystem storage adapter. Development runs an S3-compatible server
  (RustFS, ADR-0110) behind the same `S3BlobStore` production uses.
