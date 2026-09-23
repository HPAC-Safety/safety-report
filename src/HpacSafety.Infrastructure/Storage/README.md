---
title: Private attachment storage
description: How validated uploads reach private quarantine and how verified bytes are reached afterwards.
type: readme
---

# Private attachment storage

The store accepts validated uploads from the API into private quarantine,
promotes claimed originals/derivatives to server-minted keys, and grants
short-lived authorized reviewer reads. It never creates a public object URL.

Each upload arrives through `POST /api/v1/uploads` and waits at
`quarantine/<upload id>` until a submission claims it; deleting it removes
every version
([ADR-0096](../../../docs/decisions/ADR-0096-an-attachment-uploads-on-attach-and-is-claimed-at-submission.md)).
Unclaimed uploads, and those left behind by a failed submission, expire by
lifecycle rule.

Reviewer access is limited to verified image/video derivatives and validated
document originals. Documents are forced downloads; originals are never used as
a fallback preview. Report-linked objects remain private after soft deletion.

`S3BlobStore` is the only adapter. In AWS it reaches the bucket through the
task role; in development it points at the MinIO container with a service URL,
path-style addressing, and local credentials, and signs reviewer URLs for the
public host the browser can reach. `BlobStoreContractTests` run it against
MinIO. See [`features/media/media.feature`](../../../features/media/media.feature).
