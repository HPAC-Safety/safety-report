---
title: A reviewer downloads an attachment under its sanitized original name
description: The reporter's filename is kept, sanitized, on the report file and used only as a reviewer's download name; blob keys stay opaque identifiers and bytes are still reached through a pre-signed URL.
type: adr
status: accepted
date: 2026-09-23
decision-makers: Chase Florell
keywords: attachments, filenames, privacy, downloads, content-disposition, blob keys, reviewer
---

# ADR-0097 — A reviewer downloads an attachment under its sanitized original name

## Status

Accepted. This ADR:

- **amends** REQ-MED-003, which said the client filename is never persisted or
  returned to an admin;
- **amends** [ADR-0026](ADR-0026-presigned-urls-and-private-blob-storage.md) on
  one point only: a read URL's forced download name may be the reporter's
  sanitized filename rather than a server-minted one. Bytes are still reached
  only through a short-lived pre-signed URL, and no API route serves them.

## Context

A safety officer downloading five attachments gets five files named after
opaque ids, and has to open each one to learn which is the witness statement
and which is the photo of the launch. The reporter already named them. The
owner wants that name kept and used when a reviewer downloads the file, while
the object in storage stays named by an identifier.

The old rule refused the filename for a reason that still stands: a filename
can identify a person or a place (`jsmith_crash_pemberton.jpg`), and can even
identify the reporter. The decision is how to keep the name without letting it
leak anywhere a reviewer's own screen is not.

## Decision

**The browser sends each attachment's filename with its upload id in the final
submission. The API sanitizes it and stores it on the report file. A reviewer's
download link forces the download under that name. Nothing else ever reads
it.**

- **Sanitized, not trusted.** Only the last path segment is kept. Control
  characters, quotes, and the characters `\ / : * ? < > | ;` are removed,
  whitespace is collapsed, and the result is cut to 255 characters. A name that
  sanitizes to nothing is stored as nothing, and the download falls back to the
  server-minted `<file id>.<extension>`.
- **The extension follows the bytes served.** A reviewer who opens an image
  receives the stripped derivative, and a HEIC original's derivative is JPEG, so
  `IMG_0412.HEIC` downloads as `IMG_0412.jpg`. The name's stem is the reporter's;
  the extension is always the served type's, so a name can never make a file
  look like something it is not.
- **The name reaches only the reviewer.** It is not logged, placed in an
  exception or problem response, used in a storage key, sent to the model,
  included in any public DTO, or sent with the upload itself. It rides in the
  pre-signed URL's `Content-Disposition` override, encoded per RFC 6266 with an
  ASCII fallback and a UTF-8 `filename*` so accented French names survive.
- **Storage stays opaque.** A report's blobs are named by the report file's own
  id: `<report id>/original/<file id>` and `<report id>/stripped/<file id>`. The
  row and its bytes share one identifier; the filename appears in neither key.
- **Downloads stay pre-signed.** The API authorizes the reviewer, writes the
  audit row, and returns a short-lived URL, exactly as before. The owner first
  asked for the API to stream the bytes; a pre-signed URL carrying the original
  name gives the same result without making the API a second door onto private
  media.

## Consequences

- `report_files` gains a nullable `original_file_name`. Existing rows keep the
  minted download name.
- A reviewer can read a reporter's filename, and may learn from it something the
  reporter did not mean to share. The reviewer already sees the unredacted
  original, so the name adds little to what they can see, and it goes nowhere
  else.
- The submission's file-upload answer carries `attachments`, each an upload id
  and a filename, in place of a bare list of ids.

## Alternatives rejected

- **Keep minted names.** It keeps the old rule whole, but at the cost of the
  reviewer experience the owner wants.
- **Stream downloads through the API.** Same file name for the reviewer, but it
  reverses ADR-0026's "no route serves blob bytes" and puts every download,
  up to 50 MB, through the API.
- **Store the name exactly as given.** A path, a control character, or a quote
  in a name would reach a response header.
- **Send the name with the upload.** That would put the name on the server
  before the reporter submits, attached to a file that may never be claimed.

## Related

- [ADR-0026](ADR-0026-presigned-urls-and-private-blob-storage.md) — pre-signed reviewer reads, kept.
- [ADR-0067](ADR-0067-a-reporter-must-be-a-member-and-is-not-recorded.md) — why the name reaches nobody but a reviewer.
- [ADR-0096](ADR-0096-an-attachment-uploads-on-attach-and-is-claimed-at-submission.md) — the upload and claim flow this rides on.
- Issue #360.
