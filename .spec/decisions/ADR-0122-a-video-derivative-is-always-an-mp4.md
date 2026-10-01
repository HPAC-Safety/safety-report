---
title: A video's derivative is always an MP4
description: Whatever container a reporter's video arrived in, the remux writes an MP4, the verification requires one, and the derivative is stored, downloaded, and served as video/mp4, so an iPhone recording plays inline in every browser.
type: adr
status: accepted
date: 2026-09-24
decision-makers: Chase Florell
keywords: video, mp4, quicktime, remux, ffmpeg, derivative, playback, ADR-0094, ADR-0117
---

# ADR-0122 — A video's derivative is always an MP4

**Status:** Accepted. **Amends**
[ADR-0094](ADR-0094-video-is-remuxed-not-transcoded-and-never-refused.md): a
video is still remuxed and never transcoded, but always into MP4, rather than
into "the container it arrived in".

## Context

A published report embeds its videos in a native `<video>` element
([ADR-0117](ADR-0117-a-published-report-shows-the-reporters-photos-and-video.md)).
An iPhone records QuickTime.

The remuxer already wrote its output to `output.mp4`, so ffmpeg chose the MP4
muxer from the extension. The derivative's bytes were MP4, but everything
downstream labelled them by the upload's type:
- the stored object was `video/quicktime`;
- a reviewer's download was named `.mov`;
- the public link served `video/quicktime`, which Chrome and Edge may refuse to
  play inline.

The container was an accident of a file name, and nothing verified it (#424).

## Decision

**Every video's derivative is an MP4 container.**

- **The remux names the format.** It passes `-f mp4` rather than inferring the
  format from a file name. It still copies packets and never decodes.
- **Verification requires it.** The derivative's `major_brand` must be
  `isom`, the brand ffmpeg's MP4 muxer writes. A QuickTime brand (`qt  `) is
  refused like any other unexpected output, so the video is kept with no
  derivative (ADR-0094).
- **One type everywhere.** `MediaType.DerivativeForm` names the type of
  the bytes a derivative holds: an image's stripped form, and `video/mp4` for
  every video. The Worker stores the derivative under it, a reviewer's
  download takes its extension (REQ-MED-020), and the public link serves it.
- **A stream MP4 cannot hold** makes the remux fail, and the video is kept with
  no derivative, as for any remux that fails. It is never transcoded to fit.

## Rejected alternatives

- **Keep the arriving container and label a QuickTime derivative `video/mp4`.**
  That is less work, but it misstates the bytes, and a reviewer's `.mov`
  download would still disagree with what was served.
- **Leave QuickTime as QuickTime.** Chrome visitors would get a video that will
  not play, and the page would remove it after repeated errors.
- **Transcode to a baseline codec.** ADR-0094 rejects transcoding. MP4 holds
  the H.264 and HEVC a phone records.

## Consequences

- An iPhone recording plays inline wherever its codec does. H.264 plays
  everywhere; HEVC plays where the browser and hardware support it.
- A QuickTime file carrying a stream MP4 cannot hold, such as ProRes, gets no
  derivative, and a reviewer downloads the original, as ADR-0094 already allows.
- No stored derivative needs rewriting. ffmpeg reached the Worker image only
  with #423 (ADR-0118), and no environment with it has been deployed (#30).
