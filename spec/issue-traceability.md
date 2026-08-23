# Issue traceability

This audit covers every GitHub issue visible in the repository through issue
#82 on 2026-08-23: 45 issues total, 18 open and 27 closed. Open product issues
were reconciled to this specification; #47 remains bot-managed. Closed
historical issues are not reopened merely because their implementation will be
replaced.

## All issues

| Issue | State at audit | Specification disposition |
|---|---|---|
| [#2 — CI pipeline and deployment scaffolding](https://github.com/HPAC-Safety/safety-report/issues/2) | Closed | Historical foundation; CI/deploy separation remains, while target gates are in [testing](testing-and-quality.md). |
| [#3 — Solution scaffold and Testcontainers/Shouldly](https://github.com/HPAC-Safety/safety-report/issues/3) | Closed | Implemented foundation and retained convention. |
| [#4 — Docker Compose development environment](https://github.com/HPAC-Safety/safety-report/issues/4) | Closed | Implemented local-development support; no target conflict. |
| [#5 — Coverage gate and ratchet](https://github.com/HPAC-Safety/safety-report/issues/5) | Closed | Implemented quality gate; retained. |
| [#6 — Domain model and enums](https://github.com/HPAC-Safety/safety-report/issues/6) | Closed | Historical implementation is partially superseded; replacement domain/schema work is tracked by #79. |
| [#7 — EF Core context and initial migration](https://github.com/HPAC-Safety/safety-report/issues/7) | Closed | Historical current schema; canonical upgrade/fresh migration is tracked by #79. |
| [#8 — Bilingual UI plumbing](https://github.com/HPAC-Safety/safety-report/issues/8) | Open | Aligned for UI catalogues; question revisions are manually bilingual and summaries come from one model call. |
| [#9 — Hardcoded-string lint and locale parity](https://github.com/HPAC-Safety/safety-report/issues/9) | Closed | Implemented and retained for application chrome. |
| [#10 — CI translation for fr-CA](https://github.com/HPAC-Safety/safety-report/issues/10) | Closed | Retained only for stable UI catalogues; it must not translate database questions or runtime summaries. |
| [#11 — Tailwind HPAC theme](https://github.com/HPAC-Safety/safety-report/issues/11) | Closed | Implemented scaffold and retained. |
| [#12 — Public occurrence report form](https://github.com/HPAC-Safety/safety-report/issues/12) | Closed | Historical requirements exist, but main has no form page; the canonical implementation is tracked by #80. |
| [#14 — Submit reports with DTO and worker handoff](https://github.com/HPAC-Safety/safety-report/issues/14) | Open | Aligned: one final multipart request, known superseded revisions, streaming quarantine, and atomic report/answer/file/outbox persistence. |
| [#15 — Rate limits](https://github.com/HPAC-Safety/safety-report/issues/15) | Open | Aligned: mandatory Turnstile plus trusted-IP submission throttling and separate admin lockout. |
| [#16 — Blob storage, pre-signed uploads, and EXIF stripping](https://github.com/HPAC-Safety/safety-report/issues/16) | Closed | Keep useful private storage/type/image work, supersede pre-submit URLs, and complete video/document processing in #81. |
| [#17 — Worker claim and summary DTO](https://github.com/HPAC-Safety/safety-report/issues/17) | Open | Aligned: exact revision-bound summary DTO, one pair result, bounded retries, and no attachments/consent/deleted content. |
| [#18 — Deterministic PII scrub](https://github.com/HPAC-Safety/safety-report/issues/18) | Closed | Superseded and irrelevant to target runtime. Remove scrub code/tests/guidance rather than add another anonymization stage. |
| [#19 — Retired: aircraft answers use the standard question flow](https://github.com/HPAC-Safety/safety-report/issues/19) | Closed | Retired with no replacement. Aircraft-related responses are ordinary database-driven answers and receive no specialized processing. |
| [#20 — One runtime prompt and one AI summary call](https://github.com/HPAC-Safety/safety-report/issues/20) | Open | Aligned: real-person text removed; one strict EN/FR response, one pair row, and no extra processing stage. |
| [#24 — Safety-officer access and audit](https://github.com/HPAC-Safety/safety-report/issues/24) | Open | Aligned: current hardcoded-TLS HPAC adapter, future OIDC behind `IMemberAuthenticator`, local roles, secure sessions, and audits. |
| [#25 — Review summaries](https://github.com/HPAC-Safety/safety-report/issues/25) | Open | Aligned to one bilingual row, pair-level edit/approval, manual recovery, deletion, and safe attachment access. |
| [#27 — Bilingual end-to-end journey](https://github.com/HPAC-Safety/safety-report/issues/27) | Open | Aligned to final multipart submission, one-call pair, private documents, approval invalidation, deletion, and public DTO boundaries. |
| [#28 — Public feed](https://github.com/HPAC-Safety/safety-report/issues/28) | Open | Aligned: exact four-field DTO containing ID, both texts, and publication time. |
| [#30 — Minimal AWS deployment](https://github.com/HPAC-Safety/safety-report/issues/30) | Open | Aligned: separate static sites, no SES/email, managed encryption, explicit migrations, OIDC, and focused Worker alerts. |
| [#31 — Replace Typeform](https://github.com/HPAC-Safety/safety-report/issues/31) | Open | Aligned to cut over only after every canonical flow and operational check works. |
| [#32 — AWS bootstrap and Terraform](https://github.com/HPAC-Safety/safety-report/issues/32) | Closed | Useful implemented foundation; prune resources that exist only for superseded email/combined-site design. |
| [#33 — Turnstile Terraform](https://github.com/HPAC-Safety/safety-report/issues/33) | Closed | Aligned foundation; connect configuration to final submission enforcement. |
| [#35 — Renovate automerge](https://github.com/HPAC-Safety/safety-report/issues/35) | Closed | Repository operations only; no product-design effect. |
| [#36 — require-config action failure](https://github.com/HPAC-Safety/safety-report/issues/36) | Closed | Historical CI fix; no product-design effect. |
| [#40 — One-command development setup](https://github.com/HPAC-Safety/safety-report/issues/40) | Closed | Implemented contributor tooling; retain. |
| [#47 — Dependency Dashboard](https://github.com/HPAC-Safety/safety-report/issues/47) | Open | Bot-managed operational issue; intentionally not rewritten or treated as product scope. |
| [#49 — Admin immutable question editor](https://github.com/HPAC-Safety/safety-report/issues/49) | Open | Aligned: complete revisions, no-resurrection current selection, manual bilingual copy, consent invariants, and answer-aware deletion. |
| [#53 — Coverage ratchet first-feature fix](https://github.com/HPAC-Safety/safety-report/issues/53) | Closed | Historical quality-gate correction; retained. |
| [#61 — Typed partitioned summarizer input](https://github.com/HPAC-Safety/safety-report/issues/61) | Closed | Concept aligns; adjust output to bilingual pair and exclude all attachment/document content. |
| [#63 — Third-party libraries behind abstractions](https://github.com/HPAC-Safety/safety-report/issues/63) | Closed | Narrowed: keep owned ports at real external boundaries, not one abstraction per library or removed feature. |
| [#66 — Date/time value types](https://github.com/HPAC-Safety/safety-report/issues/66) | Closed | Aligned and retained: DateOnly/TimeOnly/DateTimeOffset, never unspecified DateTime. |
| [#69 — Retired: no aircraft-specific input or processing](https://github.com/HPAC-Safety/safety-report/issues/69) | Closed | Historical discussion is retired. Aircraft-related responses use the ordinary question/answer path with no specialized service, typed projection, or special UI. |
| [#70 — Agent skill extraction](https://github.com/HPAC-Safety/safety-report/issues/70) | Closed | Initial skill work is present; the audited pruning/alignment in [implementation status](implementation-status.md) is still required. |
| [#72 — Private context and LLM anonymization](https://github.com/HPAC-Safety/safety-report/issues/72) | Closed | Core privacy partition and role-replacement intent align. Separate auditors/translators/legacy prompts remain superseded. |
| [#74 — Simplify flow and prune guidance](https://github.com/HPAC-Safety/safety-report/issues/74) | Closed (declined) | The associated implementation was not based on current main. Its valid simplicity intent is incorporated here; its branch is preserved as history, not merged. |
| [#76 — Complete system specification](https://github.com/HPAC-Safety/safety-report/issues/76) | Closed | Completed by merged specification pull request #77. |
| [#78 — Align repository guidance and backlog](https://github.com/HPAC-Safety/safety-report/issues/78) | Open | Owns this README/skill/prompt/ADR/backlog reconciliation and is closed by its pull request. |
| [#79 — Canonical domain and persistence migration](https://github.com/HPAC-Safety/safety-report/issues/79) | Open | Added foundational slice for complete revisions, consent-only answers, pair summaries, deletion columns, managed encryption, and removal of retired types. |
| [#80 — Database-driven report form and browser continuity](https://github.com/HPAC-Safety/safety-report/issues/80) | Open | Added because the historical form issue is closed while current main has no page; covers current questions, bilingual rendering, 15-day answers, and final multipart assembly. |
| [#81 — Image, video, and private document processing](https://github.com/HPAC-Safety/safety-report/issues/81) | Open | Added for metadata-safe derivatives and validated forced-download documents that never enter AI/public output. |
| [#82 — Irreversible soft deletion and retention](https://github.com/HPAC-Safety/safety-report/issues/82) | Open | Added for transactional cascade stamping, live-flow exclusion, answer-aware question deletion, append-only audit, and private retained bytes. |

## Audit actions

The initial specification pass created #76 and declined the non-main work in
#74/#75. This alignment pass created #78, corrected open product issues #14,
#15, #17, #20, #24, #25, #27, #28, #30, #31, and #49, and added focused missing
work as #79–#82. Issue #8 already matched the target; bot-managed #47 was left
untouched. No useful product issue was deleted or closed. Closed issues remain
historical evidence and do not override this specification.
