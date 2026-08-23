# Admin authentication

The public incident form is anonymous. Review and question-management endpoints
require authentication by a standard configured OIDC/OAuth provider plus an
active local allowlist row.

`AdminUser.Subject` stores the provider's stable subject and a local role. It is
not a password or credential. Revoking the row removes access without deleting
the audit trail.

The application must not collect, store, or proxy credentials for hpac.ca or
`members.hpac.ca`. Provider tokens are validated using the platform's standard
middleware, and authorization is enforced again by the API for every admin
request.

The final identity provider and callback configuration belong to issue #24;
until then the repository contains no custom authenticator abstraction.
