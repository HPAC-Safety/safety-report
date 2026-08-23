# Authentication and authorization

The public reporting and public-feed surfaces require no account. The review and
administration surfaces require an authenticated, allowlisted HPAC member.

`IMemberAuthenticator` is the stable boundary:

- **Current adapter:** submit credentials only to the hardcoded
  `https://members.hpac.ca` login flow over TLS. Keep credentials in local
  variables for one call, never store/log/cache them, enforce timeouts and a
  kill switch, and discard the upstream session immediately.
- **Future adapter:** replace the proxy with OIDC/OAuth when HPAC provides a
  supported identity service. Authorization remains local.

A local allowlist assigns `SafetyOfficer` or `Administrator`. A successful HPAC
login without an active allowlist entry is denied. Sessions are short-lived
Secure/HttpOnly cookies with appropriate SameSite behavior, CSRF protection,
rate limiting/lockout, generic failures, and revocation when access is removed.

Raw-report views, attachment access, summary edits/approval/rejection,
publication, deletion, question changes, and allowlist changes are audited by
actor and time without copying report content into the audit entry.

See
[`spec/moderation-authentication-and-publication.md`](../spec/moderation-authentication-and-publication.md)
for the normative role and endpoint rules.
