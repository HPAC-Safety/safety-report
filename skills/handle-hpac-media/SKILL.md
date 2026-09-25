---
name: handle-hpac-media
description: Handle HPAC Safety attachment uploads, private storage, safe image/video derivatives, and private documents. Use for upload, storage, validation, metadata, or reviewer-access changes.
---

# Handle HPAC Safety attachments

## Upload

Each file uploads alone, the moment it is attached, through
`POST /api/v1/uploads`
([ADR-0096](../../docs/decisions/ADR-0096-an-attachment-uploads-on-attach-and-is-claimed-at-submission.md)):

1. Read the raw body into a temporary file, stopping one byte past 50 MB.
2. Sniff and validate it.
3. Only then write it to `quarantine/<upload id>` under a server-minted 128-bit
   upload ID.

- An upload has no database row and never names the member who made it.
- `DELETE` erases every version of it.

## Submission claims uploads

The final `POST /api/v1/reports`:

- names uploads per file-upload answer;
- enforces the configurable total count (default 5);
- refuses missing uploads by ID before writing anything;
- claims the rest by server-side copy into the report's original compartment,
  named by the report file's own id;
- carries each file's name. Sanitize it — last path segment; no control,
  quote, or reserved characters; at most 255 — and store it on the report file.
  Use it only as a reviewer's forced-download name, with the served type's
  extension
  ([ADR-0097](../../docs/decisions/ADR-0097-a-reviewer-downloads-an-attachment-under-its-sanitized-original-name.md)).

The Worker's `ProcessAttachment` handler — one outbox message per file,
idempotent, skipping deleted reports — sniffs the original and writes the
derivative
([ADR-0098](../../docs/decisions/ADR-0098-submission-copies-the-original-and-the-worker-makes-the-derivative.md)).

## Storage

- `S3BlobStore` is the only adapter: S3 through the task role in AWS, RustFS in
  docker-compose for development (ADR-0110).
- No filesystem adapter; no pre-signed PUT for reporters.
- Never copy an attachment to the CDN or a public prefix.

## Formats and validation

- Accepted:
  - images: JPEG, PNG, WebP, HEIC;
  - video: MP4, QuickTime;
  - documents: PDF, DOC, DOCX, RTF, MD, TXT, ODT.
- Sniff the format and require declared and actual to agree.
- No malware scanner (ADR-0089): sniffing and the format allowlist are the only
  gate.

## Images and video

- Decode and re-encode images, and remux videos into MP4 (a stream copy, never
  a transcode; ADR-0094, ADR-0122), to strip metadata. Reviewers see only verified derivatives.
- An image that cannot be stripped fails closed.
- A video that cannot be remuxed is kept as a private original with no
  derivative, reachable only as a reviewer download
  ([ADR-0094](../../docs/decisions/ADR-0094-video-is-remuxed-not-transcoded-and-never-refused.md)).
- A published report shows verified derivatives — never an original — when the
  reporter also consented to media. Served through a pre-signed GET of at most
  15 minutes, minted by `PublicMediaLink` and gated by the
  `public_report_media` view. A reviewer may hide any file (ADR-0117).

## Documents

- Preserve the validated original. Never transform or anonymize it, or
  parse/extract it for AI.
- Only a short-lived, forced download with active-content-safe headers: to a
  reviewer, or to anyone once `public_report_media` lists it, under a
  server-minted name (ADR-0119).
- Never inline-rendered.

## Failure, deletion, logging

- A database failure leaves only unclaimed quarantine uploads, for lifecycle
  expiry.
- Report-linked originals and derivatives stay private after soft deletion.
- Never log client filenames, storage keys or URLs, or file contents.
- Never put a filename in a key, an error, model input, or a public DTO.
