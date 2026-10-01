---
title: Step bindings
description: Generated map from every claim to the step-definition files that bind its steps, with the places the specification and the code disagree.
type: guide
---

# Step bindings

> **Generated file — do not edit by hand.**
> Regenerate with `node tools/bindings.mjs`. CI fails on a difference, and on a
> built claim with a step no definition binds
> ([ADR-0184](decisions/ADR-0184-a-generated-map-binds-every-claim-to-its-step-definitions.md)).
> One block per claim, step, or definition, and no totals, so branches merge it
> without conflicting
> ([ADR-0106](decisions/ADR-0106-every-line-of-the-matrix-derives-from-one-source-item.md)).

Each claim lists the step-definition files its runner binds: Reqnroll for a
claim without `@ui`, playwright-bdd for one with it. An `Unbound` step is one no
definition matches; a built claim may never have one.

## Claims: ai-anonymization

### REQ-AI-001

- [SummarizationOutboxSteps.cs](../tests/HpacSafety.Acceptance.Tests/SummarizationOutboxSteps.cs)

### REQ-AI-002

- [AiAnonymizationSteps.cs](../tests/HpacSafety.Acceptance.Tests/AiAnonymizationSteps.cs)

### REQ-AI-003

- [AiAnonymizationSteps.cs](../tests/HpacSafety.Acceptance.Tests/AiAnonymizationSteps.cs)

### REQ-AI-004

- [AiAnonymizationSteps.cs](../tests/HpacSafety.Acceptance.Tests/AiAnonymizationSteps.cs)

### REQ-AI-005

- [AiAnonymizationSteps.cs](../tests/HpacSafety.Acceptance.Tests/AiAnonymizationSteps.cs)

### REQ-AI-006

- [AiAnonymizationSteps.cs](../tests/HpacSafety.Acceptance.Tests/AiAnonymizationSteps.cs)

### REQ-AI-007

- [AiAnonymizationSteps.cs](../tests/HpacSafety.Acceptance.Tests/AiAnonymizationSteps.cs)

### REQ-AI-008

- [SummarizationOutboxSteps.cs](../tests/HpacSafety.Acceptance.Tests/SummarizationOutboxSteps.cs)

### REQ-AI-009

- [SummarizationOutboxSteps.cs](../tests/HpacSafety.Acceptance.Tests/SummarizationOutboxSteps.cs)

### REQ-AI-011

- [SummarizationProviderSteps.cs](../tests/HpacSafety.Acceptance.Tests/SummarizationProviderSteps.cs)

### REQ-AI-016

- [SummarizationOutboxSteps.cs](../tests/HpacSafety.Acceptance.Tests/SummarizationOutboxSteps.cs)

### REQ-AI-017

- [SummarizationOutboxSteps.cs](../tests/HpacSafety.Acceptance.Tests/SummarizationOutboxSteps.cs)

### REQ-AI-019

- [SummarizationOutboxSteps.cs](../tests/HpacSafety.Acceptance.Tests/SummarizationOutboxSteps.cs)

### REQ-AI-020

- [SummarizationOutboxSteps.cs](../tests/HpacSafety.Acceptance.Tests/SummarizationOutboxSteps.cs)

### REQ-AI-021

- [SummarizationOutboxSteps.cs](../tests/HpacSafety.Acceptance.Tests/SummarizationOutboxSteps.cs)

### REQ-AI-022

- [SummarizationProviderSteps.cs](../tests/HpacSafety.Acceptance.Tests/SummarizationProviderSteps.cs)

### REQ-AI-023

- [SummarizationProviderSteps.cs](../tests/HpacSafety.Acceptance.Tests/SummarizationProviderSteps.cs)

### REQ-AI-024

- [SummarizationProviderSteps.cs](../tests/HpacSafety.Acceptance.Tests/SummarizationProviderSteps.cs)

### REQ-AI-027

- [SummarizationOutboxSteps.cs](../tests/HpacSafety.Acceptance.Tests/SummarizationOutboxSteps.cs)

### REQ-AI-028

- [AiAnonymizationSteps.cs](../tests/HpacSafety.Acceptance.Tests/AiAnonymizationSteps.cs)

### REQ-AI-029

- [SummarizationOutboxSteps.cs](../tests/HpacSafety.Acceptance.Tests/SummarizationOutboxSteps.cs)

### REQ-AI-030

- [SummarizationProviderSteps.cs](../tests/HpacSafety.Acceptance.Tests/SummarizationProviderSteps.cs)

### REQ-AI-031

- [SummarizationOutboxSteps.Sections.cs](../tests/HpacSafety.Acceptance.Tests/SummarizationOutboxSteps.Sections.cs)
- [SummarizationOutboxSteps.cs](../tests/HpacSafety.Acceptance.Tests/SummarizationOutboxSteps.cs)

### REQ-AI-032

- [SummarizationOutboxSteps.Sections.cs](../tests/HpacSafety.Acceptance.Tests/SummarizationOutboxSteps.Sections.cs)
- [SummarizationOutboxSteps.cs](../tests/HpacSafety.Acceptance.Tests/SummarizationOutboxSteps.cs)

### REQ-AI-033

- [SummarizationOutboxSteps.Sections.cs](../tests/HpacSafety.Acceptance.Tests/SummarizationOutboxSteps.Sections.cs)
- [SummarizationOutboxSteps.cs](../tests/HpacSafety.Acceptance.Tests/SummarizationOutboxSteps.cs)

### REQ-AI-034

- [SummarizationOutboxSteps.Sections.cs](../tests/HpacSafety.Acceptance.Tests/SummarizationOutboxSteps.Sections.cs)

### REQ-AI-035

- [SummarizationOutboxSteps.Sections.cs](../tests/HpacSafety.Acceptance.Tests/SummarizationOutboxSteps.Sections.cs)

### REQ-AI-036

- [SummarizationOutboxSteps.Sections.cs](../tests/HpacSafety.Acceptance.Tests/SummarizationOutboxSteps.Sections.cs)
- [SummarizationOutboxSteps.cs](../tests/HpacSafety.Acceptance.Tests/SummarizationOutboxSteps.cs)

## Claims: comments

### REQ-COM-001

- [CommentSteps.cs](../tests/HpacSafety.Acceptance.Tests/CommentSteps.cs)

### REQ-COM-002

- [CommentSteps.cs](../tests/HpacSafety.Acceptance.Tests/CommentSteps.cs)

### REQ-COM-003

- [CommentSteps.cs](../tests/HpacSafety.Acceptance.Tests/CommentSteps.cs)

### REQ-COM-004

- [CommentSteps.cs](../tests/HpacSafety.Acceptance.Tests/CommentSteps.cs)

### REQ-COM-005

- [CommentSteps.cs](../tests/HpacSafety.Acceptance.Tests/CommentSteps.cs)

### REQ-COM-006

- [CommentSteps.cs](../tests/HpacSafety.Acceptance.Tests/CommentSteps.cs)

### REQ-COM-007

- [CommentSteps.cs](../tests/HpacSafety.Acceptance.Tests/CommentSteps.cs)

### REQ-COM-008

- [CommentSteps.cs](../tests/HpacSafety.Acceptance.Tests/CommentSteps.cs)

### REQ-COM-009

- [CommentSteps.cs](../tests/HpacSafety.Acceptance.Tests/CommentSteps.cs)

### REQ-COM-010

- [CommentSteps.cs](../tests/HpacSafety.Acceptance.Tests/CommentSteps.cs)

### REQ-COM-011

- [CommentSteps.cs](../tests/HpacSafety.Acceptance.Tests/CommentSteps.cs)

### REQ-COM-012

- [CommentSteps.cs](../tests/HpacSafety.Acceptance.Tests/CommentSteps.cs)

### REQ-COM-013

- [CommentSteps.cs](../tests/HpacSafety.Acceptance.Tests/CommentSteps.cs)

### REQ-COM-014

- [CommentSteps.cs](../tests/HpacSafety.Acceptance.Tests/CommentSteps.cs)

### REQ-COM-015

- [comments.steps.ts](../tests/e2e/steps/comments.steps.ts)

### REQ-COM-016

- [comments.steps.ts](../tests/e2e/steps/comments.steps.ts)

### REQ-COM-017

- [comments.steps.ts](../tests/e2e/steps/comments.steps.ts)

### REQ-COM-018

- [comments.steps.ts](../tests/e2e/steps/comments.steps.ts)
- [public-reports.steps.ts](../tests/e2e/steps/public-reports.steps.ts)

### REQ-COM-019

- [comments.steps.ts](../tests/e2e/steps/comments.steps.ts)

### REQ-COM-020

- [comments.steps.ts](../tests/e2e/steps/comments.steps.ts)

### REQ-COM-021

- [comments.steps.ts](../tests/e2e/steps/comments.steps.ts)
- [unsaved-changes.steps.ts](../tests/e2e/steps/unsaved-changes.steps.ts)

## Claims: domain-and-lifecycle

### REQ-DOM-001

- [ReviewLifecycleSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReviewLifecycleSteps.cs)

### REQ-DOM-003

- [PublicReportFeedSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicReportFeedSteps.cs)

### REQ-DOM-004

- [PublicReportFeedSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicReportFeedSteps.cs)

### REQ-DOM-005

- [ReviewLifecycleSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReviewLifecycleSteps.cs)

### REQ-DOM-006

- [SummarizationOutboxSteps.cs](../tests/HpacSafety.Acceptance.Tests/SummarizationOutboxSteps.cs)

### REQ-DOM-007

- [DomainAndLifecycleSteps.cs](../tests/HpacSafety.Acceptance.Tests/DomainAndLifecycleSteps.cs)

### REQ-DOM-008

- [DomainAndLifecycleSteps.cs](../tests/HpacSafety.Acceptance.Tests/DomainAndLifecycleSteps.cs)

### REQ-DOM-009

- [DomainAndLifecycleSteps.cs](../tests/HpacSafety.Acceptance.Tests/DomainAndLifecycleSteps.cs)

### REQ-DOM-010

- [DomainAndLifecycleSteps.cs](../tests/HpacSafety.Acceptance.Tests/DomainAndLifecycleSteps.cs)

### REQ-DOM-011

- Unbound: `Given a synthetic report with an attachment has been submitted`
- Unbound: `When a safety officer soft-deletes the report`
- Unbound: `Then the report row remains, stamped with a deleted timestamp`
- Unbound: `Then its answers, files, and stored objects remain`
- Unbound: `Then no application path removes them afterwards`

### REQ-DOM-013

- [AuditSteps.cs](../tests/HpacSafety.Acceptance.Tests/AuditSteps.cs)
- Unbound: `Given a question is created occurs`
- Unbound: `Then an audit log entry records the acting token subject and action metadata`
- Unbound: `Then the subject is an opaque string that joins to no user record`
- Unbound: `Then it never contains raw answers, names, credentials, tokens, or client filenames`
- Unbound: `Given a question is revised occurs`
- Unbound: `Given a question is deleted occurs`
- Unbound: `Given a question revision is deleted occurs`
- Unbound: `Given a report is deleted occurs`
- Unbound: `Given a summary is edited occurs`
- Unbound: `Given a summary is rolled back occurs`
- Unbound: `Given a report is published occurs`
- Unbound: `Given a report is unpublished occurs`

### REQ-DOM-014

- [ReviewLifecycleSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReviewLifecycleSteps.cs)

### REQ-DOM-015

- [ReviewLifecycleSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReviewLifecycleSteps.cs)

### REQ-DOM-016

- [DomainAndLifecycleSteps.cs](../tests/HpacSafety.Acceptance.Tests/DomainAndLifecycleSteps.cs)

### REQ-DOM-017

- [DomainAndLifecycleSteps.cs](../tests/HpacSafety.Acceptance.Tests/DomainAndLifecycleSteps.cs)

### REQ-DOM-018

- [ReporterImmutabilitySteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterImmutabilitySteps.cs)

### REQ-DOM-019

- [ReporterImmutabilitySteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterImmutabilitySteps.cs)

### REQ-DOM-020

- [ReporterImmutabilitySteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterImmutabilitySteps.cs)

### REQ-DOM-021

- [ReporterImmutabilitySteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterImmutabilitySteps.cs)

### REQ-DOM-022

- [ReporterImmutabilitySteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterImmutabilitySteps.cs)

### REQ-DOM-023

- [ReporterImmutabilitySteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterImmutabilitySteps.cs)

### REQ-DOM-024

- [ReporterImmutabilitySteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterImmutabilitySteps.cs)

### REQ-DOM-025

- [ReporterImmutabilitySteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterImmutabilitySteps.cs)

### REQ-DOM-026

- [ReporterImmutabilitySteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterImmutabilitySteps.cs)

### REQ-DOM-027

- [ReporterImmutabilitySteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterImmutabilitySteps.cs)

### REQ-DOM-028

- [ReporterImmutabilitySteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterImmutabilitySteps.cs)

### REQ-DOM-029

- [ReporterImmutabilitySteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterImmutabilitySteps.cs)

### REQ-DOM-030

- [ReporterImmutabilitySteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterImmutabilitySteps.cs)

## Claims: media

### REQ-MED-001

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)

### REQ-MED-002

- [AttachmentUploadSteps.cs](../tests/HpacSafety.Acceptance.Tests/AttachmentUploadSteps.cs)
- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)

### REQ-MED-003

- [AttachmentUploadSteps.cs](../tests/HpacSafety.Acceptance.Tests/AttachmentUploadSteps.cs)
- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)

### REQ-MED-005

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)
- Unbound: `Given an upload that no committed submission claimed`
- Unbound: `When the storage lifecycle rule runs`
- Unbound: `Then the upload expires, its key stopping resolving fifteen days after it was written and its bytes gone about a day after that`
- Unbound: `Then no file a committed submission claimed is expired by that rule`

### REQ-MED-006

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)
- [WorkerAttachmentSteps.cs](../tests/HpacSafety.Acceptance.Tests/WorkerAttachmentSteps.cs)

### REQ-MED-007

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)

### REQ-MED-008

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)
- [WorkerAttachmentSteps.cs](../tests/HpacSafety.Acceptance.Tests/WorkerAttachmentSteps.cs)

### REQ-MED-009

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)
- [WorkerAttachmentSteps.cs](../tests/HpacSafety.Acceptance.Tests/WorkerAttachmentSteps.cs)

### REQ-MED-010

- [AttachmentAccessSteps.cs](../tests/HpacSafety.Acceptance.Tests/AttachmentAccessSteps.cs)
- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)

### REQ-MED-011

- [AttachmentAccessSteps.cs](../tests/HpacSafety.Acceptance.Tests/AttachmentAccessSteps.cs)
- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)

### REQ-MED-012

- [public-media.steps.ts](../tests/e2e/steps/public-media.steps.ts)
- Unbound: `Given an authorized reviewer opens a document attachment`
- Unbound: `When the admin site presents it`
- Unbound: `Then the admin site does not embed, preview, or inline-render the document content`
- Unbound: `Then the document is offered only as a download`

### REQ-MED-013

- [AttachmentAccessSteps.cs](../tests/HpacSafety.Acceptance.Tests/AttachmentAccessSteps.cs)
- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)

### REQ-MED-015

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)

### REQ-MED-016

- [AttachmentUploadSteps.cs](../tests/HpacSafety.Acceptance.Tests/AttachmentUploadSteps.cs)
- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)

### REQ-MED-017

- [AttachmentUploadSteps.cs](../tests/HpacSafety.Acceptance.Tests/AttachmentUploadSteps.cs)
- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)

### REQ-MED-018

- [AttachmentUploadSteps.cs](../tests/HpacSafety.Acceptance.Tests/AttachmentUploadSteps.cs)
- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)

### REQ-MED-019

- [AttachmentUploadSteps.cs](../tests/HpacSafety.Acceptance.Tests/AttachmentUploadSteps.cs)
- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)

### REQ-MED-020

- [AttachmentAccessSteps.cs](../tests/HpacSafety.Acceptance.Tests/AttachmentAccessSteps.cs)
- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)

### REQ-MED-021

- [AttachmentAccessSteps.cs](../tests/HpacSafety.Acceptance.Tests/AttachmentAccessSteps.cs)
- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)

### REQ-MED-022

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)
- [WorkerAttachmentSteps.cs](../tests/HpacSafety.Acceptance.Tests/WorkerAttachmentSteps.cs)

### REQ-MED-023

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)
- [WorkerAttachmentSteps.cs](../tests/HpacSafety.Acceptance.Tests/WorkerAttachmentSteps.cs)

### REQ-MED-024

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)
- [WorkerAttachmentSteps.cs](../tests/HpacSafety.Acceptance.Tests/WorkerAttachmentSteps.cs)

### REQ-MED-025

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)
- [PublicMediaSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicMediaSteps.cs)

### REQ-MED-026

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)
- [PublicMediaSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicMediaSteps.cs)

### REQ-MED-027

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)
- [PublicMediaSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicMediaSteps.cs)

### REQ-MED-028

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)
- [PublicMediaSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicMediaSteps.cs)

### REQ-MED-029

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)
- [PublicMediaSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicMediaSteps.cs)

### REQ-MED-030

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)
- [PublicMediaSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicMediaSteps.cs)

### REQ-MED-031

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)
- [PublicMediaSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicMediaSteps.cs)

### REQ-MED-032

- [public-media.steps.ts](../tests/e2e/steps/public-media.steps.ts)

### REQ-MED-033

- [public-media.steps.ts](../tests/e2e/steps/public-media.steps.ts)

### REQ-MED-034

- [public-media.steps.ts](../tests/e2e/steps/public-media.steps.ts)

### REQ-MED-035

- [comments.steps.ts](../tests/e2e/steps/comments.steps.ts)
- [public-media.steps.ts](../tests/e2e/steps/public-media.steps.ts)

### REQ-MED-036

- [public-media.steps.ts](../tests/e2e/steps/public-media.steps.ts)

### REQ-MED-037

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)
- [PublicMediaSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicMediaSteps.cs)

### REQ-MED-038

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)
- [PublicMediaSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicMediaSteps.cs)

### REQ-MED-039

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)
- [PublicMediaSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicMediaSteps.cs)

### REQ-MED-040

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)
- [PublicMediaSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicMediaSteps.cs)

### REQ-MED-041

- [public-media.steps.ts](../tests/e2e/steps/public-media.steps.ts)

### REQ-MED-042

- [public-media.steps.ts](../tests/e2e/steps/public-media.steps.ts)

### REQ-MED-043

- [AttachmentAccessSteps.cs](../tests/HpacSafety.Acceptance.Tests/AttachmentAccessSteps.cs)
- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)

### REQ-MED-044

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)
- [PublicMediaSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicMediaSteps.cs)

### REQ-MED-045

- [AttachmentUploadSteps.cs](../tests/HpacSafety.Acceptance.Tests/AttachmentUploadSteps.cs)
- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)

### REQ-MED-046

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)
- [PrivateAttachmentSteps.cs](../tests/HpacSafety.Acceptance.Tests/PrivateAttachmentSteps.cs)

### REQ-MED-047

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)
- [PrivateAttachmentSteps.cs](../tests/HpacSafety.Acceptance.Tests/PrivateAttachmentSteps.cs)

### REQ-MED-048

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)
- [PrivateAttachmentSteps.cs](../tests/HpacSafety.Acceptance.Tests/PrivateAttachmentSteps.cs)

### REQ-MED-049

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)
- [PrivateAttachmentSteps.cs](../tests/HpacSafety.Acceptance.Tests/PrivateAttachmentSteps.cs)

### REQ-MED-050

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)
- [PrivateAttachmentSteps.cs](../tests/HpacSafety.Acceptance.Tests/PrivateAttachmentSteps.cs)

### REQ-MED-051

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)
- [PrivateAttachmentSteps.cs](../tests/HpacSafety.Acceptance.Tests/PrivateAttachmentSteps.cs)

### REQ-MED-052

- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)
- [PrivateAttachmentSteps.cs](../tests/HpacSafety.Acceptance.Tests/PrivateAttachmentSteps.cs)

### REQ-MED-053

- [AttachmentAccessSteps.cs](../tests/HpacSafety.Acceptance.Tests/AttachmentAccessSteps.cs)
- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)

### REQ-MED-054

- [AttachmentAccessSteps.cs](../tests/HpacSafety.Acceptance.Tests/AttachmentAccessSteps.cs)
- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)

### REQ-MED-055

- [AttachmentAccessSteps.cs](../tests/HpacSafety.Acceptance.Tests/AttachmentAccessSteps.cs)
- [MediaValidationSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaValidationSteps.cs)

### REQ-MED-056

- [public-media.steps.ts](../tests/e2e/steps/public-media.steps.ts)

### REQ-MED-057

- [public-media.steps.ts](../tests/e2e/steps/public-media.steps.ts)

### REQ-MED-058

- [public-media.steps.ts](../tests/e2e/steps/public-media.steps.ts)

### REQ-MED-059

- [public-media.steps.ts](../tests/e2e/steps/public-media.steps.ts)

### REQ-MED-060

- [public-media.steps.ts](../tests/e2e/steps/public-media.steps.ts)

### REQ-MED-061

- [public-media.steps.ts](../tests/e2e/steps/public-media.steps.ts)

## Claims: moderation-authentication-and-publication

### REQ-MOD-001

- [member-login.steps.ts](../tests/e2e/steps/member-login.steps.ts)

### REQ-MOD-002

- [member-login.steps.ts](../tests/e2e/steps/member-login.steps.ts)

### REQ-MOD-003

- [member-login.steps.ts](../tests/e2e/steps/member-login.steps.ts)

### REQ-MOD-004

- [member-login.steps.ts](../tests/e2e/steps/member-login.steps.ts)

### REQ-MOD-005

- [MembersSiteLoginSteps.cs](../tests/HpacSafety.Acceptance.Tests/MembersSiteLoginSteps.cs)

### REQ-MOD-006

- [member-login.steps.ts](../tests/e2e/steps/member-login.steps.ts)

### REQ-MOD-007

- [member-login.steps.ts](../tests/e2e/steps/member-login.steps.ts)

### REQ-MOD-009

- [member-login.steps.ts](../tests/e2e/steps/member-login.steps.ts)

### REQ-MOD-010

- [member-login.steps.ts](../tests/e2e/steps/member-login.steps.ts)

### REQ-MOD-011

- [member-login.steps.ts](../tests/e2e/steps/member-login.steps.ts)

### REQ-MOD-012

- [homepage.steps.ts](../tests/e2e/steps/homepage.steps.ts)
- [member-login.steps.ts](../tests/e2e/steps/member-login.steps.ts)

### REQ-MOD-013

- [AuthenticationSteps.cs](../tests/HpacSafety.Acceptance.Tests/AuthenticationSteps.cs)

### REQ-MOD-014

- [AuthenticationSteps.cs](../tests/HpacSafety.Acceptance.Tests/AuthenticationSteps.cs)

### REQ-MOD-015

- [AuthenticationSteps.cs](../tests/HpacSafety.Acceptance.Tests/AuthenticationSteps.cs)

### REQ-MOD-016

- [AuthenticationSteps.cs](../tests/HpacSafety.Acceptance.Tests/AuthenticationSteps.cs)

### REQ-MOD-017

- [AuthenticationSteps.cs](../tests/HpacSafety.Acceptance.Tests/AuthenticationSteps.cs)

### REQ-MOD-018

- [AuthenticationSteps.cs](../tests/HpacSafety.Acceptance.Tests/AuthenticationSteps.cs)

### REQ-MOD-019

- [AuthorizationSteps.cs](../tests/HpacSafety.Acceptance.Tests/AuthorizationSteps.cs)

### REQ-MOD-020

- [MembersSiteLoginSteps.cs](../tests/HpacSafety.Acceptance.Tests/MembersSiteLoginSteps.cs)

### REQ-MOD-021

- [MembersSiteLoginSteps.cs](../tests/HpacSafety.Acceptance.Tests/MembersSiteLoginSteps.cs)

### REQ-MOD-022

- [MembersSiteLoginSteps.cs](../tests/HpacSafety.Acceptance.Tests/MembersSiteLoginSteps.cs)

### REQ-MOD-023

- [AuthorizationSteps.cs](../tests/HpacSafety.Acceptance.Tests/AuthorizationSteps.cs)

### REQ-MOD-024

- [AuthorizationSteps.cs](../tests/HpacSafety.Acceptance.Tests/AuthorizationSteps.cs)

### REQ-MOD-025

- [AuthorizationSteps.cs](../tests/HpacSafety.Acceptance.Tests/AuthorizationSteps.cs)

### REQ-MOD-026

- [AuthorizationSteps.cs](../tests/HpacSafety.Acceptance.Tests/AuthorizationSteps.cs)

### REQ-MOD-027

- [AuthorizationSteps.cs](../tests/HpacSafety.Acceptance.Tests/AuthorizationSteps.cs)

### REQ-MOD-028

- [AuthorizationSteps.cs](../tests/HpacSafety.Acceptance.Tests/AuthorizationSteps.cs)

### REQ-MOD-029

- [AuditSteps.cs](../tests/HpacSafety.Acceptance.Tests/AuditSteps.cs)

### REQ-MOD-030

- [ReportReviewSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportReviewSteps.cs)

### REQ-MOD-031

- [ReportReviewSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportReviewSteps.cs)

### REQ-MOD-032

- [ReviewActionSteps.SummaryRevisions.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.SummaryRevisions.cs)
- [ReviewActionSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.cs)

### REQ-MOD-033

- [ReviewActionSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.cs)

### REQ-MOD-035

- [ReviewActionSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.cs)

### REQ-MOD-036

- [PublicReportFeedSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicReportFeedSteps.cs)
- [ReviewActionSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.cs)

### REQ-MOD-037

- [PublicReportFeedSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicReportFeedSteps.cs)

### REQ-MOD-038

- [PublicReportFeedSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicReportFeedSteps.cs)

### REQ-MOD-042

- [admin-route-guard.steps.ts](../tests/e2e/steps/admin-route-guard.steps.ts)

### REQ-MOD-043

- [admin-route-guard.steps.ts](../tests/e2e/steps/admin-route-guard.steps.ts)
- [member-login.steps.ts](../tests/e2e/steps/member-login.steps.ts)

### REQ-MOD-044

- [AuditSteps.cs](../tests/HpacSafety.Acceptance.Tests/AuditSteps.cs)

### REQ-MOD-045

- [AuditSteps.cs](../tests/HpacSafety.Acceptance.Tests/AuditSteps.cs)

### REQ-MOD-046

- [AttachmentAccessSteps.cs](../tests/HpacSafety.Acceptance.Tests/AttachmentAccessSteps.cs)

### REQ-MOD-047

- Unbound: `Given an administrator or reviewer performs an action that must be audited`
- Unbound: `When the audit row fails to write`
- Unbound: `Then the action itself does not commit`
- Unbound: `Then the caller sees the action as failed, not succeeded`

### REQ-MOD-048

- [member-login.steps.ts](../tests/e2e/steps/member-login.steps.ts)

### REQ-MOD-049

- [ReportReviewSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportReviewSteps.cs)

### REQ-MOD-050

- [ReportReviewSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportReviewSteps.cs)

### REQ-MOD-051

- [ReportReviewSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportReviewSteps.cs)

### REQ-MOD-052

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-053

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-054

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-055

- [ReviewActionSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.cs)

### REQ-MOD-057

- [ReviewActionSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.cs)

### REQ-MOD-058

- [ReviewActionSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.cs)

### REQ-MOD-059

- [ReviewActionSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.cs)

### REQ-MOD-060

- [ReviewActionSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.cs)

### REQ-MOD-061

- [ReviewActionSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.cs)

### REQ-MOD-062

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-063

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-064

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-065

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-066

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-067

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-068

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-069

- [ReviewActionSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.cs)

### REQ-MOD-070

- [ReviewActionSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.cs)

### REQ-MOD-071

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-072

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-073

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-074

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-075

- [locale.steps.ts](../tests/e2e/steps/locale.steps.ts)
- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-076

- [locale.steps.ts](../tests/e2e/steps/locale.steps.ts)
- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-077

- [AnswerTranslationModeSteps.cs](../tests/HpacSafety.Acceptance.Tests/AnswerTranslationModeSteps.cs)

### REQ-MOD-078

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-079

- [public-reports.steps.ts](../tests/e2e/steps/public-reports.steps.ts)

### REQ-MOD-080

- [member-login.steps.ts](../tests/e2e/steps/member-login.steps.ts)
- [public-reports.steps.ts](../tests/e2e/steps/public-reports.steps.ts)

### REQ-MOD-081

- [public-reports.steps.ts](../tests/e2e/steps/public-reports.steps.ts)

### REQ-MOD-082

- [public-reports.steps.ts](../tests/e2e/steps/public-reports.steps.ts)

### REQ-MOD-083

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-084

- [PendingCountSteps.cs](../tests/HpacSafety.Acceptance.Tests/PendingCountSteps.cs)
- [ReportReviewSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportReviewSteps.cs)

### REQ-MOD-085

- [PendingCountSteps.cs](../tests/HpacSafety.Acceptance.Tests/PendingCountSteps.cs)

### REQ-MOD-086

- [PendingCountSteps.cs](../tests/HpacSafety.Acceptance.Tests/PendingCountSteps.cs)

### REQ-MOD-087

- [admin-pending-counts.steps.ts](../tests/e2e/steps/admin-pending-counts.steps.ts)
- [member-login.steps.ts](../tests/e2e/steps/member-login.steps.ts)

### REQ-MOD-089

- [admin-pending-counts.steps.ts](../tests/e2e/steps/admin-pending-counts.steps.ts)
- [member-login.steps.ts](../tests/e2e/steps/member-login.steps.ts)

### REQ-MOD-090

- [ReviewActionSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.cs)

### REQ-MOD-091

- [AuditSteps.cs](../tests/HpacSafety.Acceptance.Tests/AuditSteps.cs)

### REQ-MOD-092

- [member-login.steps.ts](../tests/e2e/steps/member-login.steps.ts)

### REQ-MOD-093

- [admin-pending-counts.steps.ts](../tests/e2e/steps/admin-pending-counts.steps.ts)
- [member-login.steps.ts](../tests/e2e/steps/member-login.steps.ts)

### REQ-MOD-094

- [AuthorizationSteps.cs](../tests/HpacSafety.Acceptance.Tests/AuthorizationSteps.cs)

### REQ-MOD-095

- [review-type-ahead-values.steps.ts](../tests/e2e/steps/review-type-ahead-values.steps.ts)

### REQ-MOD-096

- [ConsentViewSteps.cs](../tests/HpacSafety.Acceptance.Tests/ConsentViewSteps.cs)

### REQ-MOD-097

- [review-type-ahead-values.steps.ts](../tests/e2e/steps/review-type-ahead-values.steps.ts)

### REQ-MOD-098

- [PrivateNoteSteps.cs](../tests/HpacSafety.Acceptance.Tests/PrivateNoteSteps.cs)

### REQ-MOD-099

- [PrivateNoteSteps.cs](../tests/HpacSafety.Acceptance.Tests/PrivateNoteSteps.cs)

### REQ-MOD-100

- [PrivateNoteSteps.cs](../tests/HpacSafety.Acceptance.Tests/PrivateNoteSteps.cs)

### REQ-MOD-101

- [PrivateNoteSteps.cs](../tests/HpacSafety.Acceptance.Tests/PrivateNoteSteps.cs)

### REQ-MOD-102

- [PrivateNoteSteps.cs](../tests/HpacSafety.Acceptance.Tests/PrivateNoteSteps.cs)

### REQ-MOD-103

- [PrivateNoteSteps.cs](../tests/HpacSafety.Acceptance.Tests/PrivateNoteSteps.cs)

### REQ-MOD-104

- [PrivateNoteSteps.cs](../tests/HpacSafety.Acceptance.Tests/PrivateNoteSteps.cs)

### REQ-MOD-105

- [SummarizationOutboxSteps.cs](../tests/HpacSafety.Acceptance.Tests/SummarizationOutboxSteps.cs)

### REQ-MOD-106

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)
- [private-notes.steps.ts](../tests/e2e/steps/private-notes.steps.ts)

### REQ-MOD-107

- [PrivateAttachmentSteps.cs](../tests/HpacSafety.Acceptance.Tests/PrivateAttachmentSteps.cs)

### REQ-MOD-108

- [PrivateAttachmentSteps.cs](../tests/HpacSafety.Acceptance.Tests/PrivateAttachmentSteps.cs)

### REQ-MOD-109

- [PrivateAttachmentSteps.cs](../tests/HpacSafety.Acceptance.Tests/PrivateAttachmentSteps.cs)

### REQ-MOD-110

- [PrivateAttachmentSteps.cs](../tests/HpacSafety.Acceptance.Tests/PrivateAttachmentSteps.cs)

### REQ-MOD-111

- [PrivateAttachmentSteps.cs](../tests/HpacSafety.Acceptance.Tests/PrivateAttachmentSteps.cs)

### REQ-MOD-112

- [PrivateAttachmentSteps.cs](../tests/HpacSafety.Acceptance.Tests/PrivateAttachmentSteps.cs)

### REQ-MOD-113

- [SummarizationOutboxSteps.cs](../tests/HpacSafety.Acceptance.Tests/SummarizationOutboxSteps.cs)

### REQ-MOD-114

- [PrivateAttachmentSteps.cs](../tests/HpacSafety.Acceptance.Tests/PrivateAttachmentSteps.cs)

### REQ-MOD-115

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)
- [private-attachments.steps.ts](../tests/e2e/steps/private-attachments.steps.ts)

### REQ-MOD-116

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)
- [private-attachments.steps.ts](../tests/e2e/steps/private-attachments.steps.ts)

### REQ-MOD-117

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)
- [private-attachments.steps.ts](../tests/e2e/steps/private-attachments.steps.ts)

### REQ-MOD-118

- [locale.steps.ts](../tests/e2e/steps/locale.steps.ts)
- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-119

- [ReportReviewSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportReviewSteps.cs)

### REQ-MOD-120

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-121

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-122

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-123

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-124

- [ReportReviewSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportReviewSteps.cs)

### REQ-MOD-125

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-126

- [public-reports.steps.ts](../tests/e2e/steps/public-reports.steps.ts)

### REQ-MOD-127

- [public-reports.steps.ts](../tests/e2e/steps/public-reports.steps.ts)

### REQ-MOD-128

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)
- [public-reports.steps.ts](../tests/e2e/steps/public-reports.steps.ts)

### REQ-MOD-129

- [ReportReviewSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportReviewSteps.cs)

### REQ-MOD-130

- [AdminSearchSteps.cs](../tests/HpacSafety.Acceptance.Tests/AdminSearchSteps.cs)

### REQ-MOD-131

- [AdminSearchSteps.cs](../tests/HpacSafety.Acceptance.Tests/AdminSearchSteps.cs)

### REQ-MOD-132

- [AdminSearchSteps.cs](../tests/HpacSafety.Acceptance.Tests/AdminSearchSteps.cs)

### REQ-MOD-133

- [AdminSearchSteps.cs](../tests/HpacSafety.Acceptance.Tests/AdminSearchSteps.cs)

### REQ-MOD-134

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-135

- [AdminSearchSteps.cs](../tests/HpacSafety.Acceptance.Tests/AdminSearchSteps.cs)

### REQ-MOD-136

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-137

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-138

- [AdminSearchSteps.cs](../tests/HpacSafety.Acceptance.Tests/AdminSearchSteps.cs)

### REQ-MOD-139

- [AdminSearchSteps.cs](../tests/HpacSafety.Acceptance.Tests/AdminSearchSteps.cs)

### REQ-MOD-140

- [PublicSearchSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicSearchSteps.cs)

### REQ-MOD-141

- [PublicSearchSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicSearchSteps.cs)

### REQ-MOD-142

- [PublicSearchSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicSearchSteps.cs)

### REQ-MOD-143

- [PublicSearchSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicSearchSteps.cs)

### REQ-MOD-144

- [PublicSearchSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicSearchSteps.cs)

### REQ-MOD-145

- [PublicSearchSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicSearchSteps.cs)

### REQ-MOD-146

- [PublicSearchSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicSearchSteps.cs)

### REQ-MOD-147

- [PublicSearchSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicSearchSteps.cs)

### REQ-MOD-148

- [PublicReportFeedSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicReportFeedSteps.cs)
- [PublicSearchSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicSearchSteps.cs)

### REQ-MOD-149

- [comments.steps.ts](../tests/e2e/steps/comments.steps.ts)
- [member-login.steps.ts](../tests/e2e/steps/member-login.steps.ts)
- [public-reports.steps.ts](../tests/e2e/steps/public-reports.steps.ts)

### REQ-MOD-150

- [PublicReportFeedSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicReportFeedSteps.cs)

### REQ-MOD-151

- [ReportReviewSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportReviewSteps.cs)

### REQ-MOD-152

- [PublicReportFeedSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicReportFeedSteps.cs)

### REQ-MOD-153

- [public-reports.steps.ts](../tests/e2e/steps/public-reports.steps.ts)

### REQ-MOD-154

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-155

- [PublicReportFeedSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicReportFeedSteps.cs)

### REQ-MOD-156

- [AuthorizationSteps.cs](../tests/HpacSafety.Acceptance.Tests/AuthorizationSteps.cs)

### REQ-MOD-157

- [InterimIssuerSteps.cs](../tests/HpacSafety.Acceptance.Tests/InterimIssuerSteps.cs)

### REQ-MOD-158

- [InterimIssuerSteps.cs](../tests/HpacSafety.Acceptance.Tests/InterimIssuerSteps.cs)

### REQ-MOD-159

- [InterimIssuerSteps.cs](../tests/HpacSafety.Acceptance.Tests/InterimIssuerSteps.cs)

### REQ-MOD-160

- [review-type-ahead-values.steps.ts](../tests/e2e/steps/review-type-ahead-values.steps.ts)

### REQ-MOD-161

- [review-type-ahead-values.steps.ts](../tests/e2e/steps/review-type-ahead-values.steps.ts)

### REQ-MOD-162

- [review-type-ahead-values.steps.ts](../tests/e2e/steps/review-type-ahead-values.steps.ts)

### REQ-MOD-163

- [review-type-ahead-values.steps.ts](../tests/e2e/steps/review-type-ahead-values.steps.ts)

### REQ-MOD-164

- [public-reports.steps.ts](../tests/e2e/steps/public-reports.steps.ts)

### REQ-MOD-165

- [public-reports.steps.ts](../tests/e2e/steps/public-reports.steps.ts)

### REQ-MOD-166

- [review-type-ahead-values.steps.ts](../tests/e2e/steps/review-type-ahead-values.steps.ts)

### REQ-MOD-167

- [review-type-ahead-values.steps.ts](../tests/e2e/steps/review-type-ahead-values.steps.ts)

### REQ-MOD-168

- [review-type-ahead-values.steps.ts](../tests/e2e/steps/review-type-ahead-values.steps.ts)

### REQ-MOD-169

- [review-type-ahead-values.steps.ts](../tests/e2e/steps/review-type-ahead-values.steps.ts)

### REQ-MOD-170

- [review-type-ahead-values.steps.ts](../tests/e2e/steps/review-type-ahead-values.steps.ts)

### REQ-MOD-171

- [review-type-ahead-values.steps.ts](../tests/e2e/steps/review-type-ahead-values.steps.ts)

### REQ-MOD-172

- [review-type-ahead-values.steps.ts](../tests/e2e/steps/review-type-ahead-values.steps.ts)

### REQ-MOD-173

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)
- [private-attachments.steps.ts](../tests/e2e/steps/private-attachments.steps.ts)

### REQ-MOD-174

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)
- [private-attachments.steps.ts](../tests/e2e/steps/private-attachments.steps.ts)

### REQ-MOD-175

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)
- [private-attachments.steps.ts](../tests/e2e/steps/private-attachments.steps.ts)

### REQ-MOD-176

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)
- [private-attachments.steps.ts](../tests/e2e/steps/private-attachments.steps.ts)

### REQ-MOD-177

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)
- [private-attachments.steps.ts](../tests/e2e/steps/private-attachments.steps.ts)
- [unsaved-changes.steps.ts](../tests/e2e/steps/unsaved-changes.steps.ts)

### REQ-MOD-178

- [public-reports.steps.ts](../tests/e2e/steps/public-reports.steps.ts)

### REQ-MOD-179

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-180

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)
- [private-attachments.steps.ts](../tests/e2e/steps/private-attachments.steps.ts)

### REQ-MOD-181

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)
- [private-attachments.steps.ts](../tests/e2e/steps/private-attachments.steps.ts)

### REQ-MOD-184

- [admin-route-guard.steps.ts](../tests/e2e/steps/admin-route-guard.steps.ts)
- [member-login.steps.ts](../tests/e2e/steps/member-login.steps.ts)

### REQ-MOD-185

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)
- [unsaved-changes.steps.ts](../tests/e2e/steps/unsaved-changes.steps.ts)

### REQ-MOD-186

- [review-type-ahead-values.steps.ts](../tests/e2e/steps/review-type-ahead-values.steps.ts)
- [unsaved-changes.steps.ts](../tests/e2e/steps/unsaved-changes.steps.ts)

### REQ-MOD-187

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)
- [unsaved-changes.steps.ts](../tests/e2e/steps/unsaved-changes.steps.ts)

### REQ-MOD-190

- [public-reports.steps.ts](../tests/e2e/steps/public-reports.steps.ts)

### REQ-MOD-191

- [public-reports.steps.ts](../tests/e2e/steps/public-reports.steps.ts)

### REQ-MOD-192

- [public-reports.steps.ts](../tests/e2e/steps/public-reports.steps.ts)

### REQ-MOD-193

- [PublicReportFeedSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicReportFeedSteps.cs)

### REQ-MOD-194

- [ReviewActionSteps.SummaryRevisions.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.SummaryRevisions.cs)
- [ReviewActionSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.cs)

### REQ-MOD-195

- [ReviewActionSteps.SummaryRevisions.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.SummaryRevisions.cs)
- [ReviewActionSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.cs)

### REQ-MOD-196

- [ReviewActionSteps.SummaryRevisions.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.SummaryRevisions.cs)
- [ReviewActionSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.cs)

### REQ-MOD-197

- [ReviewActionSteps.SummaryRevisions.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.SummaryRevisions.cs)
- [ReviewActionSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.cs)

### REQ-MOD-198

- [ReviewActionSteps.SummaryRevisions.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.SummaryRevisions.cs)
- [ReviewActionSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.cs)

### REQ-MOD-199

- [ReviewActionSteps.SummaryRevisions.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.SummaryRevisions.cs)
- [ReviewActionSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.cs)

### REQ-MOD-200

- [ReviewActionSteps.SummaryRevisions.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.SummaryRevisions.cs)
- [ReviewActionSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.cs)

### REQ-MOD-201

- [ReviewActionSteps.SummaryRevisions.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.SummaryRevisions.cs)
- [ReviewActionSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.cs)

### REQ-MOD-202

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-203

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-204

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-205

- [ReviewActionSteps.SummaryRevisions.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.SummaryRevisions.cs)
- [ReviewActionSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReviewActionSteps.cs)

### REQ-MOD-206

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-207

- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-MOD-208

- [summary-markdown.steps.ts](../tests/e2e/steps/summary-markdown.steps.ts)

### REQ-MOD-209

- [summary-markdown.steps.ts](../tests/e2e/steps/summary-markdown.steps.ts)

### REQ-MOD-210

- [public-reports.steps.ts](../tests/e2e/steps/public-reports.steps.ts)
- [summary-markdown.steps.ts](../tests/e2e/steps/summary-markdown.steps.ts)

### REQ-MOD-211

- [summary-markdown.steps.ts](../tests/e2e/steps/summary-markdown.steps.ts)

## Claims: question-bank-and-form

### REQ-QB-001

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [QuestionForkSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionForkSteps.cs)

### REQ-QB-002

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [QuestionForkSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionForkSteps.cs)
- [ReporterAddedChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterAddedChoiceSteps.cs)

### REQ-QB-003

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [QuestionForkEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionForkEndpointSteps.cs)
- [QuestionForkSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionForkSteps.cs)

### REQ-QB-004

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-005

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [QuestionForkEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionForkEndpointSteps.cs)

### REQ-QB-006

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [QuestionForkSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionForkSteps.cs)
- [ReporterAddedChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterAddedChoiceSteps.cs)

### REQ-QB-008

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [QuestionForkEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionForkEndpointSteps.cs)

### REQ-QB-009

- [PublicQuestionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicQuestionEndpointSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-010

- [PublicQuestionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicQuestionEndpointSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-011

- [PublicQuestionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicQuestionEndpointSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-012

- [PublicQuestionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicQuestionEndpointSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-013

- [PublicQuestionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicQuestionEndpointSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-014

- [MediaConsentSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaConsentSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-015

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-016

- [MediaConsentSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaConsentSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-019

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [StoredAnswerSteps.cs](../tests/HpacSafety.Acceptance.Tests/StoredAnswerSteps.cs)

### REQ-QB-025

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [StoredAnswerSteps.cs](../tests/HpacSafety.Acceptance.Tests/StoredAnswerSteps.cs)

### REQ-QB-026

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [StoredAnswerSteps.cs](../tests/HpacSafety.Acceptance.Tests/StoredAnswerSteps.cs)
- Unbound: `Given an answer is created against a private question revision`
- Unbound: `Then it stores the exact revision identifier and a privacy snapshot`
- Unbound: `Then the answer is available only to authorized admin flows and to the Worker as labeled recognition context`
- Unbound: `Then it never becomes public content`

### REQ-QB-027

- [QuestionBankInvariantSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankInvariantSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-030

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-031

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-036

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [ReporterAddedChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterAddedChoiceSteps.cs)

### REQ-QB-044

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-045

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-QB-046

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-047

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [question-bank-and-form.steps.ts](../tests/e2e/steps/question-bank-and-form.steps.ts)

### REQ-QB-048

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-049

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-050

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-051

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-052

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- Unbound: `Given a question is grouped under a group question`
- Unbound: `When an Administrator deletes the group`
- Unbound: `Then the grouped question gets a new revision that is ungrouped`
- Unbound: `Then it appears on the reporter's form where the group stood`
- Unbound: `When an Administrator retypes the group to a type other than group`

### REQ-QB-053

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-054

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-055

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-056

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-057

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-058

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-059

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-060

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-061

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-062

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-063

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-066

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [QuestionTranslationSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionTranslationSteps.cs)

### REQ-QB-067

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [QuestionTranslationSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionTranslationSteps.cs)

### REQ-QB-069

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-070

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-071

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-072

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-074

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-075

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-076

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-077

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-078

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-079

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-080

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-081

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-082

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-085

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-086

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-087

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-088

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [typeform-import.steps.ts](../tests/e2e/steps/typeform-import.steps.ts)

### REQ-QB-089

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [typeform-import.steps.ts](../tests/e2e/steps/typeform-import.steps.ts)

### REQ-QB-090

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-092

- [ChoiceCodeEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ChoiceCodeEndpointSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-093

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-096

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [QuestionKeyEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionKeyEndpointSteps.cs)

### REQ-QB-097

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [ReporterAddedChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterAddedChoiceSteps.cs)

### REQ-QB-098

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [ReporterAddedChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterAddedChoiceSteps.cs)

### REQ-QB-101

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [ReporterAddedChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterAddedChoiceSteps.cs)

### REQ-QB-103

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)

### REQ-QB-104

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [SeededWordingSteps.cs](../tests/HpacSafety.Acceptance.Tests/SeededWordingSteps.cs)

### REQ-QB-105

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [SeededWordingSteps.cs](../tests/HpacSafety.Acceptance.Tests/SeededWordingSteps.cs)

### REQ-QB-106

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [SeededWordingSteps.cs](../tests/HpacSafety.Acceptance.Tests/SeededWordingSteps.cs)

### REQ-QB-107

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [SeededWordingSteps.cs](../tests/HpacSafety.Acceptance.Tests/SeededWordingSteps.cs)

### REQ-QB-108

- [AnswerTranslationModeSteps.cs](../tests/HpacSafety.Acceptance.Tests/AnswerTranslationModeSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-109

- [AnswerTranslationModeSteps.cs](../tests/HpacSafety.Acceptance.Tests/AnswerTranslationModeSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-110

- [AnswerTranslationModeSteps.cs](../tests/HpacSafety.Acceptance.Tests/AnswerTranslationModeSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-111

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-112

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-113

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [public-media.steps.ts](../tests/e2e/steps/public-media.steps.ts)

### REQ-QB-114

- [MediaConsentSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaConsentSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-115

- [MediaConsentSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaConsentSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-116

- [MediaConsentSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaConsentSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-117

- [MediaConsentSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaConsentSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-118

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [StoredAnswerSteps.cs](../tests/HpacSafety.Acceptance.Tests/StoredAnswerSteps.cs)

### REQ-QB-119

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [StoredAnswerSteps.cs](../tests/HpacSafety.Acceptance.Tests/StoredAnswerSteps.cs)

### REQ-QB-120

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [YesNoLanguageSteps.cs](../tests/HpacSafety.Acceptance.Tests/YesNoLanguageSteps.cs)

### REQ-QB-121

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [YesNoLanguageSteps.cs](../tests/HpacSafety.Acceptance.Tests/YesNoLanguageSteps.cs)

### REQ-QB-122

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [StoredAnswerSteps.cs](../tests/HpacSafety.Acceptance.Tests/StoredAnswerSteps.cs)

### REQ-QB-123

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [ReporterAddedChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterAddedChoiceSteps.cs)

### REQ-QB-124

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [ReporterAddedChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterAddedChoiceSteps.cs)

### REQ-QB-125

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [ReporterAddedChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterAddedChoiceSteps.cs)

### REQ-QB-126

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [ReporterAddedChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterAddedChoiceSteps.cs)

### REQ-QB-127

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [ReporterAddedChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterAddedChoiceSteps.cs)

### REQ-QB-128

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [ReporterAddedChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterAddedChoiceSteps.cs)

### REQ-QB-129

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [ReporterAddedChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterAddedChoiceSteps.cs)

### REQ-QB-130

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [ReporterAddedChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterAddedChoiceSteps.cs)

### REQ-QB-131

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [ReporterAddedChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterAddedChoiceSteps.cs)

### REQ-QB-132

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [ReporterAddedChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterAddedChoiceSteps.cs)

### REQ-QB-133

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [ReporterAddedChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterAddedChoiceSteps.cs)

### REQ-QB-134

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [ReporterValueTranslationSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterValueTranslationSteps.cs)

### REQ-QB-135

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [ReporterAddedChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReporterAddedChoiceSteps.cs)

### REQ-QB-136

- [ChoiceReferenceMigrationSteps.cs](../tests/HpacSafety.Acceptance.Tests/ChoiceReferenceMigrationSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-137

- [BooleanAnswerMigrationSteps.cs](../tests/HpacSafety.Acceptance.Tests/BooleanAnswerMigrationSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-138

- [BooleanAnswerMigrationSteps.cs](../tests/HpacSafety.Acceptance.Tests/BooleanAnswerMigrationSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-139

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-140

- [ForkedParentConditionSteps.cs](../tests/HpacSafety.Acceptance.Tests/ForkedParentConditionSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-141

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-142

- [PublicQuestionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/PublicQuestionEndpointSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-143

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [question-bank-and-form.steps.ts](../tests/e2e/steps/question-bank-and-form.steps.ts)

### REQ-QB-144

- [ChoicePinSteps.cs](../tests/HpacSafety.Acceptance.Tests/ChoicePinSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-145

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)

### REQ-QB-146

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)

### REQ-QB-147

- [ChoicePinSteps.cs](../tests/HpacSafety.Acceptance.Tests/ChoicePinSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-148

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)

### REQ-QB-149

- [ChoicePinSteps.cs](../tests/HpacSafety.Acceptance.Tests/ChoicePinSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-150

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-151

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-152

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [review-type-ahead-values.steps.ts](../tests/e2e/steps/review-type-ahead-values.steps.ts)

### REQ-QB-153

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [manage-reports.steps.ts](../tests/e2e/steps/manage-reports.steps.ts)

### REQ-QB-154

- [FutureDateSteps.cs](../tests/HpacSafety.Acceptance.Tests/FutureDateSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-155

- [FutureDateSteps.cs](../tests/HpacSafety.Acceptance.Tests/FutureDateSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-156

- [FutureDateSteps.cs](../tests/HpacSafety.Acceptance.Tests/FutureDateSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-157

- [FutureDateSteps.cs](../tests/HpacSafety.Acceptance.Tests/FutureDateSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-158

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-159

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)

### REQ-QB-160

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)

### REQ-QB-161

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)

### REQ-QB-162

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)

### REQ-QB-163

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)

### REQ-QB-164

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-165

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-166

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-167

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-168

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-169

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-170

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-171

- [choice-answers.steps.ts](../tests/e2e/steps/choice-answers.steps.ts)
- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)

### REQ-QB-172

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-173

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-174

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-175

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-176

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-177

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-178

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-179

- [DependentChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/DependentChoiceSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-180

- [DependentChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/DependentChoiceSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-181

- [DependentChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/DependentChoiceSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-184

- [DependentChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/DependentChoiceSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-185

- [DependentChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/DependentChoiceSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-187

- [DependentChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/DependentChoiceSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-188

- [DependentChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/DependentChoiceSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-189

- [DependentChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/DependentChoiceSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-190

- [DependentChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/DependentChoiceSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-191

- [DependentChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/DependentChoiceSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-192

- [DependentChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/DependentChoiceSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-195

- [dependent-choices.steps.ts](../tests/e2e/steps/dependent-choices.steps.ts)
- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-197

- [dependent-choices.steps.ts](../tests/e2e/steps/dependent-choices.steps.ts)
- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-198

- [dependent-choices.steps.ts](../tests/e2e/steps/dependent-choices.steps.ts)
- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-199

- [dependent-choices.steps.ts](../tests/e2e/steps/dependent-choices.steps.ts)
- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-200

- [dependent-choices.steps.ts](../tests/e2e/steps/dependent-choices.steps.ts)
- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-201

- [dependent-choices.steps.ts](../tests/e2e/steps/dependent-choices.steps.ts)
- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-203

- [DependentChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/DependentChoiceSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-204

- [dependent-choices.steps.ts](../tests/e2e/steps/dependent-choices.steps.ts)
- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-205

- [dependent-choices.steps.ts](../tests/e2e/steps/dependent-choices.steps.ts)
- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-206

- [DependentChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/DependentChoiceSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-208

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)

### REQ-QB-209

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)

### REQ-QB-210

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)

### REQ-QB-211

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)

### REQ-QB-212

- [DependentChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/DependentChoiceSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-213

- [DependentChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/DependentChoiceSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-214

- [DependentChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/DependentChoiceSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-215

- [DependentChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/DependentChoiceSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-216

- [DependentChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/DependentChoiceSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-217

- [DependentChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/DependentChoiceSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-218

- [DependentChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/DependentChoiceSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-219

- [DependentChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/DependentChoiceSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-220

- [DependentChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/DependentChoiceSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-221

- [DependentChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/DependentChoiceSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-222

- [dependent-choices.steps.ts](../tests/e2e/steps/dependent-choices.steps.ts)
- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-223

- [dependent-choices.steps.ts](../tests/e2e/steps/dependent-choices.steps.ts)
- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-224

- [dependent-choices.steps.ts](../tests/e2e/steps/dependent-choices.steps.ts)
- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-225

- [ChoiceParentMigrationSteps.cs](../tests/HpacSafety.Acceptance.Tests/ChoiceParentMigrationSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-226

- [ChoiceParentMigrationSteps.cs](../tests/HpacSafety.Acceptance.Tests/ChoiceParentMigrationSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-227

- [dependent-choices.steps.ts](../tests/e2e/steps/dependent-choices.steps.ts)
- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-228

- [ChoiceParentMigrationSteps.cs](../tests/HpacSafety.Acceptance.Tests/ChoiceParentMigrationSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

### REQ-QB-229

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)

### REQ-QB-230

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)

### REQ-QB-231

- [dependent-choices.steps.ts](../tests/e2e/steps/dependent-choices.steps.ts)
- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-232

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)

### REQ-QB-233

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)

### REQ-QB-234

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)

### REQ-QB-235

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)

### REQ-QB-236

- [dependent-choices.steps.ts](../tests/e2e/steps/dependent-choices.steps.ts)
- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-237

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [review-type-ahead-values.steps.ts](../tests/e2e/steps/review-type-ahead-values.steps.ts)

### REQ-QB-238

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [unsaved-changes.steps.ts](../tests/e2e/steps/unsaved-changes.steps.ts)

### REQ-QB-239

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [unsaved-changes.steps.ts](../tests/e2e/steps/unsaved-changes.steps.ts)

### REQ-QB-240

- [label-colon.steps.ts](../tests/e2e/steps/label-colon.steps.ts)
- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-241

- [label-colon.steps.ts](../tests/e2e/steps/label-colon.steps.ts)
- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-242

- [label-colon.steps.ts](../tests/e2e/steps/label-colon.steps.ts)
- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-243

- [label-colon.steps.ts](../tests/e2e/steps/label-colon.steps.ts)
- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)

### REQ-QB-244

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [QuestionLabelColonSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionLabelColonSteps.cs)

### REQ-QB-245

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [QuestionLabelColonSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionLabelColonSteps.cs)

### REQ-QB-246

- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)
- [QuestionLabelColonSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionLabelColonSteps.cs)

### REQ-QB-247

- [MediaConsentSteps.cs](../tests/HpacSafety.Acceptance.Tests/MediaConsentSteps.cs)
- [QuestionBankSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionBankSteps.cs)

## Claims: report-submission

### REQ-SUB-001

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-002

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-003

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-005

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-008

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-010

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-011

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-013

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-014

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-015

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-016

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-017

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-018

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-019

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-020

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-021

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)
- Unbound: `When the submission completes`
- Unbound: `Then no audit entry attributes the submission to a subject`
- Unbound: `Then no log line records the submitting subject at any level`

### REQ-SUB-022

- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-023

- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-024

- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-025

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-026

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-028

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-029

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-030

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-031

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-032

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-033

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-034

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-035

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-036

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-037

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-038

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-041

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-042

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-043

- [AttachmentUploadSteps.cs](../tests/HpacSafety.Acceptance.Tests/AttachmentUploadSteps.cs)
- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-044

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-045

- [attachments.steps.ts](../tests/e2e/steps/attachments.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-046

- [attachments.steps.ts](../tests/e2e/steps/attachments.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-047

- [attachments.steps.ts](../tests/e2e/steps/attachments.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-048

- [attachments.steps.ts](../tests/e2e/steps/attachments.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-049

- [attachments.steps.ts](../tests/e2e/steps/attachments.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-050

- [attachments.steps.ts](../tests/e2e/steps/attachments.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-051

- [attachments.steps.ts](../tests/e2e/steps/attachments.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-053

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-054

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-055

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-056

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-057

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-058

- [attachments.steps.ts](../tests/e2e/steps/attachments.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-059

- [attachments.steps.ts](../tests/e2e/steps/attachments.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-060

- [attachments.steps.ts](../tests/e2e/steps/attachments.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-061

- [attachments.steps.ts](../tests/e2e/steps/attachments.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-062

- [attachments.steps.ts](../tests/e2e/steps/attachments.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-063

- [attachments.steps.ts](../tests/e2e/steps/attachments.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-064

- [attachments.steps.ts](../tests/e2e/steps/attachments.steps.ts)
- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-065

- [attachments.steps.ts](../tests/e2e/steps/attachments.steps.ts)
- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-066

- [attachments.steps.ts](../tests/e2e/steps/attachments.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-067

- [attachments.steps.ts](../tests/e2e/steps/attachments.steps.ts)
- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-068

- [locale.steps.ts](../tests/e2e/steps/locale.steps.ts)
- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-071

- [AnswerTranslationModeSteps.cs](../tests/HpacSafety.Acceptance.Tests/AnswerTranslationModeSteps.cs)
- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-072

- [AttachmentUploadSteps.cs](../tests/HpacSafety.Acceptance.Tests/AttachmentUploadSteps.cs)
- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-073

- [AttachmentUploadSteps.cs](../tests/HpacSafety.Acceptance.Tests/AttachmentUploadSteps.cs)
- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-074

- [AttachmentUploadSteps.cs](../tests/HpacSafety.Acceptance.Tests/AttachmentUploadSteps.cs)
- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-075

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-076

- [attachments.steps.ts](../tests/e2e/steps/attachments.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-077

- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)
- [yes-no-language.steps.ts](../tests/e2e/steps/yes-no-language.steps.ts)

### REQ-SUB-078

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-079

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-080

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-081

- [AnswerTranslationModeSteps.cs](../tests/HpacSafety.Acceptance.Tests/AnswerTranslationModeSteps.cs)
- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-082

- [AnswerTranslationModeSteps.cs](../tests/HpacSafety.Acceptance.Tests/AnswerTranslationModeSteps.cs)
- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-083

- [choice-answers.steps.ts](../tests/e2e/steps/choice-answers.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-084

- [attachments.steps.ts](../tests/e2e/steps/attachments.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-085

- [contact-answers.steps.ts](../tests/e2e/steps/contact-answers.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-086

- [contact-answers.steps.ts](../tests/e2e/steps/contact-answers.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-087

- [contact-answers.steps.ts](../tests/e2e/steps/contact-answers.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-088

- [contact-answers.steps.ts](../tests/e2e/steps/contact-answers.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-089

- [contact-answers.steps.ts](../tests/e2e/steps/contact-answers.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-090

- [contact-answers.steps.ts](../tests/e2e/steps/contact-answers.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-091

- [contact-answers.steps.ts](../tests/e2e/steps/contact-answers.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-092

- [contact-answers.steps.ts](../tests/e2e/steps/contact-answers.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-093

- [contact-answers.steps.ts](../tests/e2e/steps/contact-answers.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-094

- [contact-answers.steps.ts](../tests/e2e/steps/contact-answers.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-095

- [contact-answers.steps.ts](../tests/e2e/steps/contact-answers.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-096

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)
- [StoredAnswerSteps.cs](../tests/HpacSafety.Acceptance.Tests/StoredAnswerSteps.cs)

### REQ-SUB-097

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)
- [StoredAnswerSteps.cs](../tests/HpacSafety.Acceptance.Tests/StoredAnswerSteps.cs)

### REQ-SUB-098

- [date-answers.steps.ts](../tests/e2e/steps/date-answers.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-099

- [date-answers.steps.ts](../tests/e2e/steps/date-answers.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-100

- [date-answers.steps.ts](../tests/e2e/steps/date-answers.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-101

- [date-answers.steps.ts](../tests/e2e/steps/date-answers.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-102

- [date-answers.steps.ts](../tests/e2e/steps/date-answers.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-103

- [date-answers.steps.ts](../tests/e2e/steps/date-answers.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-104

- [date-answers.steps.ts](../tests/e2e/steps/date-answers.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-105

- [date-answers.steps.ts](../tests/e2e/steps/date-answers.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-106

- [date-answers.steps.ts](../tests/e2e/steps/date-answers.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-107

- [date-answers.steps.ts](../tests/e2e/steps/date-answers.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-108

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)
- [StoredAnswerSteps.cs](../tests/HpacSafety.Acceptance.Tests/StoredAnswerSteps.cs)

### REQ-SUB-109

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)
- [StoredAnswerSteps.cs](../tests/HpacSafety.Acceptance.Tests/StoredAnswerSteps.cs)

### REQ-SUB-110

- [FutureDateSteps.cs](../tests/HpacSafety.Acceptance.Tests/FutureDateSteps.cs)
- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-111

- [date-answers.steps.ts](../tests/e2e/steps/date-answers.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-112

- [date-answers.steps.ts](../tests/e2e/steps/date-answers.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-113

- [DependentChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/DependentChoiceSteps.cs)
- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-114

- [DependentChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/DependentChoiceSteps.cs)
- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-115

- [DependentChoiceSteps.cs](../tests/HpacSafety.Acceptance.Tests/DependentChoiceSteps.cs)
- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-116

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-117

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-118

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-119

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-120

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-SUB-121

- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)
- [unsaved-changes.steps.ts](../tests/e2e/steps/unsaved-changes.steps.ts)

### REQ-SUB-122

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)
- [unsaved-changes.steps.ts](../tests/e2e/steps/unsaved-changes.steps.ts)

### REQ-SUB-123

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)
- [unsaved-changes.steps.ts](../tests/e2e/steps/unsaved-changes.steps.ts)

### REQ-SUB-124

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-125

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-126

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

### REQ-SUB-127

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)
- [submit-report.steps.ts](../tests/e2e/steps/submit-report.steps.ts)

## Claims: typeform-question-import-export

### REQ-TF-001

- [TypeformImportEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportEndpointSteps.cs)
- [TypeformImportSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportSteps.cs)

### REQ-TF-002

- [TypeformImportSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportSteps.cs)

### REQ-TF-003

- [TypeformImportSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportSteps.cs)

### REQ-TF-004

- [TypeformImportSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportSteps.cs)

### REQ-TF-005

- [TypeformImportSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportSteps.cs)

### REQ-TF-006

- [TypeformImportSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportSteps.cs)

### REQ-TF-008

- [TypeformImportSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportSteps.cs)

### REQ-TF-009

- [TypeformImportSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportSteps.cs)

### REQ-TF-010

- [TypeformImportSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportSteps.cs)

### REQ-TF-011

- [TypeformImportSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportSteps.cs)

### REQ-TF-012

- [TypeformImportSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportSteps.cs)

### REQ-TF-013

- [TypeformImportSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportSteps.cs)

### REQ-TF-014

- [TypeformImportEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportEndpointSteps.cs)
- [TypeformImportSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportSteps.cs)

### REQ-TF-015

- [TypeformImportEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportEndpointSteps.cs)
- [TypeformImportSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportSteps.cs)

### REQ-TF-016

- [TypeformImportSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportSteps.cs)

### REQ-TF-017

- [manage-questions.steps.ts](../tests/e2e/steps/manage-questions.steps.ts)
- [typeform-import.steps.ts](../tests/e2e/steps/typeform-import.steps.ts)

### REQ-TF-018

- [TypeformImportEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportEndpointSteps.cs)
- [TypeformImportSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportSteps.cs)

### REQ-TF-019

- [TypeformImportEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportEndpointSteps.cs)
- [TypeformImportSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportSteps.cs)

### REQ-TF-020

- [TypeformImportEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportEndpointSteps.cs)
- [TypeformImportSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportSteps.cs)

### REQ-TF-021

- [TypeformImportEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportEndpointSteps.cs)
- [TypeformImportSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportSteps.cs)

### REQ-TF-022

- [TypeformImportEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportEndpointSteps.cs)
- [TypeformImportSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportSteps.cs)

### REQ-TF-023

- [TypeformImportEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportEndpointSteps.cs)
- [TypeformImportSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportSteps.cs)

### REQ-TF-024

- [TypeformImportSteps.cs](../tests/HpacSafety.Acceptance.Tests/TypeformImportSteps.cs)

## Claims: web-localization-and-design

### REQ-WLD-001

- Unbound: `Given a signed-in Safety Officer is on the public report page`
- Unbound: `When they follow the Admin menu to the review queue`
- Unbound: `Then the review queue loads on the same origin as the report page`
- Unbound: `Then the browser does not load a new document`

### REQ-WLD-002

- [homepage.steps.ts](../tests/e2e/steps/homepage.steps.ts)

### REQ-WLD-003

- [contact.steps.ts](../tests/e2e/steps/contact.steps.ts)

### REQ-WLD-004

- [homepage.steps.ts](../tests/e2e/steps/homepage.steps.ts)

### REQ-WLD-005

- [locale.steps.ts](../tests/e2e/steps/locale.steps.ts)

### REQ-WLD-006

- [locale.steps.ts](../tests/e2e/steps/locale.steps.ts)

### REQ-WLD-007

- [locale.steps.ts](../tests/e2e/steps/locale.steps.ts)
- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)

### REQ-WLD-008

- [theme.steps.ts](../tests/e2e/steps/theme.steps.ts)

### REQ-WLD-009

- [footer.steps.ts](../tests/e2e/steps/footer.steps.ts)

### REQ-WLD-010

- [WebLocalizationAndDesignSteps.cs](../tests/HpacSafety.Acceptance.Tests/WebLocalizationAndDesignSteps.cs)

### REQ-WLD-011

- [WebLocalizationAndDesignSteps.cs](../tests/HpacSafety.Acceptance.Tests/WebLocalizationAndDesignSteps.cs)

### REQ-WLD-012

- [WebLocalizationAndDesignSteps.cs](../tests/HpacSafety.Acceptance.Tests/WebLocalizationAndDesignSteps.cs)

### REQ-WLD-013

- [WebLocalizationAndDesignSteps.cs](../tests/HpacSafety.Acceptance.Tests/WebLocalizationAndDesignSteps.cs)

### REQ-WLD-014

- [QuestionRenderingSteps.cs](../tests/HpacSafety.Acceptance.Tests/QuestionRenderingSteps.cs)

### REQ-WLD-015

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)

### REQ-WLD-016

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)

### REQ-WLD-017

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)

### REQ-WLD-018

- [ReportSubmissionEndpointSteps.cs](../tests/HpacSafety.Acceptance.Tests/ReportSubmissionEndpointSteps.cs)

### REQ-WLD-019

- [public-reports.steps.ts](../tests/e2e/steps/public-reports.steps.ts)

### REQ-WLD-020

- Unbound: `Given a reviewer opens a report in the admin site`
- Unbound: `Then private answers, ordinary answers, and the summary pair each sit in their own labeled section`
- Unbound: `Then each private answer is marked private in the reviewer's language`
- Unbound: `Then processing failures and the approval state are shown apart from the report's content`

### REQ-WLD-021

- [WebLocalizationAndDesignSteps.cs](../tests/HpacSafety.Acceptance.Tests/WebLocalizationAndDesignSteps.cs)

### REQ-WLD-022

- [dark-mode.steps.ts](../tests/e2e/steps/dark-mode.steps.ts)

### REQ-WLD-023

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)

### REQ-WLD-024

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)

### REQ-WLD-025

- [report-form.steps.ts](../tests/e2e/steps/report-form.steps.ts)

### REQ-WLD-026

- [WebLocalizationAndDesignSteps.cs](../tests/HpacSafety.Acceptance.Tests/WebLocalizationAndDesignSteps.cs)

### REQ-WLD-027

- [WebLocalizationAndDesignSteps.cs](../tests/HpacSafety.Acceptance.Tests/WebLocalizationAndDesignSteps.cs)

### REQ-WLD-028

- [EnglishTargetSteps.cs](../tests/HpacSafety.Acceptance.Tests/EnglishTargetSteps.cs)

### REQ-WLD-029

- [EnglishTargetSteps.cs](../tests/HpacSafety.Acceptance.Tests/EnglishTargetSteps.cs)

### REQ-WLD-030

- [locale.steps.ts](../tests/e2e/steps/locale.steps.ts)

### REQ-WLD-031

- [locale.steps.ts](../tests/e2e/steps/locale.steps.ts)

### REQ-WLD-032

- [navigation-scroll.steps.ts](../tests/e2e/steps/navigation-scroll.steps.ts)

### REQ-WLD-033

- [OpenAiTranslationSteps.cs](../tests/HpacSafety.Acceptance.Tests/OpenAiTranslationSteps.cs)

### REQ-WLD-034

- [OpenAiTranslationSteps.cs](../tests/HpacSafety.Acceptance.Tests/OpenAiTranslationSteps.cs)

### REQ-WLD-035

- [OpenAiTranslationSteps.cs](../tests/HpacSafety.Acceptance.Tests/OpenAiTranslationSteps.cs)

### REQ-WLD-036

- [OpenAiTranslationSteps.cs](../tests/HpacSafety.Acceptance.Tests/OpenAiTranslationSteps.cs)

### REQ-WLD-037

- [OpenAiTranslationSteps.cs](../tests/HpacSafety.Acceptance.Tests/OpenAiTranslationSteps.cs)

### REQ-WLD-038

- [OpenAiTranslationSteps.cs](../tests/HpacSafety.Acceptance.Tests/OpenAiTranslationSteps.cs)

### REQ-WLD-039

- [OpenAiTranslationSteps.cs](../tests/HpacSafety.Acceptance.Tests/OpenAiTranslationSteps.cs)

### REQ-WLD-040

- [OpenAiTranslationSteps.cs](../tests/HpacSafety.Acceptance.Tests/OpenAiTranslationSteps.cs)

### REQ-WLD-041

- [OpenAiTranslationSteps.cs](../tests/HpacSafety.Acceptance.Tests/OpenAiTranslationSteps.cs)

### REQ-WLD-042

- [OpenAiTranslationSteps.cs](../tests/HpacSafety.Acceptance.Tests/OpenAiTranslationSteps.cs)

### REQ-WLD-043

- [OpenAiTranslationSteps.cs](../tests/HpacSafety.Acceptance.Tests/OpenAiTranslationSteps.cs)

### REQ-WLD-044

- [OpenAiTranslationSteps.cs](../tests/HpacSafety.Acceptance.Tests/OpenAiTranslationSteps.cs)

### REQ-WLD-045

- [summary-markdown.steps.ts](../tests/e2e/steps/summary-markdown.steps.ts)

### REQ-WLD-046

- [summary-markdown.steps.ts](../tests/e2e/steps/summary-markdown.steps.ts)

### REQ-WLD-047

- [OpenAiTranslationSteps.cs](../tests/HpacSafety.Acceptance.Tests/OpenAiTranslationSteps.cs)

### REQ-WLD-048

- Unbound: `Given a reviewer is on a published report in the admin site`
- Unbound: `When they choose to delete the report`
- Unbound: `Then the admin site asks them to confirm before calling the API`
- Unbound: `Then cancelling sends no request`
- Unbound: `When they choose to unpublish the report`

## Stale @ignore

A claim still tagged `@ignore` whose every step a definition already binds. Drop
the tag once the scenario passes, or say in the scenario why it stays.

## Ambiguous steps

A step more than one definition matches. The runner fails it; name one.

## Unused step definitions

A step definition no scenario step matches: test code the specification does
not ask for. Delete it, or write the scenario it was for.
