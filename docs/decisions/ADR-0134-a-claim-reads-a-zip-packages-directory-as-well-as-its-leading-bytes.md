---
title: A claim reads a zip package's directory as well as its leading bytes
description: Validating a claimed upload reads its stored size and only the ranges sniffing asks for, one bounded window at a time; for a DOCX or ODT that includes the zip directory at the file's end, so the claim does not read leading bytes alone.
type: adr
status: accepted
date: 2026-09-26
decision-makers: Chase Florell
keywords: attachments, uploads, quarantine, validation, sniffing, zip, DOCX, ODT, ranged read, memory
---

# ADR-0134 — A claim reads a zip package's directory as well as its leading bytes

## Status

Accepted. **Partially supersedes**
[ADR-0126](ADR-0126-an-attachment-uploads-straight-to-quarantine-by-pre-signed-put.md)
on one point: the claim reads "only as many leading bytes as sniffing needs",
and "one small ranged read per upload". Everything else ADR-0126 decided
stands.

## Context

ADR-0126 moved validation from the upload to the claim. The claim sniffs each
upload in storage without fetching the whole file. For most formats the leading
bytes decide: a signature, an image header, a bounded text prefix.

A DOCX and an ODT are zip packages. Their magic number is every zip's, so
[REQ-MED-008](../../features/media/media.feature) tells them apart by their
internal package shape. A zip's directory sits at the end of the file, not
the start. Leading bytes alone would refuse every DOCX and ODT larger than the
first read as unrecognised, and the building of #462 found this.

## Decision

**The claim reads a claimed upload through a seekable view that fetches only
the ranges its sniffer asks for, one bounded window (64 KiB) at a time.**

- An image, a video, a PDF, a DOC, an RTF, or plain text is decided by its
  leading bytes, usually in one request.
- A DOCX or ODT is decided by its leading bytes and its zip directory, usually
  in two.
- At most one window is held in memory; nothing is spooled and the whole file
  is never fetched.

`REQ-SUB-075` states it: the API reads only the upload's size and the bytes
sniffing needs, never the whole file into memory.

## Consequences

- The claim makes one metadata request and one or two small ranged reads per
  upload, on top of the server-side copy.
- A sniffer that one day needs more of a file than its header costs one window
  per region it reads, and never more than the file.

## Alternatives rejected

- **Leading bytes only.** It refuses every DOCX and ODT whose directory falls
  past the first read.
- **Spool the whole upload to a temporary file, as the Worker does.** It works,
  but pulls up to 25 MB through the API for every document to answer a question
  a few kilobytes decide, and puts the file on the API's disk.
- **Trust a DOCX or ODT's magic number at claim and leave the package check to
  the Worker.** A mislabelled zip would then reach a report, which is what the
  claim's check exists to prevent.

## Related

- [ADR-0126](ADR-0126-an-attachment-uploads-straight-to-quarantine-by-pre-signed-put.md) — validation at claim, partially superseded here.
- [ADR-0098](ADR-0098-submission-copies-the-original-and-the-worker-makes-the-derivative.md) — the Worker's own validation, unchanged.
- Issue #462.
