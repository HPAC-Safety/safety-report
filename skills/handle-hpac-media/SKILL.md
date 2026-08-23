---
name: handle-hpac-media
description: Protect uploaded HPAC occurrence-report media throughout upload, storage, metadata stripping, quarantine, and reviewer access. Use when changing blob storage, pre-signed URLs, BlobKey, upload slots, media derivatives, content sniffing, codecs, filenames, metadata, retention, or media tests.
---

# Keep report media private

Read `docs/data-handling.md`, ADR-0025, ADR-0026, and the media sections of the
relevant slice README before changing uploads.

## Enforce the path

- Never proxy uploaded or downloaded blob bytes through the API. Browsers PUT
  through a one-key pre-signed URL and reviewers read through a short-lived
  pre-signed GET from a private bucket.
- Keep every key under its report id:
  `<report-id>/original/<minted-name>`,
  `<report-id>/stripped/<minted-name>`, or
  `quarantine/<report-id>/<minted-name>`. Enforce this in `BlobKey`.
- Mint stored filenames. Never place a client-supplied filename or other client
  string in a key, URL, or path.
- Treat unguessable ids as reinforcement, never authorization.

## Fail closed

- Sniff content types from bytes; never trust the client declaration. Keep the
  accepted format set closed.
- Add a format only when metadata can be stripped or the domain explicitly
  models that it cannot be viewed. Never fall back from a missing derivative to
  the original.
- Fail process startup when an accepted codec is unavailable.
- Route all upload and reviewer-link creation through their single enforcement
  types and protect that architecture with tests.
- Specify infrastructure lifecycle guarantees exactly. In a versioned bucket,
  quarantine deletion needs both current and noncurrent-version expiration.

Use [`test-hpac-safety`](../test-hpac-safety/SKILL.md) to create synthetic
fixtures that prove metadata existed before stripping and is absent afterward.
