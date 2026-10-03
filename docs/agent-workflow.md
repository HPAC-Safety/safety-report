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
makes it useful is what it refuses. Five are declared here. Four of them —
`spec-author`, `test-writer`, `implementer`, `spec-reviewer` — are the steps of
the specification-driven chain, each holding one job and trusting only the
artifact from the step before it
([ADR-0086](../.spec/decisions/ADR-0086-four-role-agents-defined-in-the-repository.md)).
The fifth, `ai-author`, maintains the agent instructions and sits outside the
chain
([ADR-0121](../.spec/decisions/ADR-0121-a-fifth-role-maintains-the-agent-instructions.md)).
Each declares the model and reasoning effort it runs on
([ADR-0182](../.spec/decisions/ADR-0182-a-role-agent-declares-its-model-and-effort.md)).
They are definitions an operator invokes, not a pipeline: the repository's
gates remain the enforcement.

Runtime AI instructions are not coding-agent skills. The one current prompt
lives under `src/HpacSafety.Worker/Prompts/` and is deployed with the Worker.

## The specification in the graph

graphify ingests markdown and cannot ingest a `.feature` file — its document
extensions are a hardcoded set with no configuration hook — and this repository
does not fork it to change that
([ADR-0088](../.spec/decisions/ADR-0088-the-matrix-carries-the-specification-into-the-graph.md)).

[`.spec/traceability.md`](../.spec/traceability.md) is the bridge. It is markdown, so it
enters the graph, and it carries every claim ID with its area, scenario name,
executing engine, and covered-or-planned status, plus every constraint and what
verifies it. Ask the graph about a claim; open the `.feature` file when you need
the `Given`/`When`/`Then` text behind it.

## Generated files

| Output | Owning command |
|---|---|
| `.claude/skills/`, `.claude/agents/` | `skillfile install` |
| `Skillfile.lock` | `skillfile add`, `skillfile remove`, or `skillfile upgrade`; then `skillfile install` |
| `docs/form-spec.md` | `tools/dev/extract-typeform.py` |
| `.spec/traceability.md` | `node tools/spec/generate-traceability.ts`; on a same-repo PR, `traceability.yml` commits it (ADR-0101) |
| `.spec/README.md` | `node tools/spec/generate-spec-index.ts`; committed with the matrix by `traceability.yml` (ADR-0183) |
| `.spec/bindings.md` | `node tools/spec/generate-bindings.ts`; committed with the matrix by `traceability.yml`, and fails `docs` when a built claim's step is unbound (ADR-0184) |
| `locales/fr-CA.json`, `locales/fr-CA.meta.json` | `tools/i18n/translate-locale.ts` |
| `src/web/dist/` | `npm --prefix src/web run build` |

Question text is not generated from locale catalogues: every database question
revision is manually authored in English and French.

## Finish

Run relevant tests and validation, inspect the diff, push the branch, and open a
PR containing `Closes #<number>`. Keep working until required checks are green.
See [`deliver-change`](../skills/deliver-change/SKILL.md) and
[`deliver-hpac-change`](../skills/deliver-hpac-change/SKILL.md).
