---
title: Submission copies the original and the Worker makes the derivative
description: Claiming an upload is a server-side copy into the report's original compartment; the Worker's attachment handler sniffs the original and writes the stripped derivative off the request path.
type: adr
status: accepted
date: 2026-09-23
decision-makers: Chase Florell
keywords: attachments, worker, outbox, derivatives, S3 copy, quarantine, submission
---

# ADR-0098 — Submission copies the original and the Worker makes the derivative

## Status

Accepted. This ADR **amends**
[ADR-0096](ADR-0096-an-attachment-uploads-on-attach-and-is-claimed-at-submission.md):
the submission no longer judges and strips a claimed upload inside the request.
It completes what `CON-IF-008` and REQ-MED-009 already required: one
`ProcessAttachment` outbox message per file, handled by the Worker.

## Context

After #360 the submission request still read every claimed upload into memory,
sniffed it, re-encoded images, remuxed videos, and wrote both the original and
the derivative before it committed. It also enqueued a `ProcessAttachment`
message per file that nothing consumed. Five 50 MB videos made one slow,
memory-heavy request, and a crash in the imaging library failed the whole
report.

Moving the work to the Worker raises one question: where are the bytes between
the commit and the Worker's run? A claimed upload sits in `quarantine/`, which
the lifecycle rule empties after about a day. A Worker backlog longer than that
would lose the file.

## Decision

**At submission the API copies each claimed upload, unchanged and server-side,
from `quarantine/<upload id>` to `<report id>/original/<file id>`, records the
file with the type and size its upload was validated as, commits, and removes
the upload. The Worker's `ProcessAttachment` handler later reads the original,
sniffs it again, and writes the stripped derivative.**

- **The copy is a storage operation.** S3 `CopyObject` moves the bytes inside the
  bucket: nothing passes through the API, and nothing is buffered. The upload
  was sniffed and validated when it arrived (ADR-0096), and the stored object's
  type is the validated one, so the file row takes its type and size from the
  object's metadata.
- **The original is safe from the lifecycle rule** the moment the report
  commits, because it no longer lives under `quarantine/`.
- **The Worker judges the original again.** It sniffs the bytes and requires them
  to be the recorded type. A mismatch or unreadable file is recorded as a safe
  processing error code, and the file stays inaccessible to reviewers
  (REQ-MED-013). An image that cannot be stripped fails the same way. A video
  that cannot be remuxed is retained with no derivative (ADR-0094), and a
  document never has one.
- **Each file is its own outbox message.** One slow or corrupt file neither rolls
  back the report nor delays another file (REQ-MED-009). The handler reads
  current state, so a redelivered message for an already-processed file writes
  nothing new, and a file whose report was deleted is skipped.
- **Until the Worker finishes, a reviewer sees the file as awaiting processing.**
  No link is issued for it, exactly as for a video with no derivative.

## Consequences

- The submission request is fast and flat in memory whatever the attachments
  weigh.
- The Worker needs the storage adapter, the imaging library, and ffmpeg. Its IAM
  role can now read and write the report compartments. ffmpeg in the deployed
  Worker image is deferred to #30; without it a video is retained with no
  derivative, which ADR-0094 already permits.
- Development runs the Worker in docker-compose, since without it no derivative
  would ever appear.
- The Worker still buffers each file in memory while it strips it; #362 streams
  it instead.

## Alternatives rejected

- **Leave the upload in quarantine for the Worker to promote.** This keeps the
  submission simpler, but a Worker delay past the lifecycle window loses the
  file.
- **Keep processing inside the request.** This is the behaviour being replaced.
- **Copy through the API** (read, then write). The bytes would pass through the
  API process for no benefit that a storage-side copy does not give.

## Related

- [ADR-0002](ADR-0002-transactional-outbox.md) — the outbox the handler consumes.
- [ADR-0094](ADR-0094-video-is-remuxed-not-transcoded-and-never-refused.md) — video remux, and retention without ffmpeg.
- [ADR-0096](ADR-0096-an-attachment-uploads-on-attach-and-is-claimed-at-submission.md) — the upload and claim flow, amended here.
- Issues #361, #362, #30.
