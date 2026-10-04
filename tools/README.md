---
title: Tools
description: How the repository's scripts under tools/ are grouped and named, and where their tests live.
type: readme
---

# Tools

Every script a workflow, a git hook, or a contributor runs lives here, grouped
by the domain it serves
([ADR-0189](../.spec/decisions/ADR-0189-a-workflow-step-runs-one-command-and-tools-is-grouped-by-domain.md)).
A workflow step runs one command; its logic is a script here. Each script's test
is `tests/js/<group>/<name>.test.ts`.

Every script is TypeScript, run directly by Node 24's type stripping: no build
step, no flag, only erasable syntax (no `enum`, `namespace`, or constructor
parameter properties; `erasableSyntaxOnly` refuses them). Import a sibling with
its `.ts` extension, and a type with `import type`. Node does not check types:
`npm run typecheck` (tsc, in CI's `lint` job) and `npm run lint` do
([ADR-0189](../.spec/decisions/ADR-0189-a-workflow-step-runs-one-command-and-tools-is-grouped-by-domain.md)
amendment).

| Group | Holds |
|---|---|
| `lib/` | Shared helpers: `actions.ts` (exec, outputs, summary, annotations) |
| `spec/` | The specification's generators and gates: traceability, index, bindings, feature coverage and its area map, claim results, `@ignore` ownership, ADR numbers |
| `docs/` | Markdown gates: frontmatter, links, generic instructions, source inventory |
| `web/` | Web front-end gates: component split, hardcoded strings, bundle, coverage scope, screenshots |
| `i18n/` | Translation and locale parity |
| `coverage/` | The coverage gate, its baseline, test counts |
| `build/` | Container images and the .NET major |
| `github/` | Pull-request, issue, release, and hook glue |
| `infra/` | Terraform and deployment steps |
| `dev/` | Local-only helpers: `ci-local.sh`, session label, Typeform extraction, the act runner image, `git-hook-shim.sh` (the installed git hook, which runs the tracked `.githooks/<name>`), `sync-agent-tooling.sh` (the `SessionStart` check that installs and prunes `.claude/` agents and skills when they drift from the `Skillfile`) |
| `gherkin/` | Gherkin syntax check, with its own `package.json` |

## Names

| Prefix | Means |
|---|---|
| `check-*` | A read-only gate; exits 1 on failure |
| `generate-*` | Writes a tracked generated file |
| `guard-*` | A hook that refuses an action |
| `remind-*` | A hook that only reminds, and never blocks |
| `build-*` | Produces an artifact |
| `find-*`, `read-*` | Hands a value to a later step |
| `report-*` | Writes summary or comment markdown |
| other verb-object | Any other action (`push-to-pr-branch`) |
| a noun | An importable module or a multi-mode command (`translator`, `adr-numbers`) |

A script is dependency-free, exports pure functions plus a `main()` that returns
an exit code, takes its effects through `lib/actions.ts` so a test can stub
them, and reads its inputs from the environment.
