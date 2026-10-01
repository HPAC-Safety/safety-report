---
title: A paused video that played on when its link was replaced
description: A visitor's paused video started playing again when its expired link was replaced, because the player autoplays every source it loads. The scenario never said whether the video was playing, so a test raced the clip to its end.
type: lesson
date: 2026-09-29
issue: 680
status: accepted
---

# Lesson 0038 — A paused video that played on when its link was replaced

## Symptom

Playwright scenario REQ-MED-033 ("An expired link is replaced and the video
resumes where it was") failed 7 of 15 runs at four workers, and more under
load. The last step timed out on `currentTime` reading 2, the end of the
2 s test clip, instead of 1.2.

## Root cause

- `AttachmentLightbox` puts `autoPlay` on its `<video>`. The replacement source
  loads into the same element, so the browser autoplays it too, whatever state
  the visitor left the video in. The player recorded whether the video was
  playing, and resumed a playing one, but never kept a paused one paused.
- The scenario said only "part-way through" and "resumes from where it was". It
  never said playing or paused, so the step left the autoplaying video running
  and asserted a moving `currentTime` of about 1.2. It passed only while the
  poll happened to land inside a 100 ms window; under load it missed, and the
  video ended at 2.
- This was a product defect (a paused video resumed playing), which the racing
  assertion only sometimes exposed.

## Spec delta

- REQ-MED-033 now says the visitor paused the video, and that it resumes from
  where it was, still paused.
- `.spec/features/media/README.md` says a video resumes paused if it was paused and
  playing if it was playing.
- The lightbox pauses a resumed video whose recorded state was paused.

## Scenario

REQ-MED-033.

## Skill

None; the claim is the remedy.
