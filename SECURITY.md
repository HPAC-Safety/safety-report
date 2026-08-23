# Security policy

This system stores accounts of real aviation accidents, including names, phone
numbers, injuries, and occasionally fatalities. A vulnerability here can expose
personal information about identifiable people who filed reports in good faith
under a non-punitive policy.

Please treat findings accordingly.

## Reporting

**Do not open a public issue.**

Use [GitHub private vulnerability
reporting](https://github.com/HPAC-Safety/safety-report/security/advisories/new),
or email **safety@hpac.ca** if you cannot.

Please include what you found, how to reproduce it, and what an attacker could
reach. If you accessed real report data while investigating, say so — we need to
know what was exposed, and you will not be penalised for reporting it.

## Scope — things we especially want to hear about

- **Anonymization failure.** A published summary containing a name, phone
  number, email, member number, site name, or aircraft make and model. This is
  the failure this system exists to prevent.
- **Credential handling.** Admin login proxies credentials to
  `members.hpac.ca`. Any path where a credential is persisted, cached, logged,
  or lands in an exception message is a serious finding.
- **Unauthorized access to raw reports**, which are admin-only and contain
  unredacted personal information.
- **Attachment access or processing.** Unauthorized access to an original or
  derivative; an image/video derivative retaining identifying metadata; a
  document rendered inline, sent to AI, exposed publicly, or made available
  before format and malware checks pass.
- **Consent bypass** — any route by which a report marked "do not publish"
  becomes publishable.
- Standard web issues: authentication bypass, injection, SSRF, XSS.

## Out of scope

- Findings against `hpac.ca` or `members.hpac.ca` — those are separate systems
  not maintained here. Report them to HPAC directly.
- Missing hardening headers with no demonstrated impact.
- Automated scanner output without a working proof of concept.
- Rate-limit findings against a local development instance.

## Please do not

- Access, download, or retain real occurrence report data beyond what is needed
  to demonstrate the issue.
- Run automated scanners against production.
- Publicly disclose before we have had a chance to fix it.

## What to expect

We are a small volunteer project. We will acknowledge as soon as we can, tell
you honestly whether and when we can fix it, and credit you if you would like
that.
