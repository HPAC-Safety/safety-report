---
title: A permanent Super Administrator comes from configuration
description: One email, held in a masked GitHub environment secret named SUPER_ADMIN and read from configuration, never from the database, always resolves to Administrator, is always listed on the role page, even when role assignments cannot be read, and can never be removed there.
type: adr
status: accepted
date: 2026-10-16
decision-makers: Chase Florell
keywords: Super Administrator, SUPER_ADMIN, break-glass, configuration, GitHub environment secret, role assignment, ADR-0199, ADR-0172
---

# ADR-0200 — A permanent Super Administrator comes from configuration

**Status:** Accepted. Decided by Chase Florell on 2026-10-16 in
[#909](https://github.com/HPAC-Safety/safety-report/issues/909).

## Context

[ADR-0199](ADR-0199-roles-come-from-application-owned-role-assignments-and-the-provider-proves-membership-only.md)
moves every Administrator and Safety Officer into role assignments in the
database. If the database cannot be read, or every Administrator's
assignment is revoked, or a provider's claims break, nobody could
administer the site. Someone must be able to administer whatever state the
assignments are in.

## Decision drivers

- One way back in that does not depend on the database.
- No person's email in the repository, a log, or a plan comment.
- Nothing on the page can remove it by accident.

## Considered options

- **A seeded first Administrator assignment in the database.** Rejected: it
  fails whenever the database does, and an Administrator could revoke it.
- **An email committed in tfvars or appsettings.** Rejected: it publishes the
  email in a public repository.
- **One email in a masked GitHub environment secret, read from
  configuration** — chosen.

## Decision

- **One Super Administrator per environment.** Its email is held in a masked
  GitHub environment secret named `SUPER_ADMIN`. Terraform creates a Secrets
  Manager entry only; every deploy puts the value from `SUPER_ADMIN`, and
  puts a blank value when the secret is absent or empty. The API's Lambda
  function carries only the entry's ARN, and the API reads the value at cold start,
  the same way it reads the vendor keys. It is never read from the database.
- **It always resolves to Administrator**, with or without a role
  assignment, and when role assignments cannot be read.
- **It is always listed** on the role page as a permanent Administrator, even
  when role assignments cannot be read, and it cannot be removed there. When
  they cannot be read, the page says plainly that the other assignments cannot
  be shown right now, and offers no adding or removing until they can.
- **It is permanent, not interim.** Deleting the interim issuer
  ([ADR-0172](ADR-0172-a-temporary-interim-issuer-signs-staging-tokens-until-a-real-provider-exists.md))
  leaves it in place.
- **Optional, and removable.** A blank value means no Super Administrator.
  Where `SUPER_ADMIN` is unset, the deploy blanks the stored value, so
  removing the secret retires the Super Administrator at the next deploy. This change sets it for staging only. Production
  gets no environment or secret here.
- **Never disclosed.** The value never appears in a log, a Terraform plan
  comment, a committed file, an issue, or a pull request.
- **A real issuer's mapping is deferred.** How a real identity provider's
  token is matched to this email is
  [#911](https://github.com/HPAC-Safety/safety-report/issues/911)'s, recorded
  now as a leading scenario.

## Consequences

- An environment with no `SUPER_ADMIN` and no Administrator assignment can
  be administered only after someone adds one by other means.
- Changing the Super Administrator means changing the secret and
  redeploying.
- Claims: the Super Administrator scenarios in
  [`authentication-and-roles.feature`](../features/authentication-and-roles/authentication-and-roles.feature);
  CON-INF-028.

## Related

- [ADR-0199](ADR-0199-roles-come-from-application-owned-role-assignments-and-the-provider-proves-membership-only.md) — role assignments
- [ADR-0171](ADR-0171-terraform-reads-back-only-the-origin-secret-and-log-groups-are-guarded-by-name.md) — how the deploy treats secret values
