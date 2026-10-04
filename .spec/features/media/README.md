---
title: Attachments
description: Supporting detail for the image, video, document, quarantine, and derivative scenarios, and for the staff-only private attachments on a report.
type: spec
area: media
prefix: REQ-MED
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
purged by the application after deletion.

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
It is not marked as a processing failure either, because nothing failed that
should cost the reporter their footage.
The same now holds for a still-processing or failed **image** (REQ-MED-013,
REQ-MED-053, widening ADR-0094 — #427): either way the reviewer strip marks
the tile Processing or Failed, and `GET
/api/admin/reports/{reportId}/attachments/{attachmentId}/original` gives an
reviewer a short-lived, forced, audited download of the raw
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
to REQ-MED-036, REQ-MED-063 to REQ-MED-068, REQ-MED-084,
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

Moderation happens after publication. A reviewer hides
a file from the public report page or the admin report page, and shows it
again from the admin report page; both are audited. The file itself is never
deleted by a hide.

Media consent (`consent_media`) is the form's second system question. The form
asks it only when publication consent is yes and a file is attached. A report
filed before it existed has no answer and shows no media.

## Public documents

A published report also offers its validated documents (REQ-MED-037 to
REQ-MED-042, REQ-MED-069, REQ-MED-084,
[ADR-0119](../../decisions/ADR-0119-a-published-report-offers-its-documents-for-download.md)).
`public_report_media` lists a document that is neither deleted nor hidden, with no processing error
once the Worker has recorded it validated (`validated_at`), on a report whose
`consent_documents` is yes. The report page lists its opaque id, the kind
`document`, and a coarse format, nothing more.

The link endpoint answers a document with a pre-signed GET to the unchanged
original that lives at most fifteen minutes and forces a download under a name
made from the file id and the format. The reporter's own filename never
reaches the public.

`consent_documents` is `consent_media`'s answer, recorded only when the
reporter answered the wording the form showed at submission. Media consent was
reworded to name documents. A yes given before that shows the report's images
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

**A reporter's footage carries no captions (`REQ-MED-062`).** This is a
deliberate position, not a gap. The lightbox plays the verified derivative as
it is, with the browser's own controls, and offers no caption or subtitle
track, no caption control, and no text implying that captions exist.

- Captions would have to be made from what the footage says. Speech-to-text is
  a model call, and the only one this system makes is the summary's, for a
  consenting report and over text, never over media
  ([AGENTS.md](../../../AGENTS.md) invariants 3 and 5). A person writing them
  would be handling the reporter's footage in a way nothing here provides for.
- The published report's anonymized text summary is the accessible account of
  what happened. The video is the reporter's own, offered with their media
  consent, and adds to it.
- The lightbox's own controls (Previous, Next, Close) and labels are text and
  remain operable by keyboard and screen reader (`REQ-MED-056`); focus stays
  inside the open lightbox and returns to its thumbnail when it closes
  (`REQ-MED-067`, `REQ-MED-068`).
- The `<video>` in `AttachmentLightboxMedia.view.tsx` therefore disables
  `jsx-a11y/media-has-caption` on that line, with a reason naming this claim.
  If the position changes, this paragraph, the scenario, and that disable
  change together.

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
  A 404 drops the item (`REQ-MED-058`).

See [ADR-0117](../../decisions/ADR-0117-a-published-report-shows-the-reporters-photos-and-video.md#amendment-2026-09-28)
for the full record.

## Private attachments (#507)

A reviewer may add files to a report that are for
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

A private note may refer to a private attachment; the note itself is in
[`.spec/features/review-and-publication`](../review-and-publication/README.md).

### On the report page

The report view has a **Private attachments** section, newest first. Each
lists its file name, size, optional description, who added it (**You**, or
the adder's opaque token subject), and when.

Adding files uses the same dashed drop zone as the reporter form's attachment
question (#658): dropped or chosen, several at once, each begins uploading the
moment it is staged, with its own progress and its own Cancel or Remove
control and description box. Removing a staged, already-uploaded row asks no
API to erase it; its bytes simply expire by the 15-day quarantine lifecycle
rule that already governs an unclaimed upload
([ADR-0126](../../decisions/ADR-0126-an-attachment-uploads-straight-to-quarantine-by-pre-signed-put.md)).
Cancelling a row still uploading aborts it and erases the mint, as before.
**Add N attachments** stays disabled until every staged row has settled —
finished or failed — where N counts only the finished ones; it then claims
each in turn with its own description, and while it runs each staged row's
Remove and description are locked, so what is added is exactly what was
shown (#674). Leaving the report page while an upload is staged but not yet
added warns, on both a browser close/reload and an in-app navigation; a list
holding only refused files has nothing to lose and does not warn (#658, #674;
a general leave-warning for every form is issue #659, not built here). Any
reviewer may download any added attachment, or remove one after confirming
(REQ-MOD-115, REQ-MOD-117, REQ-MOD-173..177, REQ-MOD-180..181,
REQ-MED-071..082). A note may refer to one on its own report only, and an edit
that drops the reference keeps it in the note's history (REQ-MOD-114,
REQ-MOD-116, REQ-MED-070).

- Any report that is not deleted, in any status, including a report without
  publication consent (REQ-MOD-108). Any file type, up to the configured cap;
  the file travels and is stored as the upload, claim, and download above
  describe.
- The file name is required and is sanitized; the description is optional
  plain text of at most 500 characters (REQ-MOD-110).
- Removal deletes the row, records who removed it, and writes one
  `RemovedPrivateAttachment` audit entry; the bytes stay in storage. Deleting
  the report does the same to its private attachments (REQ-MOD-109,
  REQ-MOD-111).
- The endpoints live under
  `/api/admin/reports/{reportId}/private-attachments` and answer only a Safety
  Officer or an Administrator (REQ-MOD-107). Nothing else reads the table: not
  the report detail DTO or its attachment list, not a count, not a database
  view, not the public feed or its media, not the Worker or the model
  (REQ-MOD-112, REQ-MOD-113). An attachment count on a report list (#427)
  counts the reporter's attachments only.

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
- Reviewer-authored alt text, captions, or transcripts, and captions or
  transcripts made from a video by a model or a person. A public file carries
  a generic localized label, and a reporter's video carries no captions
  (`REQ-MED-062`).
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
  restoring a removed one, a per-report count cap, any count of them outside
  their own list, and any sharing or publication beyond the two reviewer
  roles (ADR-0135).
- A filesystem storage adapter. Development runs an S3-compatible server
  (RustFS, ADR-0110) behind the same `S3BlobStore` production uses.
