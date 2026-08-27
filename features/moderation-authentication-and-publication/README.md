# Moderation, authentication, and publication

Supporting detail for
[`moderation-authentication-and-publication.feature`](moderation-authentication-and-publication.feature)
that doesn't fit Gherkin.

## Authentication boundary

Core retains one small `IMemberAuthenticator` port. Its result establishes the
stable upstream HPAC member identifier; the local allowlist then decides
whether that identity can enter the admin application and with which role.
Domain and authorization code do not depend on the upstream protocol.

The current target adapter may proxy credentials to HPAC's hardcoded TLS
member login endpoint and supports a configuration kill switch. When HPAC
offers OIDC/OAuth, another adapter can replace it without changing roles or
the allowlist contract.

State-changing admin requests require CSRF protection in addition to the
session cookie.

## Roles

| Role | Capabilities |
|---|---|
| SafetyOfficer | View the review queue and private report material; view safe image/video derivatives and download validated unredacted documents; edit the bilingual summary pair; approve, reject, publish, and soft-delete reports. |
| Administrator | Every SafetyOfficer capability, plus create question revisions and manage the admin allowlist/roles. |

## Public DTO edge state

The requested UI locale may determine which text is displayed first but is
edge state, not extra report data.
