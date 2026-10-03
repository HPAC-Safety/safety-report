---
title: A registry limit and a retry that outlasted nothing
description: public.ecr.aws's per-IP anonymous rate limit throttled release builds on shared GitHub runners, and the mirror script's fix then hit a permanent skopeo error that its retry loop spent ten minutes retrying anyway.
type: lesson
date: 2026-09-29
issue: 629
status: accepted
kind: incident
---

# Lesson 0032 — A registry limit and a retry that outlasted nothing

## Symptom

The release `build` job kept failing on the API image: `public.ecr.aws`
answered `429 Too Many Requests — Data limit exceeded` for
`awsguru/aws-lambda-adapter:1.1.0`, and #621's five-attempt retry could not
outlast it. After the mirror fix, release run 36505352236 failed in the new
"Mirror the Lambda Web Adapter" step: every attempt gave `Docker references
with both a tag and digest are currently not supported`, and the retry loop
still spent about ten minutes backing off before giving up.

## Root cause

- `public.ecr.aws`'s anonymous pull limit is per source IP, and GitHub's
  hosted runners share IPs with everyone else's jobs, so the quota was spent
  by other people's traffic, not this repository's.
- `tools/build/mirror-lambda-adapter.sh` passed skopeo a source reference carrying
  both a tag and a digest
  (`public.ecr.aws/awsguru/aws-lambda-adapter:1.1.0@sha256:…`). The skopeo
  version on `ubuntu-latest` rejects that form outright — a permanent error,
  not a transient one — but the retry loop treated every failure the same
  way and retried it anyway.

## Spec delta

None upstream: this is where a build-time image comes from and how a script
invokes skopeo, not a product claim.
[ADR-0042](../decisions/ADR-0042-lambda-hosted-api-with-fargate-migration-path.md)
is amended: the
adapter is mirrored into `ghcr.io/hpac-safety/aws-lambda-adapter`, keyed by
its upstream digest alone (`@sha256:…`, no tag), and the retry loop now
retries only 429/rate-limit/timeout/connection/5xx errors, failing everything
else — a malformed reference included — at once.

## Scenario

None. This is a process lesson, and no scenario can prove it.

## Skill

[`manage-hpac-infrastructure`](../../skills/manage-hpac-infrastructure/SKILL.md)
gains "Workflow mechanics":

- pull a CI base image from a registry with no per-IP anonymous limit — mirror
  it into this org's own registry when the only upstream source has one;
- a retry loop retries only errors that can clear on their own; anything else
  fails on the first attempt.
