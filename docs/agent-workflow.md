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
4. Work from current `main` on `issue-<number>/<short-description>`.

Project-owned skill sources live under `skills/` and role agents under
`agents/`. `skillfile install` generates tool-specific copies under `.claude/`;
never edit or commit those copies. Keep local skills concise and
HPAC-specific. Search before adding generic guidance, and do not install a
skill whose architecture conflicts with `.spec/features`.

A skill and an agent are not the same thing. A skill is knowledge, loaded when
its topic is in play, and it constrains nothing. An agent is a role, and what
makes it useful is what it refuses. Ten are declared here. Four of them —
`spec-author`, `test-writer`, a builder (`backend`, `ux`, or `infrastructure`),
`spec-reviewer` — are the steps
of the specification-driven chain, each holding one job and trusting only the
artifact from the step before it
([ADR-0197](../.spec/decisions/ADR-0197-backend-and-ux-replace-the-implementer-and-the-adversary-takes-privacy-review.md),
which supersedes
[ADR-0086](../.spec/decisions/ADR-0086-four-role-agents-defined-in-the-repository.md)).
`ai-author` maintains the agent instructions and `database-administrator` the
schema
([ADR-0121](../.spec/decisions/ADR-0121-a-fifth-role-maintains-the-agent-instructions.md));
`critic` challenges a plan and `adversary` attacks a change
([CONV-007](../.spec/conventions/CONV-007-a-plan-meets-the-critic-and-a-change-meets-the-adversary.md)).
Each declares the model and reasoning effort it runs on
([ADR-0182](../.spec/decisions/ADR-0182-a-role-agent-declares-its-model-and-effort.md)).
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
| `.claude/skills/`, `.claude/agents/` | `skillfile install` |
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
See [`deliver-change`](../skills/deliver-change/SKILL.md) and
[`deliver-hpac-change`](../skills/deliver-hpac-change/SKILL.md).
