---
title: An outcome computed and never recorded
description: Every submitted image was decoded, re-encoded, and stripped of EXIF, then the fact that it had been was thrown away — no ReportFile ever became viewable.
type: lesson
date: 2026-09-22
issue: 311
status: accepted
---

# Lesson 0005 — An outcome computed and never recorded

## Symptom

Building the reviewer attachment-link endpoints (#311) required a `ReportFile`
whose stripped derivative existed and was recorded. None did. Every image and
video ever submitted through `POST /api/v1/reports` stayed
`AwaitsStripping == true` forever, so `ReviewerMediaLink.CreateViewUrlAsync`
would have refused every one of them.

## Root cause

`ReportSubmissionEndpoints.IngestFilesAsync` already called
`MediaIngestor.IngestAsync` synchronously, in the request — decoding,
re-encoding, and EXIF-stripping every accepted image right there, no Worker
outbox item involved. The full `MediaIngestOutcome` — original key, derivative
key, stripped-at timestamp — came back correct. The endpoint read three fields
off it (`OriginalKey`, `ContentType`, `ByteSize`) to call `report.AddFile(...)`
and never read the other two. `ReportFile.RecordStripped` existed, was
correct, and was simply never called from the one place that had everything
it needed to call it.

Nothing failed loudly because nothing needed the derivative to exist yet — the
image-processing scenarios that would have caught it (REQ-MED-006/009) were
still `@ignore`, and the endpoint's own test only asserted `202 Accepted`. The
gap was invisible until a scenario needed to read the state back.

## Spec delta

No new claim: `REQ-MED-010` — "A reviewer gets a short-lived URL only for
successfully processed media" — already said a successfully processed image
must be viewable. The gap was that nothing before this fix could have made its
`Given` true. The fix (`ReportSubmissionEndpoints.IngestFilesAsync` now calls
`file.RecordStripped` when `outcome.IsViewable`) is what lets that scenario be
un-ignored honestly rather than by an endpoint that had never actually
produced the state it describes.

The general rule: **when a computation's result has fields the immediate
caller doesn't need, check whether a *later* caller does before discarding
them.** `MediaIngestOutcome` was designed to carry exactly what `ReportFile`
needs to represent both possible outcomes (retained-only vs. stripped); using
three of its five fields and dropping the rest silently is the same failure
mode as building a struct nobody reads in full.

## Scenario

`REQ-MED-010` now exercises the fix directly: `AttachmentAccessSteps` seeds a
`ReportFile` through the same `RecordStripped` call path and proves a reviewer
can request it. `ReportSubmissionEndpointTests.GivenAFileUploadAnswer_WhenSubmittedWithAValidImage_ThenAcceptedAndFileIsLinked`
additionally asserts, against a real submission, that the derivative is
recorded — the specific regression this lesson is about.

## Skill

None — the claim is the remedy.
