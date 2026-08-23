# Attachments

## Product behavior

A reporter may attach multiple images, videos, or documents to the finalized
report. The maximum count is configurable and defaults to five across all
attachment kinds. Each file is limited to
50 MB. Supported attachment types are:

| Kind | MIME type | Typical extension |
|---|---|---|
| Image | `image/jpeg` | `.jpg` |
| Image | `image/png` | `.png` |
| Image | `image/webp` | `.webp` |
| Image | `image/heic` | `.heic` |
| Video | `video/mp4` | `.mp4` |
| Video | `video/quicktime` | `.mov` |
| Document | `application/pdf` | `.pdf` |
| Document | `application/msword` | `.doc` |
| Document | `application/vnd.openxmlformats-officedocument.wordprocessingml.document` | `.docx` |
| Document | `application/rtf` or `text/rtf` | `.rtf` |
| Document | `text/markdown` | `.md` |
| Document | `text/plain` | `.txt` |
| Document | `application/vnd.oasis.opendocument.text` | `.odt` |

The initial document allowlist covers the common formats above; it is
configuration-backed so another document type can be added deliberately. The
allowlist is constrained by deployed detection/codec support. Adding an image
or video format requires signature detection, safe derivative processing,
tests, and a localized UI update. Adding a document type requires reliable
format detection, download-safety tests, and the same UI update. Accepting a
MIME label or filename extension alone is insufficient.
Markdown and plain text share one bounded UTF-8 text-validation path because
their bytes cannot be distinguished reliably; their declared type changes only
the private download label, never security handling or rendering.

## Ingest

Attachments arrive only as parts of the finalized multipart report submission.
The API applies the request and count bounds, mints an opaque filename, streams each
file through a bounded counter and signature sniffer, and writes accepted bytes
to a private quarantine key. Declared content type must agree with the detected
allowlisted type. Extensions and client filenames are never trusted.

The client filename is discarded at the HTTP boundary: it is not persisted,
logged, placed in an exception, used in a key, sent to the model, or returned to
an admin. Object keys encode only an opaque report/file identity and a managed
compartment.

## Storage compartments

- Quarantine contains the immutable private original first received by the API.
- Private original is the retained canonical input after validation.
- Derivative contains the safe reviewer copy.

All compartments are private. Storage blocks public access, uses TLS in transit
and provider-managed encryption at rest, and grants least-privilege access to
the API/Worker roles. There are no application-encrypted blobs.

Unreferenced quarantine blobs expire through a short storage lifecycle rule so
a failed database transaction does not leak indefinite objects. Referenced
report objects follow report retention and are not physically purged by the
application after soft deletion.

## Worker processing

The same Worker deployment handles attachments and summarization, but each file
has an independent outbox item. A slow or corrupt file therefore cannot roll back a
valid report or force additional AI calls.

Every image is decoded and re-encoded into a supported safe representation,
with EXIF, GPS, profiles, comments, thumbnails, and other metadata removed.
Every video is decoded/remuxed or transcoded through a controlled toolchain to
remove container metadata, location, device, creation, and filename fields. A
byte-for-byte copy of a video is not a safe derivative.

Documents are not anonymized or content-transformed. The Worker scans them for
known malware and validates their actual
format, including the internal package shape for DOCX/ODT and bounded text
decoding for Markdown/plain text, marks them available for private download,
and never extracts their text. Documents are never sent to the LLM and never
published. They remain the reporter-supplied original, so the review UI clearly
labels them as unredacted private evidence. A malware or validation failure
makes the document inaccessible; scanning does not make an untrusted document
safe, so the UI also warns reviewers before download.

Processing records status, safe content type, byte size, derivative key where
applicable, and timestamps without recording supplied names or metadata. Tool output and error
messages are sanitized before logging.

## Fail closed

For images and videos, a reviewer receives a short-lived read URL only for a
successfully processed derivative. For a validated document, an authorized
reviewer may request a short-lived URL to the private original. The response
forces download with a server-minted display name and the HTTP header
`X-Content-Type-Options: nosniff`; the admin site does not embed or inline-render active document
content. There is no API blob proxy or public URL.

If signature or malware validation, decoding, metadata removal,
re-encoding/remuxing, write, or verification fails, the file is marked failed
and is inaccessible to the reviewer. “Accepted upload” never implies
“viewable.”

Attachments are never public, even after the report is published. Public DTOs
do not contain file counts, types, keys, or links.

## Current implementation divergence

Main already validates the six image/video types, enforces a 50 MB policy, sniffs
content, mints keys, stores privately, and re-encodes images. It currently
creates pre-signed upload slots before submission and intentionally retains
videos without a viewable derivative. The target replaces the upload-slot flow
with API streaming and requires a safe derivative for videos as well as images.
It does not yet accept or privately expose the document types specified above.
