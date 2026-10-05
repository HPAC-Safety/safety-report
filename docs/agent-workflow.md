---
title: Working with coding agents
description: How instructions, skills, and generated files are arranged for the agents that work here.
type: guide
---

# Working with coding agents

[`AGENTS.md`](../AGENTS.md) is the only always-loaded repository instruction;
the tool-specific instruction paths are symlinks to it. The product-design
authority is [`.spec/features/README.md`](../.spec/features/README.md).

## Start

1. Run `./init-dev.sh` or `./init-dev.sh --check`.
2. Read `AGENTS.md`, the affected `.spec/features` pages, and the focused issue.
3. Load only the project skills relevant to the task.
4. Work in a worktree off fresh `origin/main`, on `issue-<number>/<short-description>`.

Project-owned skill sources (`hpac-*`) live under `skills/`. The role agents
and generic skills live in [agent-team](https://github.com/ChaseFlorell/agent-team); install them
globally by cloning it and running `skillfile install` there (optional, into
`~/.claude`). This repository pins nothing from it: Claude Code resolves
personal skills over project skills but project agents over personal ones, so a
project pin plus a global install would skew versions. A change to a moved file
is a pull request to agent-team; each user re-runs its `skillfile install`.
`skillfile install` here generates tool-specific copies under `.claude/`;
never edit or commit those copies. The post-merge and post-rewrite hooks and
`init-dev.sh` keep `.claude/` in step: after a change to `Skillfile`,
`Skillfile.lock`, or `skills/` they run `skillfile install` and delete
any installed agent or skill the `Skillfile` no longer declares. Personal agents
and skills, the agent-team install included, go in `~/.claude`, not `.claude/`. A fast-forward rebase, `git
worktree add`, `merge --squash`, and a hand-resolved conflicted merge fire no
such hook; the `SessionStart` check (`tools/dev/sync-agent-tooling.sh`, wired in
`.claude/settings.json`) catches what the git hooks can't see, and an edit to an
agent or skill that changes no name. It compares the installed names and a
content fingerprint with the last install's stamp; when they differ it syncs in
the background and logs to `.skillfile/cache/agent-tooling/sync.log`.
`./init-dev.sh` remains the manual fallback. Keep local skills concise and
generic: a skill is HPAC-specific only when its content cannot transfer
([CONV-009](../.spec/conventions/CONV-009-a-skills-name-says-hpac-exactly-when-it-is-project-specific.md)). Search before adding generic guidance, and do not install a
skill whose architecture conflicts with `.spec/features`.

A skill and an agent are not the same thing. A skill is knowledge, loaded when
its topic is in play; it never widens or narrows what a role may do. An agent is a persona and a
role, and what makes it useful is what it refuses. Eleven are declared in agent-team, each
with a human name; the file says who the agent is, what it does, and what it
leaves to others, and how it works is in the generic skills it preloads
([CONV-008](../.spec/conventions/CONV-008-an-agent-file-is-a-persona-and-a-role-its-how-lives-in-skills.md)).
Four of them — `spec-author` (Jennifer), `test-writer` (Kevin), a builder
(`backend` Brad, `ux` Tiffany, or `infrastructure` Dave), `spec-reviewer`
(Jessica) — are the steps of the specification-driven chain, each holding one
job and trusting only the artifact from the step before it (CONV-008). `ai-author` (Emily)
maintains the agent instructions and `database-administrator` (Jane) the
schema; `critic` (Karen) challenges a plan, `adversary` (Kyle) attacks a
change, and `auditor` (Ashley) audits the whole repository on demand
([CONV-007](../.spec/conventions/CONV-007-a-plan-meets-the-critic-and-a-change-meets-the-adversary.md)).
Each declares the model and reasoning effort it runs on. A skill's name says
`hpac` exactly when it is project-specific
([CONV-009](../.spec/conventions/CONV-009-a-skills-name-says-hpac-exactly-when-it-is-project-specific.md)).
They are definitions an operator invokes, not a pipeline: the repository's
gates remain the enforcement.

Runtime AI instructions are not coding-agent skills. The one current prompt
lives under `src/HpacSafety.Worker/Prompts/` and is deployed with the Worker.

## The specification in the graph

graphify cannot ingest a `.feature` file — its document extensions are a
hardcoded set with no configuration hook — skips data-shaped JSON, and gives
Markdown only its headings without an LLM pass. This repository does not fork
it ([ADR-0088](../.spec/decisions/ADR-0088-the-matrix-carries-the-specification-into-the-graph.md)).

Instead `node tools/spec/graph-fragment.ts` merges the specification into the
local `graphify-out/graph.json`: one node per claim, constraint, ADR, lesson,
and feature area, read from [`.spec/claims.json`](../.spec/claims.json), with the
claim's step text on its node and typed edges — `specified_in`, `bound_by`,
`verified_by`, `cites`, `supersedes`, `amends`, `updates`, `documented_in`. The
post-merge and post-rewrite hooks and `init-dev.sh` run it, and the nodes
survive `graphify update`
([ADR-0193](../.spec/decisions/ADR-0193-the-claims-are-generated-as-json-a-graph-fragment-and-one-slim-matrix.md)).
Ask by ID — `graphify query "REQ-WLD-049"`, `graphify query "ADR-0190"` — and
open the `.feature` file for the scenario in context. `graphify explain` lists a
node's typed edges; where a code comment also cites the ID, name the node:
`graphify explain spec_decision_adr_0190`.

## Generated files

| Output | Owning command |
|---|---|
| `.claude/skills/`, `.claude/agents/` | `skillfile install`, then a prune of what the `Skillfile` no longer declares, which includes any stale `.claude/agents/*.md` when the `Skillfile` declares no agents; run by the post-merge and post-rewrite hooks and `init-dev.sh` |
| `Skillfile.lock` | `skillfile add`, `skillfile remove`, or `skillfile upgrade`; then `skillfile install` |
| `docs/form-spec.md` | `tools/dev/extract-typeform.py` |
| `docs/issue-traceability.md` | `node tools/spec/generate-issue-traceability.ts`, from GitHub; the drift issue `issue-traceability.yml` keeps asks for it (ADR-0191) |
| `.spec/claims.json`, `.spec/traceability.md` | `node tools/spec/generate-traceability.ts`; on a same-repo PR, `traceability.yml` commits them (ADR-0101), and `docs` fails when a built claim's step is unbound (ADR-0184, ADR-0193) |
| `.spec/README.md` | `node tools/spec/generate-spec-index.ts`; committed with the claims by `traceability.yml` (ADR-0183) |
| The specification in `graphify-out/graph.json` (untracked) | `node tools/spec/graph-fragment.ts`; run by the post-merge and post-rewrite hooks and `init-dev.sh` (ADR-0193) |
| `locales/fr-CA.json`, `locales/fr-CA.meta.json` | `tools/i18n/translate-locale.ts` |
| `src/web/dist/` | `npm --prefix src/web run build` |

Question text is not generated from locale catalogues: every database question
revision is manually authored in English and French.

## Finish

Run relevant tests and validation, inspect the diff, push the branch, and open a
PR containing `Closes #<number>`. Keep working until required checks are green.
See [`deliver-change`](https://github.com/ChaseFlorell/agent-team/blob/main/skills/deliver-change/SKILL.md) and
[`deliver-hpac-change`](../skills/deliver-hpac-change/SKILL.md).
