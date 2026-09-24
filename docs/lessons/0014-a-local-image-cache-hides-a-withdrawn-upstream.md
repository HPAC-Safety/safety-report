---
title: A local image cache hides a withdrawn upstream
description: MinIO withdrew its public images, so every CI run failed to pull the test S3 server while every developer machine kept passing on a cached arm64 copy.
type: lesson
date: 2026-09-24
issue: 406
status: accepted
---

# Lesson 0014 — A local image cache hides a withdrawn upstream

## Symptom

`test` and `coverage` went red on `main` (11ac472), and on every pull request
opened after it, including Renovate's vite patch bump #400. All 387 failing
tests failed with the same Testcontainers pull error:

```
Docker API responded with status code='InternalServerError', response='{"message":"unauthorized: access to the requested resource is not authorized"}'
```

A developer machine ran the same suites green, and the failing PR looked like
the dependency bump had broken something it had not touched.

## Root cause

The test S3 server was pinned to
`quay.io/minio/minio:RELEASE.2025-04-22T22-12-26Z`. MinIO had archived its
repository in April 2026 and then withdrew anonymous access to its images, on
quay.io and on Docker Hub alike.

- Pinning a tag protects against an image changing. It does nothing when the
  image disappears.
- Every developer machine still held the image in its Docker cache, so no
  local run could reproduce the failure. The cached copy was arm64 only, so it
  could not have been re-published for CI either.
- The failure surfaced on whichever pull request ran next, and that happened
  to be a dependency bump. That made it look like a dependency problem.

## Spec delta

[ADR-0110](../decisions/ADR-0110-rustfs-replaces-minio-as-the-development-s3-server.md):

- RustFS replaces MinIO, pinned in one place (`tests/Shared/S3Emulator.cs`) and
  in `docker-compose.yml`.
- Renovate tracks both pins through the `RustFS` group, so the image moves
  through reviewed pull requests rather than silently going stale on a dead
  upstream.

## Scenario

No scenario. This is a property of the test and development tooling, not of
the system `features/` describes. The 21 tests in
`EmulatedS3BlobStoreContractTests` prove the replacement keeps every storage
guarantee the old server did.

## Skill

[`test-hpac-safety`](../../skills/test-hpac-safety/SKILL.md) now says:

- A test container's image comes from an upstream that is still maintained and
  publishes the architectures CI runs on.
- When a pull fails in CI but passes locally, ask the registry anonymously
  (`docker pull` after `docker rmi`, or the registry's token endpoint) before
  trusting the local result.
