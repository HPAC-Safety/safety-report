---
title: Video is remuxed, never transcoded, and an unstrippable video is kept rather than refused
description: ffmpeg runs as a locked-down subprocess doing a stream-copy remux that drops container metadata and non-audiovisual tracks; when that cannot produce a clean derivative the original is retained and reachable only as a reviewer download.
type: adr
status: accepted
date: 2026-09-22
decision-makers: Chase Florell
keywords: video, ffmpeg, remux, metadata, attachments, privacy, media
---

# ADR-0094 — Video is remuxed, never transcoded, and an unstrippable video is kept rather than refused

## Status

Accepted. Amends [ADR-0025](ADR-0025-magick-net-for-exif-stripping.md), which
chose Magick.NET for images and deliberately left video unhandled.

## Context

Images get a safe derivative today: decode, re-encode, every metadata profile
dropped. Video is detected and then stops — `MediaType.StrippedForm` returns
null for it and the image stripper throws. `MagickFormats.cs` already records
why ImageMagick is not the answer: shelling out to a video delegate is a
liability, not a feature.

Footage here comes from a phone camera roll. That means H.264 or HEVC in MP4 or
QuickTime, and it means the privacy problem is concentrated in three places:

- **Container atoms** — `©xyz`, `com.apple.quicktime.location.ISO6709`, make,
  model, software, creation date.
- **Timed-metadata tracks.** An iPhone recording carries `mebx` streams holding
  motion and orientation, and on some captures location. These are *separate
  streams*, so a naive stream copy carries them through intact while the file
  looks clean by every tag-based check.
- **Per-stream metadata**, which survives a global metadata wipe.

Two further facts shape the decision. Whatever we run parses attacker-supplied
bytes: a reporter uploads the file. And the association will not refuse a
reporter's footage because our toolchain could not clean it — a video nobody
can submit is a safety report nobody files.

## Decision

**ffmpeg, as a subprocess, doing a stream-copy remux. Never a transcode.**

```
ffmpeg -nostdin -hide_banner -loglevel error
       -i <input>
       -map 0:v:0 -map 0:a? -map -0:d -map -0:s -map -0:t
       -c copy
       -map_metadata -1 -map_metadata:s:v -1 -map_metadata:s:a -1
       -movflags +faststart
       <output>
```

Three properties earn this shape:

1. **A remux never decodes the video bitstream.** `-c copy` moves compressed
   packets between containers. The hostile-input surface is the demuxer, not
   the H.264 or HEVC decoder, which is the larger and more frequently
   exploited of the two. A transcode would decode every frame, and it would
   also re-encode — slower, lossy, and no cleaner.
2. **Streams are selected, not filtered.** `-map 0:v:0 -map 0:a?` takes video
   and optional audio and nothing else; the explicit negative maps drop data,
   subtitle and timed-metadata tracks. This is what removes the `mebx` tracks a
   tag-based wipe leaves behind.
3. **Metadata is dropped globally and per stream.** `-map_metadata -1` alone
   leaves stream-level tags in place.

The output is verified before it is accepted: it must contain exactly the
expected video and optional audio stream, no data streams, and no location,
make, model or creation tags. A derivative that fails verification is not a
derivative.

**Subprocess, not a library binding.** ffmpeg is invoked as a child process
with a fixed argument list, no shell, no network, a wall-clock timeout, and
bounded output. Linking `libavcodec` through a P/Invoke wrapper would put a
decoder in our address space and pull LGPL relinking obligations in for no
gain.

**An unstrippable video is kept, not refused.** When ffmpeg is unavailable, the
remux fails, or verification rejects the result, the upload still succeeds and
the original is retained with no derivative. A reviewer reaches it the way they
reach a document: a short-lived, forced download of the unredacted original,
authorized, never inline-rendered, and never published. The file is marked so
the difference is recorded rather than inferred.

This is the same trade the repository already makes for documents, which are
validated and kept exactly as they arrived. The anonymity contract lives at the
summary, which never sees an attachment at all
([ADR-0003](ADR-0003-anonymization-pipeline.md)); attachments are private to
authorized reviewers regardless, and no attachment is ever published.

## Consequences

- `features/media/media.feature` changes with this record, in the same pull
  request: REQ-MED-007 says remux rather than "remuxed or transcoded", gains
  the stream-selection and verification requirements, and REQ-MED-013 no longer
  claims a video that cannot be stripped is inaccessible — it is retained and
  reviewer-downloadable, like a document. A new claim covers the fallback.
- The Worker and API container images need ffmpeg. CI installs it explicitly
  rather than relying on a runner image that happens to ship it.
- Licensing is not an obstacle: the binary is invoked as a separate process and
  the image is private and never distributed, so LGPL/GPL obligations do not
  attach to this repository's MIT-licensed code. That is a consequence of not
  distributing, not of being non-commercial.
- Remuxing does not re-encode, so it neither decodes nor encodes H.264/HEVC —
  the codec-patent question that a transcode would raise does not arise.
- A reviewer may now see an unstripped original for the minority of files that
  fail. That is deliberate and recorded, not a gap.

## Alternatives

- **Transcode to a known-clean baseline.** Rejected: it decodes hostile input
  frame by frame, costs CPU per upload, degrades the footage a reviewer is
  trying to study, and removes no metadata that the remux leaves behind.
- **AWS Elemental MediaConvert.** Rejected for now: it moves hostile decode out
  of our container, which is a genuine benefit, but it adds per-minute cost, an
  asynchronous job to orchestrate and poll, IAM and Terraform surface, and a
  development story that either stubs it or spends money — for a system
  deliberately kept small. Worth revisiting if volume ever justifies it.
- **Refuse a video that cannot be stripped.** Rejected by the association: a
  reporter's footage is evidence about a real occurrence, and discarding it
  because our toolchain could not clean the container loses the report to save
  metadata that only an authorized reviewer would ever have seen.
- **Strip with Magick.NET, as images are.** Rejected: ADR-0025 and
  `MagickFormats.cs` already reject the video delegate path.

## Related

- [ADR-0003](ADR-0003-anonymization-pipeline.md)
- [ADR-0025](ADR-0025-magick-net-for-exif-stripping.md)
- [ADR-0026](ADR-0026-presigned-urls-and-private-blob-storage.md)
