# Moderation, authentication, and publication

## Authentication boundary

Core retains one small `IMemberAuthenticator` port. Its result establishes the
stable upstream HPAC member identifier; the local allowlist then decides
whether that identity can enter the admin application and with which role.
Domain and authorization code do not depend on the upstream protocol.

The current target adapter may proxy credentials to HPAC's hardcoded TLS member
login endpoint. It must never store, log, cache, enqueue, or put credentials in
URLs; it uses no caller-supplied upstream host, follows a narrow redirect
policy, has timeouts, and supports a configuration kill switch. When HPAC
offers OIDC/OAuth, another adapter can replace it without changing roles or the
allowlist contract.

Successful authentication issues a short-lived Secure, HttpOnly, SameSite
cookie. State-changing admin requests require CSRF protection. Login is subject
to trusted-IP rate limiting and per-identity lockout without revealing whether
an identity is allowlisted. Sessions become invalid when an admin is revoked or
soft-deleted.

## Authorization

There are exactly two application roles:

| Role | Capabilities |
|---|---|
| SafetyOfficer | View the review queue and private report material; view safe image/video derivatives and download validated unredacted documents; edit the bilingual summary pair; approve, reject, publish, and soft-delete reports. |
| Administrator | Every SafetyOfficer capability, plus create question revisions and manage the admin allowlist/roles. |

Authorization is enforced by the API for every operation, not only by hiding UI
controls. Sensitive reads and material mutations are audited with actor,
action, target, and time but no report content.

## Review queue

The default queue shows non-deleted reports needing action: submitted/stuck,
summarizing beyond its expected age, summary failed, or pending review. A detail
query supplies the reporter language, exact bilingual question labels and
answers with privacy indicated, processing state, both summary texts and their
shared provenance/approval, and short-lived links for successful image/video
derivatives or validated private documents only.

A reviewer can edit either summary language. Any edit clears pair approval and
unpublishes a previously published report. Approval applies once to the current
English/French pair. Rejection blocks publication but retains the report for
internal learning. `SummaryFailed` supports manual authoring of both texts.

## Publication

Publication is a state change guarded again by the domain and public query. It
requires a non-deleted report, explicit positive consent, two nonblank summary
texts, and current human approval of their pair. There is no bypass for an
Administrator, migration, background worker, or direct API caller.

The public list/detail DTO exposes only:

- opaque report ID;
- `ai_summary_en`;
- `ai_summary_fr`; and
- publication timestamp.

The requested UI locale may determine which text is displayed first but is
edge state, not extra report data. The public API never returns question keys,
labels, answers, consent value, report language, private flags, raw reports,
attachment metadata/URLs, admin identities, model provenance, or audit records.

The only publication surfaces are the HPAC public feed and report-detail page.
There is no email, messaging, social, webhook, or third-party publication
channel in this system.

## Deletion effects

Soft-deleting a report immediately removes it from the public feed and normal
review queries and stops ordinary Worker processing. Soft-deleting an admin
revokes current access while preserving historic audit attribution. There is no
restore workflow and no UI action that physically deletes either record.
