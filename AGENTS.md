# AGENTS.md

Instructions for any coding agent working in this repository. `CLAUDE.md`,
`.github/copilot-instructions.md`, and `.cursor/rules/agents.mdc` are symlinks to
this file; edit only this file.

## Design authority

[`features/README.md`](features/README.md) is the canonical target design.
Source and tests show the current implementation, while issues and ADRs
preserve history. They do not override the specification. If a requested
design change conflicts with `/features`, call out the conflict and update
the affected specification pages before implementing it.

The application receives real aviation occurrence reports containing personal
and medical information. Keep the system small and treat every data boundary as
privacy-sensitive.

## Product invariants

1. Questions come from the database as complete immutable bilingual revisions.
   Every edit creates a new revision. Only explicit publication consent is a
   required system question; every ordinary question may be skipped.
2. Until final submission, unfinished answers and shown revision IDs stay only
   in that browser for 15 days; files are not persisted or restored. No report,
   attachment, draft, reserved ID, or other respondent data is written to a
   server or database. A reporter then submits one final multipart request. The
   API stores the report, exact question revisions, answers, files, and outbox
   work atomically, then returns `202` without making a model call.
3. The Worker owns one versioned prompt and makes exactly one model call per
   summary attempt. `report_content` supplies eligible facts; labeled
   `private_context` may only help recognize identifying text. The response is
   one strict English/French summary pair.
4. Replace a private person's complete identity with a role. A pilot's name
   repeated in eligible narrative becomes exactly “the pilot” / “le pilote,”
   with no name fragment remaining. Private-only facts never become summary
   facts.
5. Documents such as PDF, DOC, DOCX, RTF, Markdown, text, and ODT are validated,
   malware-checked, and kept private. They are not anonymized, transformed,
   parsed, sent to the model, inline-rendered, or published.
6. Publication requires positive consent, a non-deleted report, and human
   approval of the current bilingual pair. Editing either language clears the
   pair approval.
7. HPAC member credentials are never stored, logged, cached, or included in an
   exception. The current hardcoded-TLS credential proxy and a future OIDC
   adapter both sit behind `IMemberAuthenticator`.
8. Use managed encryption at rest and TLS. Do not add application-level field
   encryption, log report content, or physically delete application records.

There is no deterministic scrubber, separate PII auditor, runtime translator,
specialized aircraft processing, outbound email flow, pre-submit
upload session, or speculative publication channel.

## Focused skills

Read only the skills relevant to the task. Installed copies under
`.claude/skills/` are generated; the project-owned sources are under `skills/`.

| Work | Guidance |
|---|---|
| Any repository change | [`hpac-safety-conventions`](skills/hpac-safety-conventions/SKILL.md) |
| Genuinely ambiguous product behavior | [`clarify-hpac-requirements`](skills/clarify-hpac-requirements/SKILL.md) |
| Tests and fixtures | [`test-hpac-safety`](skills/test-hpac-safety/SKILL.md) |
| Summary privacy or runtime prompt | [`anonymize-hpac-reports`](skills/anonymize-hpac-reports/SKILL.md) |
| Questions, reports, lifecycle, review, publication | [`incident-domain-model`](skills/incident-domain-model/SKILL.md) |
| EF Core, migrations, or query DTOs | [`persist-hpac-data`](skills/persist-hpac-data/SKILL.md) |
| Attachments or private object storage | [`handle-hpac-media`](skills/handle-hpac-media/SKILL.md) |
| English/French behavior | [`localize-hpac-app`](skills/localize-hpac-app/SKILL.md) |
| Static HTML/JS and design system | [`build-hpac-web-ui`](skills/build-hpac-web-ui/SKILL.md) |
| AWS, Terraform, or deployment | [`manage-hpac-infrastructure`](skills/manage-hpac-infrastructure/SKILL.md) |
| Issues, docs, branches, PRs, or CI | [`deliver-hpac-change`](skills/deliver-hpac-change/SKILL.md) |

Use plain code until a real external boundary or a second implementation makes
an abstraction useful. Do not introduce a pattern merely to name one.

## Runtime prompt

Runtime model instructions live with the Worker under
`src/HpacSafety.Worker/Prompts/`; they are not coding-agent skills. Keep one
current versioned prompt. Add a version when behavior changes, record its
version with each summary, and remove obsolete active-pipeline machinery.

## Delivery

Every change starts from an issue and reaches `main` through a pull request.
Put `Closes #<number>` on its own line in the PR body, use a squash-ready title,
do not add `Co-Authored-By` trailers, and keep working until required checks are
green. Follow [`deliver-hpac-change`](skills/deliver-hpac-change/SKILL.md).

Use Shouldly for .NET assertions, Given/When/Then test structure, Mermaid for
diagrams, locale catalogues for UI copy, and synthetic data in tests and docs.
Never hand-edit generated files.

## Where to look

| Need | Source |
|---|---|
| Target product and architecture | [`features/README.md`](features/README.md) |
| Current implementation gaps | [`docs/implementation-status.md`](docs/implementation-status.md) |
| Current Typeform question evidence | [`docs/form-spec.md`](docs/form-spec.md) |
| Setup | [`README.md`](README.md), `./init-dev.sh` |
| Test conventions | [`docs/testing-conventions.md`](docs/testing-conventions.md) |
| Historical rationale | [`docs/decisions/README.md`](docs/decisions/README.md) |
