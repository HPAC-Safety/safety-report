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

- Quarantine contains an accepted upload, under `quarantine/<upload id>`,
  until a submission claims it or the lifecycle rule expires it. A reporter
  removing the file erases every version of it
  ([ADR-0096](../../docs/decisions/ADR-0096-an-attachment-uploads-on-attach-and-is-claimed-at-submission.md)).
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

## Current implementation divergence

The deployed Worker image has no ffmpeg yet (#30), so a video is retained with
no derivative there
([ADR-0094](../../docs/decisions/ADR-0094-video-is-remuxed-not-transcoded-and-never-refused.md)). See
[implementation status](../../docs/implementation-status.md).

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

## A video with no derivative

A video that cannot be remuxed into a verified derivative is retained rather
than refused (REQ-MED-015,
[ADR-0094](../../docs/decisions/ADR-0094-video-is-remuxed-not-transcoded-and-never-refused.md)).
It then behaves exactly as a document does: a private original, reachable only
by an authorized reviewer as a short-lived forced download, never rendered
inline and never published. That reviewer path is REQ-MED-011's rule and is
built with the reviewer endpoints (#311); this page records that an unstripped
video joins it rather than getting a rule of its own.

The anonymity contract is unaffected. No attachment of any kind reaches the
model or the public feed — the summary never sees one.

## Out of scope

What not to build here. The global list in
[system overview](../../docs/system-overview.md) still holds; this narrows it
to this area ([ADR-0083](../../docs/decisions/ADR-0083-specification-driven-development.md)).

- Parsing, extracting, indexing, or searching the contents of a document.
- Inline rendering or preview of a document, including a thumbnail or a first
  page.
- Any public delivery of an attachment, before or after publication.
- Anonymizing or transforming a document. A validated original is retained
  exactly as it arrived.
- Client-side processing, resizing, or stripping before upload. Validation and
  metadata removal happen server-side, where they can be trusted.
- A resumable or chunked upload protocol, or a pre-signed PUT for a reporter.
  A file reaches quarantine only through `POST /api/v1/uploads`, after the API
  has validated it
  ([ADR-0096](../../docs/decisions/ADR-0096-an-attachment-uploads-on-attach-and-is-claimed-at-submission.md)).
- An upload table, or any record linking an upload to the member who made it.
- A filesystem storage adapter. Development runs MinIO behind the same
  `S3BlobStore` production uses.
