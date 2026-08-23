# Issue traceability

This audit covers every GitHub issue visible in the repository through issue
#76 on 2026-08-23: 40 issues total, 14 open and 26 closed after the disposition
of issue #74. “Requires alignment” means the issue remains useful but its body
must follow this specification when implemented. Closed historical issues are
not reopened merely because their implementation will be replaced.

## All issues

| Issue | State at audit | Specification disposition |
|---|---|---|
| [#2 — CI pipeline and deployment scaffolding](https://github.com/HPAC-Safety/safety-report/issues/2) | Closed | Historical foundation; CI/deploy separation remains, while target gates are in [testing](testing-and-quality.md). |
| [#3 — Solution scaffold and Testcontainers/Shouldly](https://github.com/HPAC-Safety/safety-report/issues/3) | Closed | Implemented foundation and retained convention. |
| [#4 — Docker Compose development environment](https://github.com/HPAC-Safety/safety-report/issues/4) | Closed | Implemented local-development support; no target conflict. |
| [#5 — Coverage gate and ratchet](https://github.com/HPAC-Safety/safety-report/issues/5) | Closed | Implemented quality gate; retained. |
| [#6 — Domain model and enums](https://github.com/HPAC-Safety/safety-report/issues/6) | Closed | Historical implementation is partially superseded: remove ordinary typed projections and adopt complete revisions/pair summaries/deletion. |
| [#7 — EF Core context and initial migration](https://github.com/HPAC-Safety/safety-report/issues/7) | Closed | Historical current schema; requires a target migration described in [data and persistence](data-and-persistence.md). |
| [#8 — Bilingual UI plumbing](https://github.com/HPAC-Safety/safety-report/issues/8) | Open | Aligned for UI catalogues; question revisions are manually bilingual and summaries come from one model call. |
| [#9 — Hardcoded-string lint and locale parity](https://github.com/HPAC-Safety/safety-report/issues/9) | Closed | Implemented and retained for application chrome. |
| [#10 — CI translation for fr-CA](https://github.com/HPAC-Safety/safety-report/issues/10) | Closed | Retained only for stable UI catalogues; it must not translate database questions or runtime summaries. |
| [#11 — Tailwind HPAC theme](https://github.com/HPAC-Safety/safety-report/issues/11) | Closed | Implemented scaffold and retained. |
| [#12 — Public occurrence report form](https://github.com/HPAC-Safety/safety-report/issues/12) | Closed | Issue history captured form requirements, but main has no form page. Implement against current revision/submission specs rather than treating closure as feature completion. |
| [#14 — Submit reports with DTO and worker handoff](https://github.com/HPAC-Safety/safety-report/issues/14) | Open | Requires alignment: one multipart final submission and acceptance of known superseded revisions; reject only unknown/deleted/invalid revisions. |
| [#15 — Rate limits](https://github.com/HPAC-Safety/safety-report/issues/15) | Open | Requires alignment: Turnstile is mandatory on public submission, alongside trusted-IP throttling; admin has separate lockout. |
| [#16 — Blob storage, pre-signed uploads, and EXIF stripping](https://github.com/HPAC-Safety/safety-report/issues/16) | Closed | Keep private storage, type detection, bounds, and image stripping. Supersede pre-submit URLs; add video derivatives and private document support. |
| [#17 — Worker claim and summary DTO](https://github.com/HPAC-Safety/safety-report/issues/17) | Open | Aligned when it loads exact revision-bound answers into the two labeled sections and excludes attachments. |
| [#18 — Deterministic PII scrub](https://github.com/HPAC-Safety/safety-report/issues/18) | Closed | Superseded and irrelevant to target runtime. Remove scrub code/tests/guidance rather than add another anonymization stage. |
| [#19 — Aircraft classification subsystem](https://github.com/HPAC-Safety/safety-report/issues/19) | Closed | Superseded. Safe coarse normalization belongs in the single summarization prompt and must refuse to guess. |
| [#20 — One runtime prompt and one AI summary call](https://github.com/HPAC-Safety/safety-report/issues/20) | Open | Requires alignment: one call returns strict EN/FR JSON and one pair row. Replace its real-person example with synthetic role-only wording before implementation; no personal name fragment belongs in the contract. |
| [#24 — Safety-officer access and audit](https://github.com/HPAC-Safety/safety-report/issues/24) | Open | Requires alignment: retain `IMemberAuthenticator`; HPAC credential proxy is the current adapter with a future OIDC/OAuth swap, plus local allowlist authorization. |
| [#25 — Review summaries](https://github.com/HPAC-Safety/safety-report/issues/25) | Open | Requires alignment to a single bilingual row and pair-level edit/approval, including manual recovery from `SummaryFailed`. |
| [#27 — Bilingual end-to-end journey](https://github.com/HPAC-Safety/safety-report/issues/27) | Open | Aligned; expand fixtures to target submission, documents, deletion, pair approval, and public DTO boundaries. |
| [#28 — Public feed](https://github.com/HPAC-Safety/safety-report/issues/28) | Open | Requires alignment: return both summary texts plus ID/publication time and nothing else; locale is display state. |
| [#30 — Minimal AWS deployment](https://github.com/HPAC-Safety/safety-report/issues/30) | Open | Requires alignment: separate public/admin hosting, no SES/email, managed encryption, explicit migrations, OIDC, and focused Worker alerts. |
| [#31 — Replace Typeform](https://github.com/HPAC-Safety/safety-report/issues/31) | Open | Aligned outcome; cut over only after target bilingual reporting/review/publication and operations pass. |
| [#32 — AWS bootstrap and Terraform](https://github.com/HPAC-Safety/safety-report/issues/32) | Closed | Useful implemented foundation; prune resources that exist only for superseded email/combined-site design. |
| [#33 — Turnstile Terraform](https://github.com/HPAC-Safety/safety-report/issues/33) | Closed | Aligned foundation; connect configuration to final submission enforcement. |
| [#35 — Renovate automerge](https://github.com/HPAC-Safety/safety-report/issues/35) | Closed | Repository operations only; no product-design effect. |
| [#36 — require-config action failure](https://github.com/HPAC-Safety/safety-report/issues/36) | Closed | Historical CI fix; no product-design effect. |
| [#40 — One-command development setup](https://github.com/HPAC-Safety/safety-report/issues/40) | Closed | Implemented contributor tooling; retain. |
| [#47 — Dependency Dashboard](https://github.com/HPAC-Safety/safety-report/issues/47) | Open | Bot-managed operational issue; intentionally not rewritten or treated as product scope. |
| [#49 — Admin immutable question editor](https://github.com/HPAC-Safety/safety-report/issues/49) | Open | Requires alignment: every field, including order/privacy/active/type/options, belongs to a new complete revision; deletion checks all answers including deleted reports. |
| [#53 — Coverage ratchet first-feature fix](https://github.com/HPAC-Safety/safety-report/issues/53) | Closed | Historical quality-gate correction; retained. |
| [#61 — Typed partitioned summarizer input](https://github.com/HPAC-Safety/safety-report/issues/61) | Closed | Concept aligns; adjust output to bilingual pair and exclude all attachment/document content. |
| [#63 — Third-party libraries behind abstractions](https://github.com/HPAC-Safety/safety-report/issues/63) | Closed | Narrowed: keep owned ports at real external boundaries, not one abstraction per library or removed feature. |
| [#66 — Date/time value types](https://github.com/HPAC-Safety/safety-report/issues/66) | Closed | Aligned and retained: DateOnly/TimeOnly/DateTimeOffset, never unspecified DateTime. |
| [#69 — Aircraft rating input/classification](https://github.com/HPAC-Safety/safety-report/issues/69) | Closed | Historical discussion is superseded by ordinary database questions and optional single-prompt normalization. No separate classifier UI/domain subsystem. |
| [#70 — Agent skill extraction](https://github.com/HPAC-Safety/safety-report/issues/70) | Closed | Initial skill work is present; the audited pruning/alignment in [implementation status](implementation-status.md) is still required. |
| [#72 — Private context and LLM anonymization](https://github.com/HPAC-Safety/safety-report/issues/72) | Closed | Core privacy partition and role-replacement intent align. Separate auditors/translators/legacy prompts remain superseded. |
| [#74 — Simplify flow and prune guidance](https://github.com/HPAC-Safety/safety-report/issues/74) | Closed (declined) | The associated implementation was not based on current main. Its valid simplicity intent is incorporated here; its branch is preserved as history, not merged. |
| [#76 — Complete system specification](https://github.com/HPAC-Safety/safety-report/issues/76) | Open | This specification issue. The documentation pull request closes it. |

## Audit actions

Issue #76 was created for this documentation change. Issue #74 and its pull
request #75 were commented and closed as declined so the repository does not
accidentally adopt a non-main implementation. No other issue was edited,
deleted, or closed during this specification pass: several open issues still
represent real work, and their required simplifications are made explicit in
the table rather than erasing useful history. GitHub issues cannot be deleted
through normal repository workflows; irrelevant closed issues remain historical
evidence and must not drive new code.
