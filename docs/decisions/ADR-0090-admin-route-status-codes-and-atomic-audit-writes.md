---
title: Admin routes answer with real 401/403, and an audit write is atomic with its action
description: The web app never masks an admin page as a 404; a signed-out visitor is redirected to sign in and a signed-in member without the required role sees a real 403. Every audited admin action writes its audit row in the same transaction as the action, and sign-out is not audited because there is nothing server-side to audit.
type: adr
status: accepted
date: 2026-09-22
decision-makers: Chase Florell
keywords: audit, authorization, admin routes, 401, 403, atomicity, security
---

# ADR-0090 — Admin routes answer with real 401/403, and an audit write is atomic with its action

## Status

Accepted.

## Context

Issue #24 asks for two things: protecting the private review surface, and
auditing what happens on it. Both raised questions this ADR settles.

**Route protection.** [ADR-0048](ADR-0048-one-website-admin-as-a-route.md)
already established that `/admin/*` is reachable by URL on purpose and that
the API — not the client route — is the security boundary
([ADR-0064](ADR-0064-jwt-bearer-authentication-with-three-roles.md)). What it
did not settle is what the *client* shows when a visitor who cannot use an
admin page reaches it anyway. The owner's first instinct was to mask every
unauthorized admin page as a 404, so a visitor without rights could not tell
the page existed. On reflection the owner reversed that: a 404 disguises a
real authorization failure as a routing failure, which makes debugging and
support harder for no privacy gain — `/admin/*` already publicly exists as a
route in the one shipped JS bundle (ADR-0048), so hiding one page's identity
behind 404 does not hide the admin surface itself. The API continues to
answer 401/403 (REQ-MOD-023/024); the client route guard is a convenience
that mirrors the real reason, not a disguise.

**Audit atomicity.** `AuditLogEntry`/`AuditAction`
(`src/HpacSafety.Core/Features/Moderation/`) already model a content-free,
append-only audit row, but nothing writes one yet (REQ-MOD-029, `@ignore`).
Left unanswered: if the audit write fails, does the moderation action it
describes still happen? A reviewer action that commits with no way to later
prove it happened is a compliance gap in a system whose entire audit value
proposition is "who saw what."

**Sign-out.** Authentication is a stateless JWT bearer token
([ADR-0064](ADR-0064-jwt-bearer-authentication-with-three-roles.md),
[ADR-0065](ADR-0065-no-user-records-identity-is-the-token-subject.md)); there
is no server-side session to end. "Sign-out" today is the client discarding
its own token — nothing calls the API. Auditing it would mean adding an
endpoint whose only reason to exist is to be audited.

## Decision

**Admin routes.** The web app adds a client-side route guard for every
`/admin/*` page:

- A visitor who is not signed in is redirected to `/login`. They have not
  presented a role to be denied yet, so there is nothing to authorize.
- A signed-in visitor whose role does not permit the page renders a real
  forbidden (403) page in place of the admin content, without calling the
  page's data API. The Admin menu already hides options a role cannot use
  (REQ-MOD-007/008/009); the route guard is what catches a visitor who
  navigates directly to a URL the menu never offered them.
- The API's own 401/403 responses on every admin endpoint
  (REQ-MOD-023/024) are unchanged and remain the actual enforcement boundary
  — the client guard only decides what to render before a request is even
  made.
- No admin page is ever rendered as a 404. `NotFoundPage` is reserved for a
  route that genuinely does not exist.

**Audit writes.** Every audited action (moderation decision, question
create/revise/reorder/deactivate/delete, raw-report view, attachment view,
sign-in outcome) writes its `AuditLogEntry` in the same database transaction
as the action it describes. If the audit write fails, the transaction rolls
back and the action did not happen from the caller's point of view. A
sign-in audit row (success or failure) is the one case with no application
transaction to join — it commits on its own, immediately before the token
response is returned, and a failure to write it fails the sign-in attempt
the same way.

**Sign-out is not audited.** The AC in issue #24 is narrowed from
"sign-in/sign-out outcomes" to sign-in outcomes only. No sign-out endpoint is
added. This does not generalize into an endpoint-per-audit-event pattern —
every other audited action already has a real server-side operation to
attach its audit row to.

**Report submission stays unattributed.** No change here, restated for
scope: filing a report records no member identity at all
([ADR-0067](ADR-0067-a-reporter-must-be-a-member-and-is-not-recorded.md)),
so an audit row can never link a report to whoever submitted it — there is no
subject available to record even if this ADR wanted one.

## Rejected alternatives

**Mask every unauthorized admin page as 404.** The owner's original
request. Rejected on reflection: the admin route tree is already public
knowledge (it ships in the one JS bundle per ADR-0048), so 404-masking one
page buys no real secrecy while making a real 401/403 harder to diagnose
from the outside — for a system whose actual security boundary has always
been the API, not the page a browser renders.

**404 at the API layer too, for full masking.** Would additionally rewrite
REQ-MOD-023/024 and their existing API tests, and does not follow from
rejecting page-level masking — the API is the boundary and states its
reason plainly (401 unauthenticated, 403 wrong role), which downstream
tooling and reviewers rely on.

**Best-effort audit writes.** Rejected because an admin action that commits
without a provable audit row defeats the reason this table exists.

**A sign-out endpoint added solely to be audited.** Rejected — it would be
new server-side surface invented to satisfy an audit requirement rather than
audit an operation that needed to exist anyway.

## Consequences

- The web app gets a route-guard component wrapping every `/admin/*` route:
  redirect-to-login when signed out, a real 403 view when signed in with the
  wrong role, page content otherwise.
- A `Forbidden`/403 page is added alongside the existing `NotFoundPage`.
- `AuditAction` gains values for a sign-in outcome and for an attachment
  view, distinct from the existing `ViewedRawReport`.
- Every moderation/question-authoring code path that currently commits its
  own change gets an audit row written in the same transaction, not a
  fire-and-forget call after commit.
- `features/moderation-authentication-and-publication/moderation-authentication-and-publication.feature`
  gains scenarios for the 401-redirect, the 403 page, sign-in audit
  (success and failure), attachment-view audit, and audit/action atomicity,
  in the same PR that implements them.

## Related

- [ADR-0048](ADR-0048-one-website-admin-as-a-route.md) — the API, not the
  route, is the security boundary
- [ADR-0064](ADR-0064-jwt-bearer-authentication-with-three-roles.md) — the
  three roles and stateless bearer token this decision assumes
- [ADR-0065](ADR-0065-no-user-records-identity-is-the-token-subject.md) — why
  there is no session to end on sign-out
- [ADR-0067](ADR-0067-a-reporter-must-be-a-member-and-is-not-recorded.md) —
  why a report can never be linked to a submitter
- Issue #24
