# AGENTS.md

Instructions for agents working in this repository. `CLAUDE.md`, Copilot, and
Cursor instruction files are symlinks to this file.

## Product boundary

HPAC collects safety occurrence reports. Keep the application limited to this
flow:

1. Read the latest active immutable question revision for each key, ordered by
   `SortOrder`.
2. Show its English or French text. Every question may be skipped except the
   `consent_publish` yes/no question, which has no default.
3. Accept a submission DTO containing the exact question revision ids and the
   answers, including skipped answers. Save the report, answers, and worker
   outbox row atomically.
4. Query a worker DTO containing the exact questions asked, their privacy
   flags, and the submitted answers.
5. Reject a submission that fails Cloudflare Turnstile server-side `siteverify`
   before it reaches the database.
6. Send answered non-private fields as `report_content` and answered private
   fields as labeled `private_context` to one LLM call using the current Worker
   prompt. Private context is only a recognition aid. A private pilot name
   repeated in narrative becomes “the pilot”; no part of the name survives.
7. Generate one candidate summary in the report's own language, then translate
   it through `ITranslator` to produce the second official language. Store both
   for human review. Publish only after explicit consent and approval of both.
8. Notify `safety@hpac.ca` that a report is ready for review, riding the same
   outbox row so a failed send never rolls back the report.

Question wording is authored in one language and translated into the other
through `ITranslator` before the row is saved — an administrator never has to
type both by hand. Aircraft classification is not a subsystem: it is two
ordinary data-driven questions, a public certification class and a private
make/model, handled by the same privacy partition as everything else. Do not
add typed projections for ordinary answers, a deterministic text scrubber, a
second AI audit, external publication channels, or other processing stages
without a new approved requirement.

## Privacy invariants

- A published summary contains no identifying information.
- `consent_publish` is the only system question and the only required answer.
- Every question revision is a complete immutable snapshot: bilingual text,
  type, options, order, privacy, active state, and any display metadata. Every
  change inserts a new revision.
- Reports reference the exact revisions shown to the reporter.
- `report_content` is the only source of summary facts. `private_context` may
  only help remove or role-generalize matching details.
- Raw answers and model payloads never enter logs, telemetry, email, issue
  bodies, or committed fixtures.
- Uploaded media is EXIF-stripped before a reviewer can see it, is never
  attached to a published summary, and is only ever reached through the
  presigned-URL chokepoint — never a raw blob key.
- Nothing is published without positive consent and human approval.

When changing anonymization, prompts, question privacy, or public output, read
[`skills/anonymize-hpac-reports/SKILL.md`](skills/anonymize-hpac-reports/SKILL.md).
Runtime instructions belong under `src/HpacSafety.Worker/Prompts/`, not in a skill.

## Repository conventions

- Target .NET 10 with nullable enabled and warnings as errors.
- Keep `HpacSafety.Core` independent of infrastructure libraries.
- Use `DateOnly`, `TimeOnly`, and `DateTimeOffset`; never `DateTime`.
- Store invariant option codes and localize only at the UI boundary.
- Use Shouldly in .NET tests and `node:test` in JavaScript tests.
- Use Mermaid for diagrams.
- Prefer plain code. Add an interface only at a real external boundary or when
  more than one implementation is required.

Generated files must be regenerated through their owner: `docs/form-spec.md`
through `tools/extract-typeform.py` and `src/web/styles/site.css` through
`tools/build-css.sh`.

## Delivery

Every change starts from a GitHub issue, uses a branch, and reaches `main`
through a pull request that closes the issue. Preserve unrelated worktree
changes. Add an ADR only for a decision that would otherwise be reasonably
reopened; keep READMEs short and current. Run tests and configuration checks
proportionally to the change and never weaken a privacy assertion to pass CI.

Canonical product behavior lives in [`docs/architecture.md`](docs/architecture.md),
question parity in [`docs/form-spec.md`](docs/form-spec.md), and runtime model
behavior in [`src/HpacSafety.Worker/Prompts/`](src/HpacSafety.Worker/Prompts/).
