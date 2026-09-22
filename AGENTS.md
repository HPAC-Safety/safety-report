# AGENTS.md

Instructions for any coding agent working in this repository. `CLAUDE.md`,
`.github/copilot-instructions.md`, and `.cursor/rules/agents.mdc` are symlinks to
this file; edit only this file.

## Design authority

[`features/README.md`](features/README.md) is the canonical target design.
Source and tests show the current implementation, while issues and ADRs
preserve history. They do not override the specification. If a requested
design change conflicts with `/features`, call out the conflict and update
the affected specification pages before implementing it. A feature file must
never contradict an accepted ADR, in either direction: a new or updated ADR
that changes what a feature file asserts updates that feature file in the
same pull request
([ADR-0047](docs/decisions/ADR-0047-feature-files-must-not-contradict-adrs.md)).

The application receives real aviation occurrence reports containing personal
and medical information. Keep the system small and treat every data boundary as
privacy-sensitive.

## Specification-driven development

Behavior flows through an artifact chain, and every hop is a tracked file
([ADR-0083](docs/decisions/ADR-0083-specification-driven-development.md)):

```
need (issue)
  → scenario            features/<area>/<area>.feature
  → supporting detail   features/<area>/README.md, docs/*.md
  → step definitions    tests/HpacSafety.Acceptance.Tests | tests/e2e/steps
  → code                src/**
```

Four rules follow, and they are not discretionary.

1. **Specify before implementing.** When behavior is added or changed, author
   or amend the scenario first, in the same pull request, and write the
   implementation that makes it pass.
2. **Correct the specification, not the chat.** When an implementation does the
   wrong thing, first ask whether the scenario said the wrong thing. If it did,
   change the scenario and re-run the chain from there. A correction argued in
   conversation leaves no artifact and does not survive the next run.
3. **The specification delta is the change.** A pull request that changes
   behavior names what it changed upstream and cites what it satisfies. The
   pull-request body becomes the commit message, so the delta lands in history.
4. **Say what not to build.** A specification that states only the target
   invites over-delivery into territory nobody asked for. Record the boundary
   where the scenarios are read, not only in the global list in
   [`docs/system-overview.md`](docs/system-overview.md).

You are not trusted to improvise the missing half of a requirement. If reading
the specification leaves a material question, ask it — see
[`clarify-hpac-requirements`](skills/clarify-hpac-requirements/SKILL.md) — and
write the answer back into the specification as a scenario or an out-of-scope
line, so the next run starts from the answer rather than from the question.

Three mechanisms are being adopted alongside these rules and arrive with their
own pull requests: stable claim IDs and a generated traceability matrix
([ADR-0084](docs/decisions/ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md)),
lessons that flow upstream after a bug
([ADR-0085](docs/decisions/ADR-0085-a-lesson-flows-upstream-into-the-specification.md)),
and four roles declared as agents
([ADR-0086](docs/decisions/ADR-0086-four-role-agents-defined-in-the-repository.md)).
Each of those records is `proposed` until the change that applies it lands.

## Product invariants

1. Questions come from the database as complete immutable bilingual revisions.
   An edit to a question nobody has answered creates a new revision; once any
   answer references it, an edit soft-deletes the question and creates a new
   one carrying the same stable key, so an old answer always correlates to the
   question as it was actually worded. Soft deletion is irreversible, and at
   most one question per key is live
   ([ADR-0071](docs/decisions/ADR-0071-an-answered-question-forks-instead-of-revising.md)).
   Publication consent is the only system
   question and the only answer read by name; it can never be made optional,
   and it is the one question that revises in place even when answered, because
   it can never be deleted.
   Every other question's required state is authored by an administrator
   ([ADR-0061](docs/decisions/ADR-0061-administrators-may-require-any-question.md)).
   An administrator authors both languages and may use machine translation as
   a drafting aid while doing so; the database holds only what they saved, and
   a question cannot be saved in one language
   ([ADR-0062](docs/decisions/ADR-0062-administrators-may-machine-translate-question-text.md)).
   A reporter may add a missing choice to a type-ahead, recorded at submission
   and marked for an administrator to curate; a type-ahead therefore renders the
   live shared list while its revision snapshot remains the record of the
   complete set of choices that reporter was offered
   ([ADR-0063](docs/decisions/ADR-0063-a-reporter-may-add-a-type-ahead-choice.md)).
   Every answer is stored as one string, in the reporter's own words, in the
   language they answered in, and is immutable once written. Its second
   language starts unset and is filled off the submission path — mechanically
   by the Worker via the same machine-translation port question authoring
   uses, or by an administrator correcting or supplying it by hand — with the
   source (`auto` or `human`) recorded. This applies to every answer, select
   or free text alike; there is no answer type this ever skips. A boolean is
   `yes` or `no`; a date, time, or date-and-time is ISO 8601 in the shape that
   fits. ISO 8601 is the storage form only — the domain still uses `DateOnly`,
   `TimeOnly`, and `DateTimeOffset`
   ([ADR-0072](docs/decisions/ADR-0072-every-answer-is-stored-as-a-string.md),
   [ADR-0080](docs/decisions/ADR-0080-every-answer-gets-a-worker-translated-second-language.md),
   [ADR-0035](docs/decisions/ADR-0035-dateonly-datetimeoffset-timeonly-datetime-is-banned.md)).
   A question may be made conditional on a yes/no question, or on a
   single-select question naming a required option, and its options may
   be copied from a shared choice list
   ([ADR-0060](docs/decisions/ADR-0060-conditional-questions-depend-on-a-boolean-question.md),
   [ADR-0074](docs/decisions/ADR-0074-a-single-select-parent-may-enable-a-conditional-question.md),
   [ADR-0058](docs/decisions/ADR-0058-shared-option-sets-with-a-revision-snapshot.md)).
2. Until final submission, unfinished answers and shown revision IDs stay only
   in that browser for 15 days; files are not persisted or restored. No report,
   attachment, draft, reserved ID, or other respondent data is written to a
   server or database. A reporter then submits one final multipart request. The
   API stores the report, exact question revisions, answers, files, and outbox
   work atomically, then returns `202` without making a model call.
3. The Worker owns one versioned prompt and makes exactly one model call per
   summary attempt. Before that call, a deterministic marking pass replaces
   any exact or token-level occurrence of a private answer's value found in
   `report_content` with a `[PRIVATE:<question-key>]` marker
   ([ADR-0082](docs/decisions/ADR-0082-a-deterministic-marking-pass-precedes-the-one-model-call.md));
   `report_content` supplies eligible facts, and labeled `private_context`
   (still sent in full) may only help recognize identifying text the marking
   pass did not catch. The response is one strict English/French summary
   pair.
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
7. Identity arrives as a signed JWT that the API validates, reading the subject
   and the role claim and nothing else. This system never handles a member's
   password and stores no user records of any kind: there is no user table, no
   allowlist, and no session store, and an approver or audit actor is an opaque
   token subject that joins to nothing
   ([ADR-0064](docs/decisions/ADR-0064-jwt-bearer-authentication-with-three-roles.md),
   [ADR-0065](docs/decisions/ADR-0065-no-user-records-identity-is-the-token-subject.md)).
   There are three roles — `User`, `SafetyOfficer`, `Administrator`. Filing a
   report requires a member of any role and records nothing about them; the
   form tells the reporter so
   ([ADR-0067](docs/decisions/ADR-0067-a-reporter-must-be-a-member-and-is-not-recorded.md)).
   The one carved exception is Development, where a fourth sign-in path may
   verify a real member's password against the live members site for the
   single call that checks it, never logging or storing it; it does not
   generalize, and it never runs outside Development
   ([ADR-0079](docs/decisions/ADR-0079-a-development-login-may-verify-against-the-live-members-site.md)).
8. Use managed encryption at rest and TLS. Do not add application-level field
   encryption, log report content, or physically delete application records.
   The one carved exception is dropping `admin_users`, a table that never held
   data in any deployed environment; it does not generalize, and any future
   `DROP TABLE` needs its own argument on its own facts
   ([ADR-0065](docs/decisions/ADR-0065-no-user-records-identity-is-the-token-subject.md)).

There is no deterministic scrubber beyond the narrow private-value marking
pass in item 3 above, no separate PII auditor,
specialized aircraft processing, outbound email flow, pre-submit
upload session, speculative publication channel, user table, allowlist,
credential proxy, CSRF machinery, or Turnstile verification. The one carved
exception is Development's members-site-verified login (item 7 above,
[ADR-0079](docs/decisions/ADR-0079-a-development-login-may-verify-against-the-live-members-site.md)):
a hardcoded, Development-only email allowlist for role, and CSRF/session
handling scoped entirely to that one credential source. It does not
generalize, never reaches Production, and any future allowlist or
credential-proxy-shaped code outside this scope needs its own argument on
its own facts. Machine translation never runs on the submission path itself —
nothing a reporter's request touches calls a translation provider. Off that
path it now has three purposes: drafting question wording while authoring,
and, for every answer including a narrative one, the Worker mechanically
supplying its second language or an administrator correcting/supplying one by
hand ([ADR-0080](docs/decisions/ADR-0080-every-answer-gets-a-worker-translated-second-language.md)).
It still never touches a summary — the bilingual summary pair comes from the
Worker's one anonymized model call, never from a translation provider.

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
| EF Core or query DTOs | [`persist-hpac-data`](skills/persist-hpac-data/SKILL.md) |
| Writing or applying a migration | [`manage-hpac-migrations`](skills/manage-hpac-migrations/SKILL.md) |
| Attachments or private object storage | [`handle-hpac-media`](skills/handle-hpac-media/SKILL.md) |
| English/French behavior | [`localize-hpac-app`](skills/localize-hpac-app/SKILL.md) |
| Static HTML/JS and design system | [`build-hpac-web-ui`](skills/build-hpac-web-ui/SKILL.md) |
| AWS, Terraform, or deployment | [`manage-hpac-infrastructure`](skills/manage-hpac-infrastructure/SKILL.md) |
| Issues, docs, worktrees, PRs, or CI | [`deliver-hpac-change`](skills/deliver-hpac-change/SKILL.md) |

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

Use Shouldly for .NET assertions, `GivenX_WhenY_ThenZ` test names
([ADR-0069](docs/decisions/ADR-0069-scannable-given-when-then-test-names.md)),
Mermaid for diagrams
([ADR-0046](docs/decisions/ADR-0046-mermaid-for-diagrams.md)), locale
catalogues for UI copy, and synthetic data in tests and docs. Never hand-edit
generated files.

## Where to look

| Need | Source |
|---|---|
| Target product and architecture | [`features/README.md`](features/README.md) |
| Current implementation gaps | [`docs/implementation-status.md`](docs/implementation-status.md) |
| Current Typeform question evidence | [`docs/form-spec.md`](docs/form-spec.md) |
| Setup | [`README.md`](README.md), `./init-dev.sh` |
| Test conventions | [`docs/testing-conventions.md`](docs/testing-conventions.md) |
| Historical rationale | [`docs/decisions/README.md`](docs/decisions/README.md) |

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
