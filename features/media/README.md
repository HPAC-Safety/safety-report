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
  ([ADR-0096](../../docs/decisions/ADR-0096-an-attachment-uploads-on-attach-and-is-claimed-at-submission.md),
  [ADR-0100](../../docs/decisions/ADR-0100-an-attachment-is-kept-as-long-as-the-saved-report.md),
  [ADR-0126](../../docs/decisions/ADR-0126-an-attachment-uploads-straight-to-quarantine-by-pre-signed-put.md)).
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
([ADR-0118](../../docs/decisions/ADR-0118-the-worker-image-installs-ubuntus-ffmpeg.md)).
A Worker run without it, such as a local `dotnet run` on a machine with no
ffmpeg, still accepts video and keeps it with no derivative
([ADR-0094](../../docs/decisions/ADR-0094-video-is-remuxed-not-transcoded-and-never-refused.md)).

## Where processing happens

A submission copies each claimed upload, inside storage, to the report's
original compartment and commits; it never decodes a file. The Worker's
attachment handler then sniffs the original and writes the derivative, one
outbox message per file
([ADR-0098](../../docs/decisions/ADR-0098-submission-copies-the-original-and-the-worker-makes-the-derivative.md)).
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
[ADR-0122](../../docs/decisions/ADR-0122-a-video-derivative-is-always-an-mp4.md)).
A stream MP4 cannot hold is a remux that fails, never a reason to transcode.

## A video with no derivative

A video that cannot be remuxed into a verified derivative is retained rather
than refused (REQ-MED-015,
[ADR-0094](../../docs/decisions/ADR-0094-video-is-remuxed-not-transcoded-and-never-refused.md)).
It then behaves as a private document does: an original reachable only by an
authorized reviewer as a short-lived forced download, never rendered inline.
Unlike a document it is never published, not even on a published report that
shows its other media. That reviewer path is REQ-MED-011's rule and is
built with the reviewer endpoints (#311); this page records that an unstripped
video joins it rather than getting a rule of its own.

The anonymity contract is unaffected. No attachment of any kind reaches the
model — the summary never sees one.

## Public media

A published report's page shows its image and video derivatives (REQ-MED-025
to REQ-MED-036,
[ADR-0117](../../docs/decisions/ADR-0117-a-published-report-shows-the-reporters-photos-and-video.md)).
One view, `public_report_media`, holds the whole rule: the report is in
`public_reports`, its reporter answered yes to media consent, and the file is
a live image or video with a verified derivative, no processing error, and no
reviewer hide. The report page lists each such file's opaque id and kind,
nothing more.

The bytes are never on the CDN. The page asks
`GET /api/v1/public/reports/{id}/media/{fileId}` for each file and gets back a
pre-signed GET to the derivative that lives at most fifteen minutes and is
served inline. When an image or video errors, the page asks again — a video
resumes where it was — and removes the file if the answer is 404. So a hide or
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
[ADR-0119](../../docs/decisions/ADR-0119-a-published-report-offers-its-documents-for-download.md)).
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
reworded to name documents. A yes given before that, or to a superseded wording
a stale draft still held, shows the report's photos and video and keeps its
documents private.

## Out of scope

What not to build here. The global list in
[system overview](../../docs/system-overview.md) still holds; this narrows it
to this area ([ADR-0083](../../docs/decisions/ADR-0083-specification-driven-development.md)).

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
- Media in the public feed list, thumbnails, or a gallery or lightbox beyond
  the native image and video controls.
- Anonymizing or transforming a document. A validated original is retained
  exactly as it arrived.
- Client-side processing, resizing, or stripping before upload. Validation and
  metadata removal happen server-side, where they can be trusted.
- A resumable, chunked, or multipart upload protocol. A file reaches quarantine
  only through the one pre-signed `PUT` the API minted for its upload ID,
  signed for its declared type and exact size, and is validated when a
  submission claims it
  ([ADR-0126](../../docs/decisions/ADR-0126-an-attachment-uploads-straight-to-quarantine-by-pre-signed-put.md)).
- A pre-signed URL that writes anywhere but `quarantine/<upload id>`, or that
  names a report or a member.
- An upload table, or any record linking an upload to the member who made it.
- A filesystem storage adapter. Development runs an S3-compatible server
  (RustFS, ADR-0110) behind the same `S3BlobStore` production uses.
