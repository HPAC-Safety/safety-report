---
title: The repository's own tools own the specification directory
description: The tools under tools/spec decide how the specification directory is generated and checked; the generic spec-driven-development generator's writing commands never run here, and its checks are not gates.
type: convention
status: accepted
date: 2026-10-05
---

# CONV-011 — The repository's own tools own the specification directory

## Rule

- **The tools under `tools/spec/`, `tools/docs/`, and `tools/gherkin/` own the
  specification directory.** They generate the claims file, the matrix, and the
  index, and they check records, links, scenarios, and coverage.
- **The generic generator's writing commands never run here.** `init`, `new`,
  and `generate` without `--check` are not used: they overwrite the generated
  files in a shape without step bindings.
- **Its checks are not gates.** `check`, `generate --check`, and `coverage`
  prove nothing about this tree; the repository's own checks are the gate.
- The companion skill maps each generic command to the tool that replaces it.

## Why

Importing the generic `spec-driven-development` skill
([#867](https://github.com/HPAC-Safety/safety-report/issues/867)) brought a
generator whose `check` reports about 932 errors on this tree. Its `generate`
would also rewrite the claims file and matrix without the step-definition
bindings that [ADR-0184](../decisions/ADR-0184-a-generated-map-binds-every-claim-to-its-step-definitions.md)
requires.

Rejected: adopting the generator, or making the tree pass it. That is a rewrite
of the specification's shape and its tooling for no new rule, and it drops the
bindings.

## Enforced by

Partly: `generate-spec-index.ts --check` in pre-commit and the `docs` job
fails an index the generic generator rewrote, and
`generate-traceability.ts --check` in `docs` fails a claims file that lost its
bindings. Nothing stops the commands from running.
