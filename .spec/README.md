---
title: Specification index
description: Generated index of every feature area, constraint page, decision record, and lesson in the specification.
type: readme
---

# Specification index

> **Generated file — do not edit by hand.**
> Regenerate with `node tools/spec/generate-spec-index.ts`. CI fails on a difference
> ([ADR-0183](decisions/ADR-0183-the-specification-lives-in-a-spec-directory.md)).

The authority rules, the product contract, and the simplicity guardrails are
in [`features/README.md`](features/README.md). Every claim and constraint, with
what verifies it, is in [`traceability.md`](traceability.md); the step definitions
that bind each claim, in [`bindings.md`](bindings.md).

## Feature areas

Each area is one `.feature` file of scenarios and a supporting page with the
detail Gherkin cannot hold, including what not to build.

| Area | Claims | Scenarios | Planned (`@ignore`) | Browser (`@ui`) | Supporting detail |
|---|---|---|---|---|---|
| [AI anonymization](features/ai-anonymization/ai-anonymization.feature) | `REQ-AI` | 28 | 0 | 0 | [README](features/ai-anonymization/README.md) — Supporting detail for the one-call bilingual summarization and anonymization scenarios. |
| [Comments](features/comments/comments.feature) | `REQ-COM` | 21 | 0 | 7 | [README](features/comments/README.md) — Supporting detail for the member comment, translation, and moderation scenarios. |
| [Domain and lifecycle](features/domain-and-lifecycle/domain-and-lifecycle.feature) | `REQ-DOM` | 28 | 0 | 0 | [README](features/domain-and-lifecycle/README.md) — Supporting detail for the report states, invariants, deletion, and retention scenarios. |
| [Attachments](features/media/media.feature) | `REQ-MED` | 60 | 0 | 15 | [README](features/media/README.md) — Supporting detail for the image, video, document, quarantine, and derivative scenarios. |
| [Moderation, authentication, and publication](features/moderation-authentication-and-publication/moderation-authentication-and-publication.feature) | `REQ-MOD` | 200 | 0 | 98 | [README](features/moderation-authentication-and-publication/README.md) — Supporting detail for the member authentication, review, and public feed scenarios. |
| [Question bank and form](features/question-bank-and-form/question-bank-and-form.feature) | `REQ-QB` | 227 | 0 | 92 | [README](features/question-bank-and-form/README.md) — Supporting detail for the immutable bilingual question and form assembly scenarios. |
| [Report submission](features/report-submission/report-submission.feature) | `REQ-SUB` | 120 | 0 | 79 | [README](features/report-submission/README.md) — Supporting detail for the browser continuity, upload and submission API, DTO, and validation scenarios. |
| [Typeform question import and export](features/typeform-question-import-export/typeform-question-import-export.feature) | `REQ-TF` | 23 | 0 | 1 | [README](features/typeform-question-import-export/README.md) — Supporting detail for importing and exporting the question bank as Typeform JSON. |
| [Web, localization, and design](features/web-localization-and-design/web-localization-and-design.feature) | `REQ-WLD` | 49 | 0 | 25 | [README](features/web-localization-and-design/README.md) — Supporting detail for the bilingual React sites, design system, and accessibility scenarios. |

## Constraint pages

Each normative constraint carries a `CON-*` ID naming the claims that verify it.

| Page | Constraints | Count | Description |
|---|---|---|---|
| [System overview](system-overview.md) | `CON-SO` | 9 | The canonical purpose, boundaries, components, and explicit out-of-scope list. |
| [Data and persistence](data-and-persistence.md) | `CON-DP` | 16 | The canonical target records, naming, transactions, constraints, and query DTOs. |
| [Interfaces and data flow](interfaces-and-data-flow.md) | `CON-IF` | 10 | The canonical HTTP surface, ports, and end-to-end flow of a report through the system. |
| [Infrastructure and operations](infrastructure-and-operations.md) | `CON-INF` | 20 | The canonical minimal AWS topology, deployment, secrets, backups, and alerting. |
| [Testing and quality](testing-and-quality.md) | `CON-TQ` | 10 | The canonical test strategy, required contract coverage, and quality gates. |

## Decisions

Architecture decision records, newest first. What an ADR is for:
[`decisions/README.md`](decisions/README.md).

| ADR | Title | Status | Date |
|---|---|---|---|
| [0191](decisions/ADR-0191-each-rule-is-stated-once-and-no-status-page-is-written-by-hand.md) | Each rule is stated once, and no status page is written by hand | accepted | 2026-10-03 |
| [0190](decisions/ADR-0190-the-interface-catalogues-ship-in-the-page-bundle-and-the-page-is-never-cached-stale.md) | The interface catalogues ship in the page bundle, and the page is never cached stale | accepted | 2026-10-03 |
| [0189](decisions/ADR-0189-a-workflow-step-runs-one-command-and-tools-is-grouped-by-domain.md) | A workflow step runs one command, and tools/ is grouped by domain | accepted | 2026-10-02 |
| [0188](decisions/ADR-0188-a-components-logic-lives-in-foo-tsx-and-its-markup-in-foo-view-tsx-and-web-logic-is-unit-tested.md) | A component's logic lives in Foo.tsx and its markup in Foo.view.tsx, and web logic is unit-tested | accepted | 2026-10-02 |
| [0187](decisions/ADR-0187-a-migration-restores-the-seeded-groups-a-fresh-database-lost.md) | A migration restores the seeded groups a fresh database lost | accepted | 2026-10-02 |
| [0186](decisions/ADR-0186-the-country-question-is-a-pinned-country-pick-list-and-province-follows-it.md) | The Country question is a pinned country pick list, and Province follows it | accepted | 2026-10-02 |
| [0185](decisions/ADR-0185-a-submission-answers-only-current-revisions-and-the-browser-drops-the-rest.md) | A submission answers only current revisions, and the browser drops stale saved answers | accepted | 2026-09-30 |
| [0184](decisions/ADR-0184-a-generated-map-binds-every-claim-to-its-step-definitions.md) | A generated map binds every claim to its step definitions | accepted | 2026-09-30 |
| [0183](decisions/ADR-0183-the-specification-lives-in-a-spec-directory.md) | The specification lives in a .spec directory | partially-superseded | 2026-09-30 |
| [0182](decisions/ADR-0182-a-role-agent-declares-its-model-and-effort.md) | A role agent declares its model and effort | accepted | 2026-09-30 |
| [0181](decisions/ADR-0181-a-one-time-migration-trims-label-colons-in-place.md) | A one-time migration trims label colons in place | accepted | 2026-09-30 |
| [0180](decisions/ADR-0180-a-summary-is-markdown-with-one-section-per-public-paragraph-question.md) | A summary is Markdown with one section per public paragraph question | accepted | 2026-09-30 |
| [0179](decisions/ADR-0179-gemini-translates-everything-between-canadian-english-and-canadian-french.md) | Gemini translates everything between Canadian English and Canadian French | accepted | 2026-09-29 |
| [0178](decisions/ADR-0178-the-database-refuses-changes-to-the-reporters-account-and-to-summary-revisions.md) | The database refuses changes to the reporter's account and to summary revisions | accepted | 2026-09-29 |
| [0177](decisions/ADR-0177-summaries-are-append-only-revisions-and-a-live-edit-publishes-itself.md) | Summaries are append-only revisions, and a live edit publishes itself | accepted | 2026-09-29 |
| [0176](decisions/ADR-0176-a-published-report-page-shows-the-language-it-was-written-in.md) | A published report page shows the language it was written in | accepted | 2026-09-29 |
| [0174](decisions/ADR-0174-an-answers-second-language-is-written-once-by-the-worker-only.md) | An answer's second language is written once, by the Worker only | accepted | 2026-09-29 |
| [0173](decisions/ADR-0173-a-fresh-navigation-starts-at-the-top-and-only-a-return-restores.md) | A fresh navigation starts at the top, and only a return restores | accepted | 2026-09-29 |
| [0172](decisions/ADR-0172-a-temporary-interim-issuer-signs-staging-tokens-until-a-real-provider-exists.md) | A temporary interim issuer signs staging tokens until a real provider exists | accepted | 2026-09-28 |
| [0171](decisions/ADR-0171-terraform-reads-back-only-the-origin-secret-and-log-groups-are-guarded-by-name.md) | Terraform reads back only the origin secret, and log groups are guarded by name | accepted | 2026-09-28 |
| [0170](decisions/ADR-0170-each-account-groups-its-resources-by-a-tag-based-resource-group-alone.md) | Each account groups its resources by a tag-based Resource Group alone | accepted | 2026-09-28 |
| [0169](decisions/ADR-0169-the-deploy-role-manages-what-is-tagged-ours-and-tags-only-as-ours.md) | The deploy role manages what is already tagged ours, and tags only as ours | accepted | 2026-09-28 |
| [0168](decisions/ADR-0168-a-release-is-created-by-one-action-with-generated-notes.md) | A release is created by one Action, with notes generated from the merged pull requests | accepted | 2026-09-28 |
| [0167](decisions/ADR-0167-oidc-trust-names-the-immutable-subject.md) | The OIDC trust policies name GitHub's immutable subject, with the organization and repository IDs | accepted | 2026-09-28 |
| [0166](decisions/ADR-0166-a-release-deploys-staging-and-a-separate-workflow-promotes-to-production.md) | A release deploys staging, and a separate workflow promotes a staged tag to production | accepted | 2026-09-28 |
| [0165](decisions/ADR-0165-the-coverage-baseline-walks-back-to-the-newest-run-that-ran-coverage.md) | The coverage baseline walks back to the newest run that ran coverage | accepted | 2026-09-28 |
| [0164](decisions/ADR-0164-release-workflow-build-once-deploy-and-promote.md) | The release workflow builds once through a reusable deploy job, and a pull request plans through separate repository-scoped credentials | partially-superseded | 2026-09-27 |
| [0163](decisions/ADR-0163-the-cloudfront-origin-secret-is-terraform-generated.md) | The CloudFront origin secret is Terraform-generated, the one exception to "entries, never values" | accepted | 2026-09-27 |
| [0159](decisions/ADR-0159-cloudfront-routes-api-to-a-function-url-no-alb.md) | CloudFront routes /api/* to the API's Function URL — no ALB | accepted | 2026-09-27 |
| [0158](decisions/ADR-0158-two-aws-accounts-staged-and-promoted-by-approval.md) | Two AWS accounts, staging and production, released by date tag and promoted by approval | partially-superseded | 2026-09-27 |
| [0157](decisions/ADR-0157-the-public-search-privacy-boundary.md) | The public search privacy boundary | accepted | 2026-09-27 |
| [0156](decisions/ADR-0156-postgres-full-text-and-trigram-search-for-manage-reports.md) | Postgres full-text and trigram search powers the admin search box | accepted | 2026-09-28 |
| [0155](decisions/ADR-0155-infinite-scroll-replaces-load-more-on-both-report-lists.md) | Infinite scroll replaces Load more on both report lists | accepted | 2026-09-27 |
| [0154](decisions/ADR-0154-reporter-and-pilot-names-are-read-by-question-role.md) | Reporter and pilot names are read by question role | accepted | 2026-09-27 |
| [0153](decisions/ADR-0153-the-public-feed-sorts-by-submission-time.md) | The public feed sorts by submission time | accepted | 2026-09-27 |
| [0152](decisions/ADR-0152-a-type-aheads-list-opens-with-a-hint-below-3-characters.md) | A type-ahead's list opens with a hint below 3 characters | accepted | 2026-09-26 |
| [0151](decisions/ADR-0151-one-dependent-choice-may-be-offered-under-several-parent-choices.md) | One dependent choice may be offered under several parent choices | accepted | 2026-09-26 |
| [0150](decisions/ADR-0150-a-single-select-is-a-select-only-combobox-the-form-draws.md) | A single-select is a select-only combobox the form draws | accepted | 2026-09-26 |
| [0149](decisions/ADR-0149-a-bot-push-stands-down-when-its-own-output-already-landed.md) | A bot push stands down when its own output already landed | accepted | 2026-09-26 |
| [0148](decisions/ADR-0148-a-terraform-apply-waits-in-its-own-concurrency-group.md) | A Terraform apply waits in its own concurrency group | accepted | 2026-09-26 |
| [0147](decisions/ADR-0147-pull-requests-merge-through-a-merge-queue.md) | Pull requests merge through a merge queue | accepted | 2026-09-26 |
| [0146](decisions/ADR-0146-a-choice-list-may-depend-on-another-questions-answer.md) | A question's choices may depend on another question's answer | superseded | 2026-09-26 |
| [0145](decisions/ADR-0145-a-pull-requests-checks-run-locally-under-act.md) | A pull request's checks run locally under act, against CI's own baseline | accepted | 2026-09-26 |
| [0144](decisions/ADR-0144-the-wording-is-translated-on-request-in-a-chosen-direction.md) | The question wording is translated on request, in a direction the administrator chooses | accepted | 2026-09-26 |
| [0143](decisions/ADR-0143-issue-traceability-drift-opens-an-issue-and-gates-nothing.md) | Issue traceability drift opens an issue and gates nothing | partially-superseded | 2026-09-26 |
| [0142](decisions/ADR-0142-a-web-ui-pull-request-shows-its-screenshots.md) | A web UI pull request shows its screenshots | accepted | 2026-09-26 |
| [0141](decisions/ADR-0141-a-choice-is-translated-on-request-in-a-chosen-direction.md) | A choice is translated on request, one at a time, in a direction the administrator chooses | accepted | 2026-09-26 |
| [0140](decisions/ADR-0140-a-type-ahead-is-a-combobox-the-form-draws.md) | A type-ahead is a combobox the form draws | accepted | 2026-09-26 |
| [0139](decisions/ADR-0139-the-database-skills-and-agent-join-the-generic-classification.md) | The database skills and agent join the generic classification | accepted | 2026-09-26 |
| [0138](decisions/ADR-0138-a-date-question-allows-future-dates-only-when-it-says-so.md) | A date question allows future dates only when it says so | accepted | 2026-09-26 |
| [0137](decisions/ADR-0137-a-phone-answer-is-stored-in-e164.md) | A phone answer is stored in E.164 | accepted | 2026-09-26 |
| [0136](decisions/ADR-0136-choices-are-listed-alphabetically-in-the-readers-language.md) | Choices are listed alphabetically in the reader's language | accepted | 2026-09-26 |
| [0135](decisions/ADR-0135-staff-add-private-attachments-to-a-report.md) | Staff add private attachments to a report | accepted | 2026-09-26 |
| [0134](decisions/ADR-0134-a-claim-reads-a-zip-packages-directory-as-well-as-its-leading-bytes.md) | A claim reads a zip package's directory as well as its leading bytes | accepted | 2026-09-26 |
| [0133](decisions/ADR-0133-staff-keep-private-notes-on-a-report.md) | Staff keep private notes on a report | accepted | 2026-09-26 |
| [0132](decisions/ADR-0132-a-condition-follows-its-parent-through-a-fork.md) | A condition follows its parent through a fork | accepted | 2026-09-25 |
| [0131](decisions/ADR-0131-a-generic-skill-names-no-project-and-a-project-skill-extends-it.md) | A generic skill names no project, and a project skill extends it | accepted | 2026-09-25 |
| [0130](decisions/ADR-0130-a-yes-or-no-answer-is-stored-as-a-boolean.md) | A yes or no answer is stored as a boolean | accepted | 2026-09-25 |
| [0129](decisions/ADR-0129-a-type-ahead-value-is-edited-in-place-merged-and-reviewed.md) | A type-ahead value is edited in place, merged, and reviewed by a safety officer | accepted | 2026-09-25 |
| [0128](decisions/ADR-0128-an-answer-names-its-choice-and-a-picker-option-is-fixed-or-replaced.md) | An answer names its choice, and a picker option is fixed in place or replaced | accepted | 2026-09-25 |
| [0127](decisions/ADR-0127-a-yes-or-no-answer-is-stored-in-the-reporters-language.md) | A yes or no answer is stored in the reporter's language | superseded | 2026-09-25 |
| [0126](decisions/ADR-0126-an-attachment-uploads-straight-to-quarantine-by-pre-signed-put.md) | An attachment uploads straight to quarantine by pre-signed PUT | partially-superseded | 2026-09-25 |
| [0125](decisions/ADR-0125-a-report-is-pending-published-or-unpublished.md) | A report is pending, published, or unpublished | accepted | 2026-09-25 |
| [0124](decisions/ADR-0124-the-ai-author-role-may-register-its-files-and-mend-links.md) | The ai-author role may register its files and mend links | accepted | 2026-09-25 |
| [0123](decisions/ADR-0123-the-worker-runs-on-lambda-and-the-website-on-s3-and-cloudfront.md) | The Worker runs on Lambda and the website on S3 and CloudFront | accepted | 2026-09-25 |
| [0122](decisions/ADR-0122-a-video-derivative-is-always-an-mp4.md) | A video's derivative is always an MP4 | accepted | 2026-09-24 |
| [0121](decisions/ADR-0121-a-fifth-role-maintains-the-agent-instructions.md) | A fifth role maintains the agent instructions | accepted | 2026-09-24 |
| [0120](decisions/ADR-0120-the-dotnet-major-moves-in-one-pull-request.md) | The .NET major moves in one pull request | accepted | 2026-09-24 |
| [0119](decisions/ADR-0119-a-published-report-offers-its-documents-for-download.md) | A published report offers its documents for download | accepted | 2026-09-24 |
| [0118](decisions/ADR-0118-the-worker-image-installs-ubuntus-ffmpeg.md) | The Worker image installs Ubuntu's ffmpeg | partially-superseded | 2026-09-24 |
| [0117](decisions/ADR-0117-a-published-report-shows-the-reporters-photos-and-video.md) | A published report shows the reporter's photos and video | accepted | 2026-09-24 |
| [0116](decisions/ADR-0116-a-read-rule-lives-in-a-view.md) | A read rule lives in a view | accepted | 2026-09-24 |
| [0115](decisions/ADR-0115-the-english-translation-target-is-configuration.md) | The English machine-translation target is configuration | partially-superseded | 2026-09-24 |
| [0114](decisions/ADR-0114-members-may-comment-on-a-published-report.md) | Members may comment on a published report | accepted | 2026-09-24 |
| [0113](decisions/ADR-0113-a-bot-pushing-onto-a-pull-request-replays-past-another-bot.md) | A bot pushing onto a pull request replays its commit past another bot | accepted | 2026-09-24 |
| [0112](decisions/ADR-0112-only-answers-that-need-it-get-a-second-language.md) | Only answers that need it get a second language; a picker's comes from its choice | partially-superseded | 2026-09-24 |
| [0111](decisions/ADR-0111-renovate-cites-the-claims-a-web-dependency-bump-preserves.md) | Renovate cites the claims a web dependency bump preserves | accepted | 2026-09-24 |
| [0110](decisions/ADR-0110-rustfs-replaces-minio-as-the-development-s3-server.md) | RustFS replaces MinIO as the development and test S3 server | accepted | 2026-09-24 |
| [0109](decisions/ADR-0109-no-translation-stand-in-in-any-environment.md) | No translation stand-in in any environment; Development needs a real key | accepted | 2026-09-24 |
| [0108](decisions/ADR-0108-a-reviewer-may-machine-translate-a-summary-language.md) | A reviewer may machine-translate one summary language from the other | accepted | 2026-09-23 |
| [0107](decisions/ADR-0107-an-agent-session-link-never-reaches-the-public-history.md) | An agent session link never reaches the public history | accepted | 2026-09-23 |
| [0106](decisions/ADR-0106-every-line-of-the-matrix-derives-from-one-source-item.md) | Every line of the traceability matrix derives from one source item | accepted | 2026-09-23 |
| [0105](decisions/ADR-0105-approving-a-consented-pair-publishes-it.md) | Approving a consented pair publishes it | partially-superseded | 2026-09-23 |
| [0104](decisions/ADR-0104-summaries-are-generated-by-gemini-through-a-paid-key.md) | Summaries are generated by Gemini through a paid key | accepted | 2026-09-23 |
| [0103](decisions/ADR-0103-a-translation-run-reports-to-the-issues-waiting-on-it.md) | A translation run reports to the issues waiting on it | accepted | 2026-09-23 |
| [0102](decisions/ADR-0102-a-term-list-holds-the-french-translator-to-a-word.md) | A term list holds the French translator to a word | partially-superseded | 2026-09-23 |
| [0101](decisions/ADR-0101-ci-regenerates-the-traceability-matrix.md) | CI regenerates the traceability matrix onto the pull request, and the matrix check is required to merge | accepted | 2026-09-23 |
| [0100](decisions/ADR-0100-an-attachment-is-kept-as-long-as-the-saved-report.md) | An attachment is kept as long as the saved report | accepted | 2026-09-23 |
| [0099](decisions/ADR-0099-a-report-page-is-addressed-by-its-question-key.md) | A report page is addressed by its question key | accepted | 2026-09-23 |
| [0098](decisions/ADR-0098-submission-copies-the-original-and-the-worker-makes-the-derivative.md) | Submission copies the original and the Worker makes the derivative | accepted | 2026-09-23 |
| [0097](decisions/ADR-0097-a-reviewer-downloads-an-attachment-under-its-sanitized-original-name.md) | A reviewer downloads an attachment under its sanitized original name | accepted | 2026-09-23 |
| [0096](decisions/ADR-0096-an-attachment-uploads-on-attach-and-is-claimed-at-submission.md) | An attachment uploads on attach and is claimed at submission | partially-superseded | 2026-09-23 |
| [0095](decisions/ADR-0095-a-question-owns-its-choices-outside-its-revisions.md) | A question owns its choices, outside its revisions | accepted | 2026-09-22 |
| [0094](decisions/ADR-0094-video-is-remuxed-not-transcoded-and-never-refused.md) | Video is remuxed, never transcoded, and an unstrippable video is kept rather than refused | accepted | 2026-09-22 |
| [0093](decisions/ADR-0093-the-return-type-says-a-method-is-asynchronous.md) | The return type says a method is asynchronous, so the name does not | accepted | 2026-09-22 |
| [0092](decisions/ADR-0092-admin-route-status-codes-and-atomic-audit-writes.md) | Admin routes answer with real 401/403, and an audit write is atomic with its action | accepted | 2026-09-22 |
| [0091](decisions/ADR-0091-an-adr-number-is-verified-not-assumed.md) | An ADR number is verified, not assumed | accepted | 2026-09-22 |
| [0090](decisions/ADR-0090-an-exemption-cites-the-claims-it-preserves.md) | An exemption from scenario coverage cites the claims it preserves | accepted | 2026-09-22 |
| [0089](decisions/ADR-0089-no-malware-scanning-for-attachments.md) | No malware scanning for attachments | accepted | 2026-09-22 |
| [0088](decisions/ADR-0088-the-matrix-carries-the-specification-into-the-graph.md) | The generated matrix carries the specification into the graph | accepted | 2026-09-22 |
| [0087](decisions/ADR-0087-every-markdown-file-declares-itself.md) | Every markdown file declares what it is | accepted | 2026-09-22 |
| [0086](decisions/ADR-0086-four-role-agents-defined-in-the-repository.md) | Four roles are defined as repository agents | accepted | 2026-09-22 |
| [0085](decisions/ADR-0085-a-lesson-flows-upstream-into-the-specification.md) | A lesson flows upstream into the specification | accepted | 2026-09-22 |
| [0084](decisions/ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md) | A claim has a stable ID, and the traceability matrix is generated | partially-superseded | 2026-09-22 |
| [0083](decisions/ADR-0083-specification-driven-development.md) | Specification-driven development is how this repository works | accepted | 2026-09-22 |
| [0082](decisions/ADR-0082-a-deterministic-marking-pass-precedes-the-one-model-call.md) | A deterministic marking pass precedes the one model call | accepted | 2026-09-22 |
| [0081](decisions/ADR-0081-trust-forwarded-headers-from-the-security-group-boundary.md) | Trust forwarded headers because the security group is the trust boundary, not a static proxy list | superseded | 2026-09-22 |
| [0080](decisions/ADR-0080-every-answer-gets-a-worker-translated-second-language.md) | Every answer gets a Worker-translated second language; the submitted value is immutable | partially-superseded | 2026-09-22 |
| [0079](decisions/ADR-0079-a-development-login-may-verify-against-the-live-members-site.md) | A development login may verify against the live members site | accepted | 2026-09-21 |
| [0078](decisions/ADR-0078-typeform-import-is-english-led-and-defers-all-branching-logic.md) | Typeform import is English-led, and every real branching rule is pending, not auto-mapped | accepted | 2026-09-22 |
| [0077](decisions/ADR-0077-typeform-json-import-and-export.md) | Question bank import/export uses Typeform's own JSON, not QSF | accepted | 2026-09-21 |
| [0076](decisions/ADR-0076-statement-and-group-question-types.md) | Statement and Group are question types again, and a Group has children | accepted | 2026-09-21 |
| [0075](decisions/ADR-0075-tabs-over-spaces-for-indentation.md) | Tabs, not spaces, for indentation | accepted | 2026-09-22 |
| [0074](decisions/ADR-0074-a-single-select-parent-may-enable-a-conditional-question.md) | A conditional question's parent may be yes/no or single-select, naming a required option | accepted | 2026-09-21 |
| [0073](decisions/ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md) | A @ui scenario is skipped by Reqnroll itself, not by a CI filter | accepted | 2026-09-21 |
| [0072](decisions/ADR-0072-every-answer-is-stored-as-a-string.md) | Every answer is stored as a string, in the reporter's language | partially-superseded | 2026-09-21 |
| [0071](decisions/ADR-0071-an-answered-question-forks-instead-of-revising.md) | A question that has been answered forks instead of revising | accepted | 2026-09-21 |
| [0070](decisions/ADR-0070-a-hand-edited-french-value-is-a-recorded-correction.md) | A hand-edited French value is a recorded correction | accepted | 2026-09-21 |
| [0069](decisions/ADR-0069-scannable-given-when-then-test-names.md) | Scannable Given/When/Then test names | accepted | 2026-09-21 |
| [0068](decisions/ADR-0068-the-member-token-replaces-turnstile-on-submission.md) | The member token replaces Turnstile on submission | accepted | 2026-09-21 |
| [0067](decisions/ADR-0067-a-reporter-must-be-a-member-and-is-not-recorded.md) | A reporter must be a member, and is not recorded | accepted | 2026-09-21 |
| [0066](decisions/ADR-0066-a-development-identity-provider-signed-with-a-dev-key.md) | A development identity provider, signed with a dev key | partially-superseded | 2026-09-21 |
| [0065](decisions/ADR-0065-no-user-records-identity-is-the-token-subject.md) | No user records; identity is the token subject | accepted | 2026-09-21 |
| [0064](decisions/ADR-0064-jwt-bearer-authentication-with-three-roles.md) | JWT bearer authentication with three roles | accepted | 2026-09-21 |
| [0063](decisions/ADR-0063-a-reporter-may-add-a-type-ahead-choice.md) | A reporter may add a missing type-ahead choice, and an autocomplete renders the live list | superseded | 2026-09-21 |
| [0062](decisions/ADR-0062-administrators-may-machine-translate-question-text.md) | An administrator may machine-translate question text while authoring; the database still stores only what they saved | partially-superseded | 2026-09-20 |
| [0061](decisions/ADR-0061-administrators-may-require-any-question.md) | An administrator may make any question mandatory; consent is merely the one that cannot be optional | accepted | 2026-09-20 |
| [0060](decisions/ADR-0060-conditional-questions-depend-on-a-boolean-question.md) | A conditional question names a parent question, which must be a yes/no question | partially-superseded | 2026-09-20 |
| [0059](decisions/ADR-0059-dnd-kit-for-reordering.md) | Reordering uses @dnd-kit, behind one owned component, and never requires a pointer | accepted | 2026-09-20 |
| [0058](decisions/ADR-0058-shared-option-sets-with-a-revision-snapshot.md) | A shared choice list is authored once and snapshotted into every revision that uses it | superseded | 2026-09-20 |
| [0057](decisions/ADR-0057-same-repo-pull-requests-translate-in-pr.md) | A same-repo pull request gets its French translated onto its own branch; a fork PR still waits until after merge | accepted | 2026-09-20 |
| [0056](decisions/ADR-0056-fr-ca-locale-files-are-tracked-not-gitignored.md) | locales/fr-CA.json and locales/fr-CA.meta.json are tracked files, not gitignored | accepted | 2026-09-19 |
| [0055](decisions/ADR-0055-ef-core-migrations-sql-files-stored-procedures.md) | EF Core is the only path to schema change; SQL lives in files; the app applies its own migrations | accepted | 2026-09-19 |
| [0054](decisions/ADR-0054-local-build-stubs-missing-translations.md) | The local build stubs missing translations; CI still does the actual translating | accepted | 2026-09-19 |
| [0053](decisions/ADR-0053-ui-scenarios-execute-via-playwright-bdd.md) | @ui scenarios execute via Playwright, not Reqnroll | accepted | 2026-09-19 |
| [0052](decisions/ADR-0052-no-inline-script-typescript-only.md) | No inline JavaScript in HTML; every script is an external TypeScript module | accepted | 2026-09-19 |
| [0051](decisions/ADR-0051-react-router-for-client-side-navigation.md) | React Router for client-side navigation | accepted | 2026-09-19 |
| [0050](decisions/ADR-0050-ui-tag-for-scenarios-needing-playwright.md) | @ui tags the .feature scenarios that need a Playwright companion | partially-superseded | 2026-09-19 |
| [0049](decisions/ADR-0049-reqnroll-for-executable-gherkin-scenarios.md) | Reqnroll executes the .feature files | accepted | 2026-09-19 |
| [0048](decisions/ADR-0048-one-website-admin-as-a-route.md) | One website again; the admin review queue is a route, not a separate site | partially-superseded | 2026-09-19 |
| [0047](decisions/ADR-0047-feature-files-must-not-contradict-adrs.md) | A feature file may never contradict an accepted ADR | accepted | 2026-09-18 |
| [0046](decisions/ADR-0046-mermaid-for-diagrams.md) | Diagrams in Markdown are Mermaid, not images | accepted | 2026-09-18 |
| [0045](decisions/ADR-0045-ui-changes-require-playwright-and-server-tests.md) | Every UI change ships with a Playwright test and its server-side counterpart | accepted | 2026-09-18 |
| [0044](decisions/ADR-0044-containerized-web-hosting.md) | The web front end is a Docker container, not S3 + CloudFront | superseded | 2026-09-18 |
| [0043](decisions/ADR-0043-react-typescript-vite-web-front-end.md) | React, TypeScript, and Vite replace the no-framework web build | partially-superseded | 2026-09-18 |
| [0042](decisions/ADR-0042-lambda-hosted-api-with-fargate-migration-path.md) | Host the API on Lambda, built for a later Fargate migration | partially-superseded | 2026-09-18 |
| [0041](decisions/ADR-0041-no-shared-kernel-folder.md) | Core has no SharedKernel folder; cross-cutting types sit at the namespace root | accepted | 2026-09-18 |
| [0040](decisions/ADR-0040-migrate-canonical-domain-and-persistence.md) | Migrate to the canonical domain and persistence model | partially-superseded | 2026-08-26 |
| [0039](decisions/ADR-0039-path-gated-required-checks.md) | Path-gated required checks via job-level if:, never paths: | accepted | 2026-08-26 |
| [0038](decisions/ADR-0038-question-privacy-and-llm-anonymization.md) | Question privacy partitions an LLM-only anonymization request | partially-superseded | 2026-08-22 |
| [0037](decisions/ADR-0037-progressive-agent-instructions.md) | Progressive agent instructions | accepted | 2026-08-22 |
| [0035](decisions/ADR-0035-dateonly-datetimeoffset-timeonly-datetime-is-banned.md) | DateOnly, DateTimeOffset, TimeOnly; DateTime is banned | partially-superseded | 2026-08-22 |
| [0034](decisions/ADR-0034-tiny-ids.md) | Every row is identified by an eleven-character tiny id | accepted | 2026-08-22 |
| [0033](decisions/ADR-0033-third-party-libraries-behind-owned-abstractions.md) | Third-party libraries are used behind an abstraction we own | partially-superseded | 2026-08-22 |
| [0032](decisions/ADR-0032-terraform-ci-without-an-aws-account.md) | Two roles, and a check that works without an AWS account | partially-superseded | 2026-08-22 |
| [0031](decisions/ADR-0031-terraform-shape-and-topology.md) | The shape of the Terraform, and the topology it builds | superseded | 2026-08-22 |
| [0028](decisions/ADR-0028-role-words-in-place-of-names.md) | A name in a narrative becomes a role word, not a placeholder | partially-superseded | 2026-08-22 |
| [0027](decisions/ADR-0027-deterministic-scrub-design.md) | The deterministic scrub is a closed chain over labelled fields | superseded | 2026-08-22 |
| [0026](decisions/ADR-0026-presigned-urls-and-private-blob-storage.md) | Every blob is reached through a short-lived pre-signed URL | partially-superseded | 2026-08-22 |
| [0025](decisions/ADR-0025-magick-net-for-exif-stripping.md) | Magick.NET strips EXIF and sniffs content types | partially-superseded | 2026-08-22 |
| [0024](decisions/ADR-0024-dark-mode-is-a-token-redefinition.md) | Dark mode is a token redefinition, not a variant | accepted | 2026-08-22 |
| [0023](decisions/ADR-0023-pinned-and-vendored-web-assets.md) | The web build's inputs are pinned, verified, and vendored | partially-superseded | 2026-08-22 |
| [0022](decisions/ADR-0022-translation-provider-is-configuration.md) | DeepL, behind a one-file adapter, after GitHub Models was retired | partially-superseded | 2026-08-22 |
| [0021](decisions/ADR-0021-ci-translation-opens-a-pull-request.md) | The CI translation job opens a pull request, and never translates on one | accepted | 2026-08-22 |
| [0020](decisions/ADR-0020-seeding-by-migration.md) | The migration seeds the question bank, and guards the one local administrator | partially-superseded | 2026-08-22 |
| [0019](decisions/ADR-0019-application-side-field-encryption.md) | Report values are encrypted by the application, not by the database | superseded | 2026-08-22 |
| [0018](decisions/ADR-0018-feature-folders-in-core.md) | Core is organised by feature, with a shared kernel | partially-superseded | 2026-08-22 |
| [0017](decisions/ADR-0017-ratchet-judges-added-code.md) | The coverage ratchet judges added code, not the whole-repository ratio | accepted | 2026-08-22 |
| [0016](decisions/ADR-0016-data-driven-question-bank.md) | The question set is data, not code | partially-superseded | 2026-08-22 |
| [0015](decisions/ADR-0015-one-shell-script-for-development-setup.md) | One POSIX sh script for development environment setup | accepted | 2026-08-22 |
| [0014](decisions/ADR-0014-coverage-gate.md) | Coverage: an absolute floor plus a ratchet, from main's last artifact | accepted | 2026-08-22 |
| [0013](decisions/ADR-0013-ban-assert-rather-than-grep-for-it.md) | Ban Xunit.Assert with an analyzer, not a CI grep | accepted | 2026-08-22 |
| [0012](decisions/ADR-0012-every-pull-request-closes-an-issue.md) | Every pull request closes an issue, enforced in CI | accepted | 2026-08-22 |
| [0011](decisions/ADR-0011-ci-contexts-precede-their-checks.md) | CI contexts exist before the things they check | accepted | 2026-08-22 |
| [0010](decisions/ADR-0010-infrastructure-as-code.md) | Terraform, with a scripted one-time bootstrap | partially-superseded | 2026-08-22 |
| [0009](decisions/ADR-0009-hosting-on-aws.md) | Host on AWS, in ca-central-1 | partially-superseded | 2026-08-22 |
| [0008](decisions/ADR-0008-github-workflow.md) | Rulesets, and no CODEOWNERS | accepted | 2026-08-22 |
| [0007](decisions/ADR-0007-localization.md) | Bilingual, with CI-time translation | partially-superseded | 2026-08-22 |
| [0006](decisions/ADR-0006-theme-engine.md) | Tailwind v4 standalone CLI | superseded | 2026-08-22 |
| [0005](decisions/ADR-0005-authentication.md) | Credential proxy for admin authentication | superseded | 2026-08-22 |
| [0004](decisions/ADR-0004-human-review-required.md) | Mandatory human review before publication | accepted | 2026-08-22 |
| [0003](decisions/ADR-0003-anonymization-pipeline.md) | Five-stage anonymization, deterministic first | superseded | 2026-08-22 |
| [0002](decisions/ADR-0002-transactional-outbox.md) | Transactional outbox for AI processing | accepted | 2026-08-22 |
| [0001](decisions/ADR-0001-repository-and-agent-configuration.md) | Agent-agnostic configuration via AGENTS.md and skillfile | accepted | 2026-08-22 |

## Lessons

What the specification should have said, newest first. When to write one:
[`lessons/README.md`](lessons/README.md).

| Lesson | Title | What it cost us | Remedy | Issue | Date | Status |
|---|---|---|---|---|---|---|
| [0043](lessons/0043-a-deploy-deleted-the-catalogue-an-open-page-still-needed.md) | A deploy deleted the catalogue an open page still needed | Lazily loaded locale catalogues were content-hashed chunks; a deploy deleted them under an open tab, CloudFront answered with index.html, and a swallowed error left the page showing raw keys until a full reload. | REQ-WLD-049 | #805 | 2026-10-03 | accepted |
| [0042](lessons/0042-a-check-nobody-required-let-a-broken-bump-merge.md) | A check nobody required let a broken bump merge | Renovate's ESLint 10 bump merged with lint failing, because lint ran on every pull request but was not a required check, and npm ci then failed on main and every branch rebased onto it. | `deliver-hpac-change` | #788 | 2026-10-02 | accepted |
| [0041](lessons/0041-a-stubbed-form-never-saw-what-the-migrations-seed.md) | A stubbed form never saw what the migrations seed | A database created from scratch lost every seeded group, and staging asked each grouped question on its own page. The browser suite stubbed the questions by hand and no scenario said what a freshly migrated database sends, so nothing failed. | REQ-QB-259, REQ-QB-266 | #754 | 2026-10-02 | accepted |
| [0040](lessons/0040-a-group-that-stopped-being-one-hid-its-questions.md) | A group that stopped being one hid its questions | Deleting a group, or retyping it to another type, left each child naming a heading that no longer existed, and the public form dropped those questions. No scenario said what happens to a child when its group stops being one. | REQ-QB-052 | #720 | 2026-09-30 | accepted |
| [0039](lessons/0039-a-place-name-the-translator-was-told-to-copy.md) | A place name the translator was told to copy | After the switch to Gemini, a reviewer's Translate button returned type-ahead place names in English, because the translation prompt told the model to copy the names of places unchanged. No scenario said how a place should be translated. | REQ-WLD-047 | #704 | 2026-09-30 | accepted |
| [0038](lessons/0038-a-paused-video-that-played-on-when-its-link-was-replaced.md) | A paused video that played on when its link was replaced | A visitor's paused video started playing again when its expired link was replaced, because the player autoplays every source it loads. The scenario never said whether the video was playing, so a test raced the clip to its end. | REQ-MED-033 | #680 | 2026-09-29 | accepted |
| [0037](lessons/0037-a-host-that-refused-to-start-where-the-adr-said-it-would.md) | A host that refused to start where the ADR said it would | Staging deployed, but every API request returned 502, because the API threw at startup without an identity provider. ADR-0158 said such an environment still serves its public pages, and the smoke test called a health route the API never mapped. | REQ-MOD-156 | #647 | 2026-09-29 | accepted |
| [0036](lessons/0036-a-setting-aws-records-differently-than-it-was-asked.md) | A setting AWS records differently than it was asked | Staging's first successful apply failed the release's drift re-plan, because CloudFront and RDS each store a setting differently from how Terraform requested it, which leaves a permanent diff. | `manage-hpac-infrastructure` | #645 | 2026-09-29 | accepted |
| [0035](lessons/0035-a-guard-checked-against-the-resource-a-call-creates.md) | A guard checked against the resource a call creates | Staging's apply was refused three more ways once it reached the Lambda functions, security-group rules, and NAT Auto Scaling group — a reserved Lambda variable, a tag guard AWS evaluated against a brand-new untaggable rule, and a launch that needed permission on another account's AMI. | `manage-hpac-infrastructure` | #643 | 2026-09-29 | accepted |
| [0034](lessons/0034-terraform-arguments-and-tags-never-checked-against-aws-and-the-deploy-role.md) | Terraform arguments and tags never checked against AWS and the deploy role | Staging's first full apply created about 60 resources, then failed four more ways an ECR-repository-name mismatch had already foreshadowed, because no resource argument or tag flow had been checked against AWS's own constraints or the deploy role's guardrails before it ran for real. | `manage-hpac-infrastructure` | #637 | 2026-09-29 | accepted |
| [0033](lessons/0033-a-service-closed-to-new-accounts.md) | A service closed to new accounts | Terraform's AppRegistry application could never be created, because AWS closed AppRegistry to new customers months before the first apply tried to use it. | `manage-hpac-infrastructure` | #633 | 2026-09-29 | accepted |
| [0032](lessons/0032-a-registry-limit-and-a-retry-that-outlasted-nothing.md) | A registry limit and a retry that outlasted nothing | public.ecr.aws's per-IP anonymous rate limit throttled release builds on shared GitHub runners, and the mirror script's fix then hit a permanent skopeo error that its retry loop spent ten minutes retrying anyway. | `manage-hpac-infrastructure` | #629 | 2026-09-29 | accepted |
| [0031](lessons/0031-a-deploy-role-with-more-gaps-than-its-first-error-showed.md) | A deploy role with more gaps than its first error showed | The deploy role's first real apply failed on servicecatalog:TagResource; auditing it against every resource Terraform creates found six more permission gaps that would each have failed a later release. | `manage-hpac-infrastructure` | #626 | 2026-09-29 | accepted |
| [0030](lessons/0030-a-deploy-job-that-ran-steps-before-their-own-prerequisites.md) | A deploy job that ran steps before their own prerequisites | deploy-environment.yml ran a local action before checkout, and would have run the full terraform apply before any image existed in ECR for Lambda's CreateFunction to read. | `manage-hpac-infrastructure` | #623 | 2026-09-28 | accepted |
| [0029](lessons/0029-an-ami-pin-that-named-no-image-and-a-tag-that-drifted-in-case.md) | An AMI pin that named no image, and a tag that drifted in case | The fck-nat AMI lookup was pinned to the Terraform module's version rather than an AMI build, and Terraform tagged every resource Project=hpac-safety while the deploy role's policy required the exact case HPAC-Safety. | `manage-hpac-infrastructure` | #617 | 2026-09-28 | accepted |
| [0028](lessons/0028-a-var-file-path-that-only-worked-outside-chdir.md) | A -var-file path that only worked outside -chdir | Every Terraform command ran with -chdir=infra, which resolves -var-file after the chdir, so a repository-root path never found the tfvars file. | `manage-hpac-infrastructure` | #615 | 2026-09-28 | accepted |
| [0027](lessons/0027-a-trust-policy-named-a-subject-form-github-no-longer-sends.md) | A trust policy named a subject form GitHub no longer sends | bootstrap.sh's OIDC trust policies matched the name-only subject form, but this repository has GitHub's immutable subject turned on, so no token it issues ever matched. | `manage-hpac-infrastructure` | #612 | 2026-09-28 | accepted |
| [0026](lessons/0026-a-run-waiting-on-reviewers-held-every-later-run.md) | A run waiting on reviewers held every later run | A Terraform apply awaiting environment approval held terraform.yml's workflow-level concurrency group on main, so every later main push was queued and cancelled without reporting infra for four days, and agent-config's unauthenticated API calls failed a required check on a shared rate limit. | `deliver-change`, `deliver-hpac-change` | #552 | 2026-09-26 | accepted |
| [0025](lessons/0025-a-local-gate-that-re-implemented-ci-disagreed-with-it.md) | A local gate that re-implemented CI disagreed with it | Two local coverage scripts each re-implemented CI's ratchet on macOS, and their verdict often differed from CI's, so the gate lesson 0010 added could pass locally and fail on the pull request. | `deliver-change`, `deliver-hpac-change` | #540 | 2026-09-26 | accepted |
| [0024](lessons/0024-a-translate-that-only-filled-the-empty-side.md) | A Translate that only filled the empty side | The question editor's Translate stayed disabled on every existing question, because the scenarios specified translating a new question from one written language and never an edit to a question already written in both. | REQ-QB-172, REQ-QB-173, REQ-QB-164, REQ-QB-170 | #522 | 2026-09-26 | accepted |
| [0023](lessons/0023-a-rule-the-template-never-asks-for.md) | A rule the template never asks for | Two web UI pull requests reached review without the screenshots the delivery skills require, because the pull request template an author fills in had no Screenshots section and no check refused the omission. | `deliver-change`, `deliver-hpac-change` | #530 | 2026-09-26 | accepted |
| [0022](lessons/0022-a-closed-list-kept-where-the-author-never-looks.md) | A closed list kept where the author never looks | A pull request claimed a feature-coverage exemption as "copy" and failed, because the closed category list lived only in ADR-0090 and the tool, never in the template, instructions, or skill an author reads while writing the body. | `deliver-hpac-change` | #473 | 2026-09-25 | accepted |
| [0021](lessons/0021-a-consent-question-found-by-a-key-it-was-never-seeded-under.md) | A consent question found by a key it was never seeded under | On every seeded database, the publication-consent answer reached the model as an eligible fact, because the Worker left consent out by the key consent_publish while the seeded question kept its Typeform key, and the test built its own consent question instead of using the seeded one. | REQ-AI-009, REQ-QB-027, `test-hpac-safety`, `test-from-scenarios` | #450 | 2026-09-25 | accepted |
| [0020](lessons/0020-a-copy-change-that-ran-no-browser-test.md) | A copy change that ran no browser test | A pull request that changed only French catalogue strings merged with the browser suite skipped, and main's French review-page test broke, because CI's web and e2e path filters did not list the locales/ directory the web bundle loads. | REQ-MOD-075, `deliver-hpac-change`, `deliver-change` | #437 | 2026-09-25 | accepted |
| [0019](lessons/0019-a-language-code-the-provider-never-offered.md) | A language code the provider never offered | French-to-English machine translation always failed, because the DeepL adapter asked for EN-CA, a target DeepL does not have, and the unit test asserted the same code. | REQ-WLD-028, REQ-WLD-029, `test-hpac-safety`, `test-from-scenarios` | #419 | 2026-09-24 | accepted |
| [0018](lessons/0018-a-persisted-checkout-token-outranks-the-pat-on-the-remote.md) | A persisted checkout token outranks the PAT on the remote | The translation bot's push authenticated as the built-in GITHUB_TOKEN rather than the PAT on its remote URL, so the CI it started waited for a maintainer to approve it. | `deliver-hpac-change`, `deliver-change` | #416 | 2026-09-24 | accepted |
| [0017](lessons/0017-a-screenshot-linked-by-a-page-url-renders-broken.md) | A screenshot linked by a page URL renders broken | PR screenshots rendered as broken images, first with relative paths and then with github.com blob URLs, because neither resolves to image bytes in a PR body. | `deliver-hpac-change`, `deliver-change` | #28 | 2026-09-24 | accepted |
| [0016](lessons/0016-a-push-filtered-by-paths-starts-no-run-to-supersede-yours.md) | A push filtered by paths starts no run to supersede yours | The traceability bot lost a push race to the translation bot and dropped its commit, because the rule for a rejected push assumed every push starts every bot, and GitHub filters paths per push. | `deliver-hpac-change`, `deliver-change` | #394 | 2026-09-24 | accepted |
| [0015](lessons/0015-a-storage-format-shown-as-a-display-format.md) | A storage format shown as a display format | The admin report view and the continue dialog showed dates and times in their ISO 8601 storage form, because the specification said how an answer is stored and never said how a person reads it. | REQ-MOD-075, REQ-MOD-076, REQ-SUB-068 | #403 | 2026-09-24 | accepted |
| [0014](lessons/0014-a-local-image-cache-hides-a-withdrawn-upstream.md) | A local image cache hides a withdrawn upstream | MinIO withdrew its public images, so every CI run failed to pull the test S3 server while every developer machine kept passing on a cached arm64 copy. | `test-hpac-safety`, `test-from-scenarios` | #406 | 2026-09-24 | accepted |
| [0013](lessons/0013-a-generated-file-with-a-whole-tree-total-conflicts-with-every-branch.md) | A generated file with a whole-tree total conflicts with every branch | .spec/traceability.md carried a count across every scenario and a table of adjacent rows, so parallel pull requests conflicted on it and squash merges left main stale, however well CI regenerated it. | `hpac-safety-conventions`, `coding-conventions` | #395 | 2026-09-23 | accepted |
| [0012](lessons/0012-upload-translated-as-download.md) | Upload translated as download | Every French attachment string said télécharger, which Canadian French reads as download, because nothing told the translator the word and nothing checked it afterwards. | REQ-WLD-026, REQ-WLD-027 | #377 | 2026-09-23 | accepted |
| [0011](lessons/0011-a-branch-rebased-before-its-push-is-behind-by-the-time-it-is-green.md) | A branch rebased before its push is behind by the time it is green | An agent rebased onto origin/main right before pushing, as the workflow required, then reported the pull request ready once its checks went green — but main had moved while the checks ran, and the branch was already out of date. | `deliver-hpac-change`, `deliver-change` | #358 | 2026-09-23 | accepted |
| [0010](lessons/0010-a-coverage-gate-found-in-ci-not-before-the-pull-request.md) | A coverage gate found in CI, not before the pull request | A pull request opened with every test green failed CI's branch-coverage ratchet, because the gate was never run locally and "tests pass" was taken to mean "checks pass". | REQ-QB-099, REQ-QB-102, `deliver-hpac-change`, `deliver-change` | #352 | 2026-09-22 | superseded |
| [0009](lessons/0009-a-choice-list-nobody-could-see.md) | A choice list nobody could see | The "Where" question offered five choices while the page for curating choices showed none, because a choice could live in two places and the page only looked in one. | REQ-QB-099, REQ-QB-100 | #352 | 2026-09-22 | accepted |
| [0008](lessons/0008-containers-outlive-the-worktree-that-started-them.md) | Containers outlive the worktree that started them | An agent brought the dev environment up from its worktree and removed the worktree without tearing the containers down, so the next ./dev-up.sh from the primary checkout could not bind its ports and timed out after five minutes. | `deliver-hpac-change`, `deliver-change` | #349 | 2026-09-22 | accepted |
| [0007](lessons/0007-a-question-key-shown-to-the-person-who-cannot-choose-it.md) | A question key shown to the person who cannot choose it | The question editor rendered the stable question key as a field — required when authoring, read-only but indistinguishable when editing — though an administrator has no basis to choose one. | REQ-QB-086, REQ-QB-087, REQ-QB-088, REQ-QB-096 | #342 | 2026-09-22 | accepted |
| [0006](lessons/0006-an-internal-identifier-leaked-into-the-authoring-screen.md) | A choice code nobody could supply, and a reporter choice nobody recorded | Administrators were asked for an option code they could not know, and a reporter's new type-ahead value was refused at submission while a hollow step reported it covered. | REQ-QB-090, REQ-QB-091, REQ-QB-092, REQ-QB-094, REQ-QB-095, REQ-SUB-006 | #335 | 2026-09-22 | accepted |
| [0005](lessons/0005-an-outcome-computed-and-never-recorded.md) | An outcome computed and never recorded | Every submitted image was decoded, re-encoded, and stripped of EXIF, then the fact that it had been was thrown away — no ReportFile ever became viewable. | REQ-MED-010 | #311 | 2026-09-22 | accepted |
| [0004](lessons/0004-a-rule-read-once-is-not-a-rule-checked-again.md) | A rule read once is not a rule checked again | An agent that had already followed the worktree rule twice on issue #24 started issue #82 by editing eleven files directly on main — the rule existed in prose, but nothing forced a check at the moment it mattered. | `deliver-hpac-change`, `deliver-change` | #82 | 2026-09-22 | accepted |
| [0003](lessons/0003-a-number-is-claimed-the-moment-someone-else-merges.md) | A number is claimed the moment someone else merges | Three ADR numbers were taken out from under a branch in one afternoon, because the number was chosen when work started and verified never. | `deliver-hpac-change`, `deliver-change` | #319 | 2026-09-22 | accepted |
| [0002](lessons/0002-provenance-that-hashes-only-one-side-of-a-pair.md) | Provenance that hashes only one side of a pair | Hand-written French survived under a stamp claiming a machine wrote it, then was silently overwritten, because only the English was hashed. | REQ-WLD-012, REQ-WLD-013, `localize-hpac-app` | #215 | 2026-09-21 | accepted |
| [0001](lessons/0001-a-guard-that-lives-only-in-ci-is-not-a-guard.md) | A guard that lives only in CI is not a guard | Thirty-eight @ui scenarios failed on every fresh clone while CI stayed green, because the rule was a --filter argument rather than code. | `hpac-safety-conventions`, `coding-conventions` | #219 | 2026-09-21 | accepted |
