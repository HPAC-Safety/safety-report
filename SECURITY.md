# Security policy

This system stores private accounts of aviation incidents. Do not report a
vulnerability in a public issue. Use [GitHub private vulnerability
reporting](https://github.com/HPAC-Safety/safety-report/security/advisories/new)
or email safety@hpac.ca.

High-priority findings include:

- a summary that identifies a person or reveals private-context data;
- publication without explicit consent and human approval;
- unauthorized access to raw answers or private uploads;
- authentication or allowlist bypass;
- report values, model payloads, or credentials appearing in logs;
- injection, SSRF, or XSS that reaches private report data.

Do not run automated scanners against production or retain real report data
beyond what is necessary to describe the finding. `hpac.ca` and
`members.hpac.ca` are separate systems and are outside this repository's scope.
