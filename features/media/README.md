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

- Quarantine contains the immutable private original first received by the
  API.
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

Main already validates the six image/video types, enforces a 50 MB policy,
sniffs content, mints keys, stores privately, and re-encodes images. It
currently creates pre-signed upload slots before submission and intentionally
retains videos without a viewable derivative. The target replaces the
upload-slot flow with API streaming and requires a safe derivative for videos
as well as images. It does not yet accept or privately expose the document
types specified above. See
[implementation status](../implementation-status/implementation-status.md).
