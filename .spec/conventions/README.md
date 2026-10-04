---
title: Conventions
description: Where new process, tooling, and agent-workflow rules live instead of ADRs, and the shape each convention takes.
type: guide
---

# Conventions

A convention is a rule about how this repository is worked on — tooling, CI,
hooks, the delivery workflow, what an agent is expected to do. Since
[ADR-0192](../decisions/ADR-0192-an-accepted-adr-is-immutable-and-process-rules-are-conventions.md),
a **new** rule of that kind is written here, not as an ADR. The process ADRs
written before it stay in [`../decisions/`](../decisions/README.md).

An ADR records architecture and is immutable once accepted. A convention is a
living rule: it is edited in place when the rule changes, and git keeps its
history.

## The shape

One file per convention, `CONV-NNN-kebab-slug.md`, numbered in the order they
are written. A number is never reused or renumbered.

Frontmatter:

- `title`, `description`, `type: convention`;
- `status`: `accepted`, or `superseded` when another convention or an ADR
  replaced it;
- `date`: when it was first accepted.

Body:

- `# CONV-NNN — <the rule, as a sentence>`;
- `## Rule` — the rule, in imperative bullets;
- `## Why` — the reason, with the issue, lesson, or ADR behind it;
- `## Enforced by` (optional) — the tool, hook, or check that holds it.

The skill that agents read for the rule names its topic, never the convention
itself (CONV-009); `AGENTS.md` links the convention, and the convention holds
the rule's reason. `node tools/spec/check-records.ts` checks the shape,
and the generated [specification index](../README.md#conventions) lists every
convention.
