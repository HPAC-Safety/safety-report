---
name: handle-hpac-media
description: Handle HPAC Safety attachment uploads, private storage, safe image/video derivatives, and private documents. Use for upload, storage, validation, metadata, or reviewer-access changes.
---

# Handle HPAC Safety attachments

Each attachment uploads on its own, the moment it is attached, through
`POST /api/v1/uploads` ([ADR-0096](../../docs/decisions/ADR-0096-an-attachment-uploads-on-attach-and-is-claimed-at-submission.md)). Read the raw body into a temporary file, stopping
one byte past 50 MB, sniff and validate it, and only then write it to
`quarantine/<upload id>` under a server-minted 128-bit upload ID. An upload has
no database row and never names the member who made it. `DELETE` erases every
version of it. The final `POST /api/v1/reports` names uploads per file-upload
answer, enforces the configurable total count (default 5), refuses missing
uploads by ID before writing anything, and claims the rest by a server-side
copy into the report's original compartment, named by the report file's own
id. The Worker's `ProcessAttachment` handler — one outbox message per file,
idempotent, skipping deleted reports — sniffs the original and writes the
derivative
([ADR-0098](../../docs/decisions/ADR-0098-submission-copies-the-original-and-the-worker-makes-the-derivative.md)). The final submission also
carries each file's name: sanitize it (last path segment, no control, quote, or
reserved characters, at most 255), store it on the report file, and use it only
as a reviewer's forced-download name with the served type's extension
([ADR-0097](../../docs/decisions/ADR-0097-a-reviewer-downloads-an-attachment-under-its-sanitized-original-name.md)).

`S3BlobStore` is the only storage adapter: S3 through the task role in AWS,
MinIO in docker-compose for development. Do not add a filesystem adapter or a
pre-signed PUT for reporters.

Accepted formats:

- images: JPEG, PNG, WebP, HEIC;
- video: MP4, QuickTime;
- documents: PDF, DOC, DOCX, RTF, MD, TXT, ODT.

Sniff format and require declared/actual agreement. There is no malware
scanner (ADR-0089) — content-type sniffing and the format allowlist are the
only gate. Decode/re-encode images and safely remux/transcode videos to
remove metadata; reviewers may see only verified derivatives. An image that
cannot be stripped fails closed. A video that cannot be remuxed is kept as a
private original with no derivative, reachable only as a reviewer download
([ADR-0094](../../docs/decisions/ADR-0094-video-is-remuxed-not-transcoded-and-never-refused.md)).

Documents are different: preserve the validated original, do not transform or
anonymize its contents, and never parse/extract it for AI. Allow only an
authorized, short-lived, forced download with active-content-safe headers.
Documents are never inline-rendered or public.

Database failure leaves only unclaimed quarantine uploads for lifecycle expiry.
Report-linked originals/derivatives remain private after soft deletion. Never
log client filenames, storage keys/URLs, or file contents, and never put a
filename in a key, an error, model input, or a public DTO.
