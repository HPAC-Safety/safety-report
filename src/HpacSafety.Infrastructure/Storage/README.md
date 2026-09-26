---
title: Private attachment storage
description: How uploads reach private quarantine by pre-signed PUT and how verified bytes are reached afterwards.
type: readme
---

# Private attachment storage

The store mints the pre-signed PUT a browser sends an upload to, promotes
claimed originals/derivatives to server-minted keys, and grants short-lived
authorized reviewer reads. It never creates a public object URL.

`POST /api/v1/uploads` mints an upload id and, through
`IBlobStore.CreateUploadUrl`, a PUT signed for one key, one `Content-Type`, and
one exact `Content-Length`; storage refuses any other. Only a key whose
compartment `BlobKey.AcceptsDirectUpload` may be signed — today,
`quarantine/<upload id>` alone. The upload waits there until a submission
claims it, reading it through `OpenReadRange` to sniff it; deleting it removes
every version
([ADR-0096](../../../docs/decisions/ADR-0096-an-attachment-uploads-on-attach-and-is-claimed-at-submission.md),
[ADR-0126](../../../docs/decisions/ADR-0126-an-attachment-uploads-straight-to-quarantine-by-pre-signed-put.md)).
Unclaimed uploads, and those left behind by a failed submission, expire by
lifecycle rule.

Reviewer access is limited to verified image/video derivatives and validated
document originals. Documents are forced downloads; originals are never used as
a fallback preview. Report-linked objects remain private after soft deletion.

`S3BlobStore` is the only adapter. In AWS it reaches the bucket through the
task role; in development it points at the RustFS container with a service URL,
path-style addressing, and local credentials, and signs reviewer URLs for the
public host the browser can reach. The bucket accepts a cross-origin `PUT`
only from the site origins (`site_origins` in Terraform; the dev server's
origin in docker-compose). `BlobStoreContractTests` run it against
RustFS (ADR-0110). See [`features/media/media.feature`](../../../features/media/media.feature).
